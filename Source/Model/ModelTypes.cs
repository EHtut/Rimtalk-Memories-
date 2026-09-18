using System.Collections.Generic;

namespace Arkh.Model
{
    public enum ChatRole
    {
        System,
        User,
        Assistant
    }

    public struct ChatMessage
    {
        public ChatRole Role;
        public string Content;

        public ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content;
        }

        public static ChatMessage System(string content) => new ChatMessage(ChatRole.System, content);
        public static ChatMessage User(string content) => new ChatMessage(ChatRole.User, content);
        public static ChatMessage Assistant(string content) => new ChatMessage(ChatRole.Assistant, content);

        /// <summary>The wire name every OpenAI-compatible provider expects.</summary>
        public string RoleName
        {
            get
            {
                switch (Role)
                {
                    case ChatRole.System: return "system";
                    case ChatRole.Assistant: return "assistant";
                    default: return "user";
                }
            }
        }
    }

    /// <summary>Per-request knobs. Defaults are deliberately conservative about spend.</summary>
    public sealed class ModelOptions
    {
        public string Model = "";
        public float Temperature = 0.9f;
        public int MaxTokens = 300;
        public int TimeoutSeconds = 30;

        public ModelOptions Clone() => (ModelOptions)MemberwiseClone();
    }

    /// <summary>
    /// Why a request did not produce text.
    ///
    /// These are separated because each wants a different response from the player, and lumping
    /// them into one "it failed" is what makes an LLM integration infuriating to diagnose. A bad
    /// key is permanent and worth shouting about once; a rate limit is temporary and should be
    /// quiet; an unreachable local server means Ollama is not running, which is a completely
    /// different sentence from "your quota is gone".
    /// </summary>
    public enum FailureKind
    {
        None,
        NotConfigured,
        BadKey,
        RateLimited,
        QuotaExhausted,
        Timeout,
        Unreachable,
        Malformed,
        Refused,
        Cancelled,
        Unknown
    }

    public sealed class ModelFailure
    {
        /// <summary>One short sentence fit to show a player, with what to do about it.</summary>
        public string Summary;

        /// <summary>The underlying detail, for the log only. May contain provider text.</summary>
        public string Detail;

        public FailureKind Kind;

        /// <summary>
        /// True when retrying the identical request could plausibly work. Rate limits and
        /// timeouts are; a bad key and a malformed reply are not, and retrying those just burns
        /// the player's quota faster.
        /// </summary>
        public bool Transient
        {
            get
            {
                switch (Kind)
                {
                    case FailureKind.RateLimited:
                    case FailureKind.Timeout:
                    case FailureKind.Unreachable:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public ModelFailure(FailureKind kind, string summary, string detail = null)
        {
            Kind = kind;
            Summary = summary;
            Detail = detail;
        }

        public override string ToString() =>
            Summary + (string.IsNullOrEmpty(Detail) ? "" : "  (" + Detail + ")");
    }

    /// <summary>
    /// What came back. Always returned — never null, and failures are values rather than
    /// exceptions, because for this kind of work failure is an ordinary outcome and modelling it
    /// as exceptional leads to try/catch in every caller.
    /// </summary>
    public sealed class Completion
    {
        public bool Ok => Failure == null;

        public string Text = "";
        public ModelFailure Failure;

        public int PromptTokens;
        public int CompletionTokens;
        public int ElapsedMs;

        /// <summary>Which client produced this, for the log and the diagnostics panel.</summary>
        public string Provider = "";

        public static Completion Success(string text, string provider, int promptTokens, int completionTokens, int elapsedMs)
        {
            return new Completion
            {
                Text = text ?? "",
                Provider = provider,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                ElapsedMs = elapsedMs
            };
        }

        public static Completion Failed(ModelFailure failure, string provider, int elapsedMs = 0)
        {
            return new Completion
            {
                Failure = failure,
                Provider = provider,
                ElapsedMs = elapsedMs
            };
        }
    }

    /// <summary>
    /// Anything that can turn messages into a reply.
    ///
    /// One interface, several implementations — including the mock, which is a real provider and
    /// not a test double. Per-provider branching inside one class is what makes these integrations
    /// rot, so providers never know about each other.
    /// </summary>
    public interface IModelClient
    {
        /// <summary>Shown in logs and the diagnostics panel.</summary>
        string Name { get; }

        /// <summary>
        /// True when this client has what it needs to run. False is a calm, explainable state —
        /// no key configured yet — not an error.
        /// </summary>
        bool Configured { get; }

        /// <summary>
        /// Sends messages and returns what came back. Never throws: transport and protocol
        /// problems come back as a <see cref="ModelFailure"/>.
        ///
        /// Called from a worker thread. Implementations must not touch game state.
        /// </summary>
        Completion Complete(IReadOnlyList<ChatMessage> messages, ModelOptions options);
    }
}
