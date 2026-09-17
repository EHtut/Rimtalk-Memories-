using System;
using RimTalk.API;
using RimTalkMemories.Util;
using Verse;

namespace RimTalkMemories.Integration
{
    /// <summary>
    /// Every call into RimTalk goes through here, so an upstream API change breaks in one
    /// place rather than scattered across the feature code.
    ///
    /// RimTalk exposes a real extension API (RimTalkPromptAPI), and this mod uses it in
    /// preference to Harmony wherever it reaches far enough. The companion mods this one
    /// replaces mostly patched RimTalk internals instead — Context Upgrade alone patches
    /// fourteen private methods — which is why they break on RimTalk updates. Anything we
    /// cannot do through the API is listed in docs/DESIGN.md under "Where Harmony is
    /// unavoidable", and nowhere else.
    /// </summary>
    public static class RimTalkApi
    {
        /// <summary>
        /// Identifies everything this mod registers, so UnregisterAll can take it all back.
        ///
        /// RimTalk lowercases this and strips non-alphanumerics before storing it, so it is
        /// written here already in that form. If it were not, registration and removal would
        /// disagree about the name and removal would silently match nothing.
        /// </summary>
        public const string ModId = "rimtalkmemories";

        /// <summary>
        /// Adds a block of text to a pawn's context, anchored next to a section RimTalk
        /// already builds.
        ///
        /// The provider returns one ready-to-read line, or several separated by newlines.
        /// RimTalk appends it verbatim and skips it entirely when it is null or empty, so the
        /// provider owns its own label and returns empty to say nothing at all.
        ///
        /// It runs on the main thread while the prompt is assembled, so game state is safe to
        /// touch — but it is also on the tick budget. Keep it to lookups and cached values.
        /// </summary>
        public static void InjectPawnSection(
            string sectionName,
            ContextCategory anchor,
            ContextHookRegistry.InjectPosition position,
            Func<Pawn, string> provider,
            int priority = 100)
        {
            try
            {
                RimTalkPromptAPI.InjectPawnSection(ModId, sectionName, anchor, position, Guarded(provider, sectionName), priority);
                RTMLog.Debug("injected pawn section " + sectionName + " " + position + " " + anchor);
            }
            catch (Exception ex)
            {
                RTMLog.Error("Could not inject pawn section " + sectionName + ": " + ex.Message);
            }
        }

        /// <summary>Same as InjectPawnSection, but for map-wide context such as world lore.</summary>
        public static void InjectEnvironmentSection(
            string sectionName,
            ContextCategory anchor,
            ContextHookRegistry.InjectPosition position,
            Func<Map, string> provider,
            int priority = 100)
        {
            try
            {
                RimTalkPromptAPI.InjectEnvironmentSection(ModId, sectionName, anchor, position, Guarded(provider, sectionName), priority);
                RTMLog.Debug("injected environment section " + sectionName + " " + position + " " + anchor);
            }
            catch (Exception ex)
            {
                RTMLog.Error("Could not inject environment section " + sectionName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Registers a template variable the player can use in their own prompt presets,
        /// reachable as {{pawn.&lt;name&gt;}}. Same threading and cost rules as the section
        /// providers.
        /// </summary>
        public static void RegisterPawnVariable(string variableName, Func<Pawn, string> provider, string description)
        {
            try
            {
                RimTalkPromptAPI.RegisterPawnVariable(ModId, variableName, Guarded(provider, variableName), description);
                RTMLog.Debug("registered pawn variable " + variableName);
            }
            catch (Exception ex)
            {
                RTMLog.Error("Could not register pawn variable " + variableName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Drops every hook, injection and variable this mod registered.
        ///
        /// Worth calling before registering as well as after: a static constructor can run
        /// more than once in a session, and RimTalk's registry would otherwise end up holding
        /// two copies of every section.
        /// </summary>
        public static void UnregisterAll()
        {
            try
            {
                RimTalkPromptAPI.UnregisterAllHooks(ModId);
            }
            catch (Exception ex)
            {
                RTMLog.Warning("Could not unregister hooks: " + ex.Message);
            }
        }

        /// <summary>
        /// Wraps a provider so a bug in it degrades one line of a prompt instead of killing
        /// the whole conversation.
        ///
        /// RimTalk calls these from inside prompt assembly without a net of its own, so an
        /// exception escaping here would take down the pawn's talk request. Returning empty
        /// means the section is skipped, which is a far better failure.
        /// </summary>
        private static Func<T, string> Guarded<T>(Func<T, string> provider, string label)
        {
            return arg =>
            {
                try
                {
                    return provider(arg) ?? "";
                }
                catch (Exception ex)
                {
                    RTMLog.WarnOnce("Context provider " + label + " threw and was skipped: " + ex, label.GetHashCode());
                    return "";
                }
            };
        }
    }
}
