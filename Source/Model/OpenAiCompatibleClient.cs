using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Arkh.Util;

namespace Arkh.Model
{
    /// <summary>
    /// Talks to any endpoint speaking OpenAI's <c>/chat/completions</c> shape.
    ///
    /// One implementation covers OpenAI, OpenRouter, DeepSeek, Together, LM Studio and Ollama's
    /// compatibility endpoint — six of the seven providers on the list, which is why it is the
    /// first real one built. Only the base URL, the key and the model name differ.
    ///
    /// Written against the published API shape, synchronous, using <see cref="HttpWebRequest"/>
    /// rather than <c>HttpClient</c> or <c>UnityWebRequest</c>. That is a deliberate trade: it runs
    /// on a plain worker thread with no Unity coupling and no async machinery, which means the
    /// headless harness can drive it exactly as the game does. A Unity-bound transport would be
    /// testable only inside a twenty-minute game load.
    /// </summary>
    public sealed class OpenAiCompatibleClient : IModelClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly bool _keyRequired;

        public string Name { get; }

        /// <summary>
        /// Local servers (Ollama, LM Studio) take any key or none, so "configured" means different
        /// things per provider. Treating a missing key as a hard error for those would block the
        /// easiest setup a player can have.
        /// </summary>
        public bool Configured =>
            !string.IsNullOrEmpty(_baseUrl) && (!_keyRequired || !string.IsNullOrEmpty(_apiKey));

        public OpenAiCompatibleClient(string name, string baseUrl, string apiKey, bool keyRequired = true)
        {
            Name = name;
            // TrimEnd with a single char binds to TrimEnd(char) — a .NET Core 2.0 overload absent
            // from the runtime the game uses. Passing an array forces the params char[] overload,
            // which has always existed. Two or more chars would bind correctly on their own; it is
            // only the one-argument form that is a trap. See TextUtil.Clamp for the first case.
            _baseUrl = (baseUrl ?? "").TrimEnd(new[] { '/' });
            _apiKey = apiKey;
            _keyRequired = keyRequired;
        }

        public Completion Complete(IReadOnlyList<ChatMessage> messages, ModelOptions options)
        {
            if (!Configured)
            {
                return Completion.Failed(new ModelFailure(FailureKind.NotConfigured,
                    "No API key set for " + Name + ". Add one in the mod settings."), Name);
            }

            options = options ?? new ModelOptions();
            var clock = Stopwatch.StartNew();

            try
            {
                string body = BuildRequestBody(messages, options);
                string response = Post(_baseUrl + "/chat/completions", body, options.TimeoutSeconds);
                clock.Stop();
                return ReadResponse(response, (int)clock.ElapsedMilliseconds);
            }
            catch (WebException ex)
            {
                clock.Stop();
                return Completion.Failed(Classify(ex), Name, (int)clock.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                clock.Stop();
                return Completion.Failed(new ModelFailure(FailureKind.Unknown,
                    Name + " request failed unexpectedly.", ex.Message), Name, (int)clock.ElapsedMilliseconds);
            }
        }

        // --- Request --------------------------------------------------------------------------

        internal string BuildRequestBody(IReadOnlyList<ChatMessage> messages, ModelOptions options)
        {
            var array = new StringBuilder("[");
            if (messages != null)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    if (i > 0) array.Append(',');
                    array.Append("{\"role\":").Append(Json.Quote(messages[i].RoleName))
                         .Append(",\"content\":").Append(Json.Quote(messages[i].Content ?? ""))
                         .Append('}');
                }
            }
            array.Append(']');

