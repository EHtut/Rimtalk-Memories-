using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Arkh.Prompt;
using Arkh.Settings;
using Arkh.Talk;

namespace Arkh.Model
{
    /// <summary>
    /// Sends one small request and reports what happened, from the settings window.
    ///
    /// This exists because of the twenty-minute load. Without it the only way to discover that a
    /// key is wrong, a model name is misspelled or a local server is not running is to start a
    /// colony and wait for silence — and silence has a dozen causes that look identical. A button
    /// at the main menu turns that into five seconds.
    ///
    /// It deliberately exercises the **whole path**, not just connectivity: the real instruction
    /// and output contract go out, and the reply comes back through the real parser. "Can I reach
    /// this provider" is a less useful question than "will this provider and model actually
    /// produce something this mod can use", and only the second one predicts whether colonists
    /// will speak.
    /// </summary>
    public static class ConnectionTest
    {
        public enum State
        {
            Idle,
            Running,
            Succeeded,
            Failed
        }

        /// <summary>
        /// Immutable, and swapped as a whole.
        ///
        /// The worker writes it and the GUI thread reads it every frame. Replacing one reference is
        /// atomic, so a repaint can never catch a half-updated result — which writing several
        /// fields separately would allow.
        /// </summary>
        public sealed class Result
        {
            public readonly State Status;
            public readonly string Summary;
            public readonly string Detail;

            public Result(State status, string summary, string detail = null)
            {
                Status = status;
                Summary = summary;
                Detail = detail;
            }
        }

        private static volatile Result _result = new Result(State.Idle, "");

        public static Result Current => _result;

        public static bool Running => _result.Status == State.Running;

        public static void Start(ArkhSettings settings)
        {
            if (Running) return;

            if (settings == null)
            {
                _result = new Result(State.Failed, "No settings loaded.");
                return;
            }

            var client = ModelProviders.Create(settings);

            if (!client.Configured)
            {
                _result = new Result(State.Failed,
                    client.Name + " is not configured yet — add an API key above.");
                return;
            }

            var options = ModelProviders.OptionsFrom(settings);

            // A short cap: this is a reachability check, not a sample of the writing. Nobody should
            // pay for three hundred tokens to learn that their key works.
            options.MaxTokens = 60;

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(CoreSections.DefaultInstruction + "\n\n" + CoreSections.DefaultContract),
                ChatMessage.User("Tala is alone, mending a wall. Write what they say out loud.")
            };

            _result = new Result(State.Running, "Contacting " + client.Name + "…");

            Task.Run(() => Run(client, messages, options));
        }

        private static void Run(IModelClient client, List<ChatMessage> messages, ModelOptions options)
        {
            Completion completion;
            try
            {
                completion = client.Complete(messages, options);
            }
            catch (Exception ex)
            {
                _result = new Result(State.Failed, "The request threw unexpectedly.", ex.Message);
                return;
            }

            if (!completion.Ok)
            {
                _result = new Result(State.Failed, completion.Failure.Summary, completion.Failure.Detail);
                return;
            }

            var lines = ResponseContract.Parse(completion.Text, "Tala");

            string cost = string.Format("{0:0.0}s · {1} prompt + {2} reply tokens",
                completion.ElapsedMs / 1000f, completion.PromptTokens, completion.CompletionTokens);

            if (lines.Count == 0)
            {
                // Reached the provider, got words back, and could not use them. Distinct from a
                // connection failure and pointing at a different fix — usually a model too small
                // to follow the output contract.
                _result = new Result(State.Failed,
                    completion.Provider + " replied, but nothing usable could be read from it.",
                    cost + "\nRaw reply: " + Shorten(completion.Text));
                return;
            }

            _result = new Result(State.Succeeded,
                completion.Provider + " is working. " + cost,
                "Parsed as speech — " + lines[0].Speaker + ": " + lines[0].Text);
        }

        private static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            return s.Length <= 240 ? s : s.Substring(0, 240) + "…";
        }
    }
}
