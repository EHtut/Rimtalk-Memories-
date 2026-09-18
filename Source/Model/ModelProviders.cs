using System;
using Arkh.Settings;

namespace Arkh.Model
{
    /// <summary>
    /// Providers Arkh can talk to today.
    ///
    /// Gemini and Player2 are on the list but not in this enum yet, deliberately: offering a
    /// provider in the settings that cannot actually answer is worse than not offering it. They
    /// arrive as enum members when their clients do.
    /// </summary>
    public enum ModelProvider
    {
        Mock,
        OpenAI,
        OpenRouter,
        DeepSeek,
        Ollama,
        LMStudio,
        Custom
    }

    public sealed class ProviderInfo
    {
        public string DisplayName;
        public string BaseUrl;
        public string DefaultModel;
        public bool KeyRequired;
        public string Note;
    }

    public static class ModelProviders
    {
        public static ProviderInfo For(ModelProvider provider)
        {
            switch (provider)
            {
                case ModelProvider.OpenAI:
                    return new ProviderInfo
                    {
                        DisplayName = "OpenAI",
                        BaseUrl = "https://api.openai.com/v1",
                        DefaultModel = "gpt-4o-mini",
                        KeyRequired = true
                    };

                case ModelProvider.OpenRouter:
                    return new ProviderInfo
                    {
                        DisplayName = "OpenRouter",
                        BaseUrl = "https://openrouter.ai/api/v1",
                        DefaultModel = "openai/gpt-4o-mini",
                        KeyRequired = true,
                        Note = "One key, many models. Model names carry a vendor prefix."
                    };

                case ModelProvider.DeepSeek:
                    return new ProviderInfo
                    {
                        DisplayName = "DeepSeek",
                        BaseUrl = "https://api.deepseek.com/v1",
                        DefaultModel = "deepseek-chat",
                        KeyRequired = true
                    };

                case ModelProvider.Ollama:
                    return new ProviderInfo
                    {
                        DisplayName = "Ollama",
                        BaseUrl = "http://localhost:11434/v1",
                        DefaultModel = "llama3.1",
                        KeyRequired = false,
                        Note = "Runs on your machine. No key, no cost — but Ollama must be running, "
                               + "and the model pulled, before colonists can speak."
                    };

                case ModelProvider.LMStudio:
                    return new ProviderInfo
                    {
                        DisplayName = "LM Studio",
                        BaseUrl = "http://localhost:1234/v1",
                        DefaultModel = "local-model",
                        KeyRequired = false,
                        Note = "Runs on your machine. Start its local server first; the model name "
                               + "is whatever LM Studio shows."
                    };

                case ModelProvider.Custom:
                    return new ProviderInfo
                    {
                        DisplayName = "Custom (OpenAI-compatible)",
                        BaseUrl = "",
                        DefaultModel = "",
                        KeyRequired = false,
                        Note = "Any endpoint speaking OpenAI's /chat/completions shape. Give the "
                               + "base URL up to and including /v1."
                    };

                default:
                    return new ProviderInfo
                    {
                        DisplayName = "Mock (no network)",
                        BaseUrl = "",
                        DefaultModel = "",
                        KeyRequired = false,
                        Note = "Canned replies, no key and no cost. Use it to see the mod working, "
                               + "or to test how the colony behaves when requests fail."
                    };
            }
        }

        /// <summary>
        /// Builds the client the settings currently describe.
        ///
        /// Cheap enough to call per request, so there is no cached instance to invalidate when the
        /// player changes provider mid-game — a whole class of "why is it still using my old key"
        /// bug that is not worth the allocation it saves.
        /// </summary>
        public static IModelClient Create(ArkhSettings settings)
        {
            if (settings == null) return new MockClient();

            var provider = settings.Provider;
            var info = For(provider);

            if (provider == ModelProvider.Mock)
            {
                return new MockClient
                {
                    DelayMs = settings.MockDelayMs,
                    FailureRate = settings.MockFailureRate
                };
            }

            string baseUrl = string.IsNullOrEmpty(settings.BaseUrlOverride)
                ? info.BaseUrl
                : settings.BaseUrlOverride;

            return new OpenAiCompatibleClient(info.DisplayName, baseUrl, settings.ApiKey, info.KeyRequired);
        }

        /// <summary>The options a request should use, from settings plus the provider's defaults.</summary>
        public static ModelOptions OptionsFrom(ArkhSettings settings)
        {
            if (settings == null) return new ModelOptions();

            var info = For(settings.Provider);
            return new ModelOptions
            {
                Model = string.IsNullOrEmpty(settings.ModelName) ? info.DefaultModel : settings.ModelName,
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxResponseTokens,
                TimeoutSeconds = settings.TimeoutSeconds
            };
        }

        public static ModelProvider[] All =>
            (ModelProvider[])Enum.GetValues(typeof(ModelProvider));
    }
}
