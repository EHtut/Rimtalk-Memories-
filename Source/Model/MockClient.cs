using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Arkh.Model
{
    /// <summary>
    /// A provider that answers without a network, a key or a bill.
    ///
    /// This is a real provider, not a test double, and it is deliberately the first one built.
    /// Everything downstream — the talk engine, the display layer, the whole memory phase — needs
    /// *something* that returns plausible text in order to be developed at all. Waiting on a real
    /// provider would mean building those against nothing, and paying per iteration once it
    /// existed.
    ///
    /// It also lets the headless harness exercise the entire pipeline, which matters when the
    /// alternative is a twenty-minute game load per question.
    ///
    /// The failure rate is not decoration. Failure is the common case in this kind of integration
    /// (see <see cref="FailureKind"/>), and code that has only ever seen success handles it badly.
    /// Turning the dial up is how we find out whether a colonist falling silent looks like a bug.
    /// </summary>
    public sealed class MockClient : IModelClient
    {
        private static readonly string[] Lines =
        {
            "Reckon the weather's turning. Again.",
            "Someone left the freezer door open. I'm not saying who.",
            "I keep thinking about the raid. Don't much want to talk about it.",
            "Third time this week we've eaten the same thing.",
            "If that wall falls over one more time I'm sleeping outside.",
            "You ever wonder what's out past the ridge?",
            "Slept badly. Kept hearing something in the dark.",
            "Good to see you upright, after all that."
        };

        private readonly Random _random;

        /// <summary>Simulated round-trip latency, so callers meet the real timing shape.</summary>
        public int DelayMs = 0;

        /// <summary>0 to 1. The share of requests that come back as a failure.</summary>
        public float FailureRate = 0f;

        /// <summary>Which failure to produce when one is rolled.</summary>
        public FailureKind FailAs = FailureKind.RateLimited;

        public string Name => "Mock";

        public bool Configured => true;

        /// <summary>
        /// Seeded so a harness run is reproducible. A flaky test of failure handling is worse than
        /// no test, because it trains people to re-run rather than to read.
        /// </summary>
        public MockClient(int seed = 1337)
        {
            _random = new Random(seed);
        }

        public Completion Complete(IReadOnlyList<ChatMessage> messages, ModelOptions options)
        {
            var clock = Stopwatch.StartNew();

            if (DelayMs > 0) Thread.Sleep(DelayMs);

            if (FailureRate > 0f && _random.NextDouble() < FailureRate)
            {
                clock.Stop();
                return Completion.Failed(Failure(FailAs), Name, (int)clock.ElapsedMilliseconds);
            }

            string text = Lines[_random.Next(Lines.Length)];

            // Honour the caller's length cap the way a real provider would, so a caller that
            // mishandles truncation fails here rather than in production.
            int approxCharCap = Math.Max(1, options?.MaxTokens ?? 300) * 4;
            if (text.Length > approxCharCap) text = text.Substring(0, approxCharCap);

            clock.Stop();

            int promptChars = 0;
            if (messages != null)
            {
                foreach (var m in messages) promptChars += m.Content?.Length ?? 0;
            }

            return Completion.Success(text, Name, promptChars / 4, text.Length / 4, (int)clock.ElapsedMilliseconds);
        }

        private static ModelFailure Failure(FailureKind kind)
        {
            switch (kind)
            {
                case FailureKind.RateLimited:
                    return new ModelFailure(kind, "Mock provider: simulated rate limit.");
                case FailureKind.Timeout:
                    return new ModelFailure(kind, "Mock provider: simulated timeout.");
                case FailureKind.BadKey:
                    return new ModelFailure(kind, "Mock provider: simulated authentication failure.");
                default:
                    return new ModelFailure(kind, "Mock provider: simulated failure (" + kind + ").");
            }
        }
    }
}