            return new Json.Writer()
                .Str("model", options.Model)
                .Raw("messages", array.ToString())
                .Num("temperature", options.Temperature)
                .Num("max_tokens", options.MaxTokens)
                .Bool("stream", false)
                .Done();
        }

        private string Post(string url, string body, int timeoutSeconds)
        {
            // Mono's default can exclude TLS 1.2, and every one of these endpoints requires it.
            // Setting it per request is cheap and avoids depending on startup ordering.
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // Older runtimes may not know the value. Nothing useful to do; the request below
                // will fail with a clearer message than anything we could invent here.
            }

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = Math.Max(1, timeoutSeconds) * 1000;
            request.ReadWriteTimeout = request.Timeout;

            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers["Authorization"] = "Bearer " + _apiKey;
            }

            byte[] payload = Encoding.UTF8.GetBytes(body);
            request.ContentLength = payload.Length;
            using (var stream = request.GetRequestStream())
            {
                stream.Write(payload, 0, payload.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        // --- Response -------------------------------------------------------------------------

        internal Completion ReadResponse(string json, int elapsedMs)
        {
            var root = Json.Parse(json);

            // Some providers answer 200 with an error object rather than an HTTP error status.
            var error = root["error"];
            if (error.Exists)
            {
                string message = error["message"].AsString("(no message)");
                return Completion.Failed(new ModelFailure(ClassifyErrorText(message),
                    Name + " returned an error.", message), Name, elapsedMs);
            }

            var content = root["choices"][0]["message"]["content"];
            if (!content.Exists)
            {
                return Completion.Failed(new ModelFailure(FailureKind.Malformed,
                    Name + " sent a reply in an unexpected shape.",
                    Truncate(json, 300)), Name, elapsedMs);
            }

            var usage = root["usage"];
            return Completion.Success(
                content.AsString().Trim(),
                Name,
                usage["prompt_tokens"].AsInt(),
                usage["completion_tokens"].AsInt(),
                elapsedMs);
        }

        private ModelFailure Classify(WebException ex)
        {
            if (ex.Status == WebExceptionStatus.Timeout)
            {
                return new ModelFailure(FailureKind.Timeout,
                    Name + " did not answer in time.", ex.Message);
            }

            if (ex.Status == WebExceptionStatus.ConnectFailure ||
                ex.Status == WebExceptionStatus.NameResolutionFailure ||
                ex.Status == WebExceptionStatus.ProxyNameResolutionFailure)
            {
                return new ModelFailure(FailureKind.Unreachable,
                    "Could not reach " + Name + ". If it is a local server, check that it is running.",
                    ex.Message);
            }

            var response = ex.Response as HttpWebResponse;
            if (response == null)
            {
                return new ModelFailure(FailureKind.Unknown, Name + " could not be reached.", ex.Message);
            }

            string body = ReadBodyQuietly(response);
            int status = (int)response.StatusCode;

            switch (status)
            {
                case 401:
                    return new ModelFailure(FailureKind.BadKey,
                        Name + " rejected the API key. Check it in the mod settings.", body);
                case 403:
                    return new ModelFailure(FailureKind.Refused,
                        Name + " refused the request — the key may lack access to this model.", body);
                case 404:
                    return new ModelFailure(FailureKind.NotConfigured,
                        Name + " has no such model or endpoint. Check the model name and base URL.", body);
                case 429:
                    return new ModelFailure(ClassifyErrorText(body),
                        Name + " is rate limiting, or the quota is spent.", body);
                default:
                    if (status >= 500)
                    {
                        return new ModelFailure(FailureKind.Unreachable,
                            Name + " is having trouble (HTTP " + status + ").", body);
                    }
                    return new ModelFailure(FailureKind.Unknown,
                        Name + " returned HTTP " + status + ".", body);
            }
        }

        /// <summary>
        /// Rate limit and spent quota both arrive as 429 but mean opposite things to the player:
        /// one clears by waiting, the other never does. The only signal is the message text.
        /// </summary>
        private static FailureKind ClassifyErrorText(string text)
        {
            if (string.IsNullOrEmpty(text)) return FailureKind.RateLimited;

            string lower = text.ToLowerInvariant();
            if (lower.Contains("insufficient_quota") || lower.Contains("quota") ||
                lower.Contains("billing") || lower.Contains("credit"))
            {
                return FailureKind.QuotaExhausted;
            }
            if (lower.Contains("rate") || lower.Contains("too many")) return FailureKind.RateLimited;
            if (lower.Contains("api key") || lower.Contains("unauthorized")) return FailureKind.BadKey;

            return FailureKind.RateLimited;
        }

        private static string ReadBodyQuietly(HttpWebResponse response)
        {
            try
            {
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                {
                    return Truncate(reader.ReadToEnd(), 500);
                }
            }
            catch
            {
                return "";
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
