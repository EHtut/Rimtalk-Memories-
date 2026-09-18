using System;
using System.Collections.Generic;
using RimTalk.API;
using RimTalkMemories.Util;
using Verse;

namespace RimTalkMemories.Integration
{
    /// <summary>
    /// Every call into RimTalk goes through here, so an upstream API change breaks in one place
    /// rather than scattered across the feature code.
    ///
    /// RimTalk exposes a real extension API (RimTalkPromptAPI), and this mod uses it in preference
    /// to Harmony wherever it reaches far enough. The companion mods this one replaces mostly
    /// patched RimTalk internals instead — Context Upgrade alone patches fourteen private methods
    /// — which is why they break on RimTalk updates. Anything we cannot do through the API is
    /// listed in docs/DESIGN.md under "Where Harmony is unavoidable", and nowhere else.
    ///
    /// This class also keeps the register of what we contributed, which the profile panel renders.
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

        private static readonly List<InjectionDeclaration> Declarations = new List<InjectionDeclaration>();

        private static readonly List<(string Name, string Description)> Variables = new List<(string, string)>();

        /// <summary>Everything this mod contributes, for the profile panel to render.</summary>
        public static IReadOnlyList<InjectionDeclaration> Registrations => Declarations;

        /// <summary>Template variables we registered, for the profile panel to list.</summary>
        public static IReadOnlyList<(string Name, string Description)> RegisteredVariables => Variables;

        /// <summary>Adds a declaration. Nothing reaches RimTalk until ApplyAll runs.</summary>
        public static void Declare(InjectionDeclaration declaration)
        {
            if (declaration == null) return;
            Declarations.Add(declaration);
        }

        /// <summary>
        /// Pushes every declaration into RimTalk, replacing whatever was registered before.
        ///
        /// Safe to call repeatedly, and it needs to be: changing a section's mode at runtime is
        /// just a re-apply. Registering twice without clearing would inject every section twice.
        /// </summary>
        public static void ApplyAll()
        {
            UnregisterAll();

            foreach (var declaration in Declarations)
            {
                declaration.ResetCounters();
                try
                {
                    Apply(declaration);
                }
                catch (Exception ex)
                {
                    RTMLog.Error("Could not register section " + declaration.SectionName + ": " + ex.Message);
                }
            }
        }

        private static void Apply(InjectionDeclaration d)
        {
            // Build the guarded delegate once. Creating it inside the hook lambda would allocate
            // on every prompt, and providers run on the tick path.
            if (d.IsPawnSection)
            {
                var provider = GuardedPawn(d);

                if (d.UsesHook)
                {
                    var operation = d.Mode == InjectionMode.HookOverride
                        ? ContextHookRegistry.HookOperation.Override
                        : ContextHookRegistry.HookOperation.Append;

                    RimTalkPromptAPI.RegisterPawnHook(ModId, d.Anchor, operation,
                        (pawn, original) => Fold(d, original, provider(pawn)), d.Priority);
                }
                else
                {
                    RimTalkPromptAPI.InjectPawnSection(ModId, d.SectionName, d.Anchor, PositionOf(d), provider, d.Priority);
                }
            }
            else
            {
                var provider = GuardedMap(d);

                if (d.UsesHook)
                {
                    var operation = d.Mode == InjectionMode.HookOverride
                        ? ContextHookRegistry.HookOperation.Override
                        : ContextHookRegistry.HookOperation.Append;

                    RimTalkPromptAPI.RegisterEnvironmentHook(ModId, d.Anchor, operation,
                        (map, original) => Fold(d, original, provider(map)), d.Priority);
                }
                else
                {
                    RimTalkPromptAPI.InjectEnvironmentSection(ModId, d.SectionName, d.Anchor, PositionOf(d), provider, d.Priority);
                }
            }

            RTMLog.Debug("registered " + d.SectionName + " as " + d.Mode + " on " + d.Anchor);
        }

        private static ContextHookRegistry.InjectPosition PositionOf(InjectionDeclaration d)
        {
            return d.Mode == InjectionMode.InjectBefore
                ? ContextHookRegistry.InjectPosition.Before
                : ContextHookRegistry.InjectPosition.After;
        }

        /// <summary>
        /// Combines our text with RimTalk's existing value for a hooked category.
        ///
        /// RimTalk does no concatenation of its own — "Append" names the order handlers run in,
        /// not a string operation, and a handler returns the complete new value. So the joining
        /// is ours to do. See docs/RIMTALK-API.md §1.2.
        ///
        /// Returning null from an Override handler is meaningful: RimTalk reads it as "I decline"
        /// and falls through to other mods' prepend and append hooks. Returning the original
        /// instead would win the override and silently suppress them.
        /// </summary>
        private static string Fold(InjectionDeclaration d, string original, string ours)
        {
            if (string.IsNullOrEmpty(ours))
            {
                return d.Mode == InjectionMode.HookOverride ? null : original;
            }

            if (d.Mode == InjectionMode.HookOverride) return ours;

            return string.IsNullOrEmpty(original) ? ours : original + " — " + ours;
        }

        /// <summary>
        /// Registers a template variable players can use in their own prompt presets, reachable
        /// as {{pawn.&lt;name&gt;}}.
        /// </summary>
        public static void RegisterPawnVariable(string variableName, Func<Pawn, string> provider, string description)
        {
            try
            {
                RimTalkPromptAPI.RegisterPawnVariable(ModId, variableName, Guard(provider, variableName), description);
                Variables.Add(("{{pawn." + variableName + "}}", description));
            }
            catch (Exception ex)
            {
                RTMLog.Error("Could not register pawn variable " + variableName + ": " + ex.Message);
            }
        }

        /// <summary>Registers a map-wide template variable, reachable as {{&lt;name&gt;}}.</summary>
        public static void RegisterEnvironmentVariable(string variableName, Func<Map, string> provider, string description)
        {
            try
            {
                RimTalkPromptAPI.RegisterEnvironmentVariable(ModId, variableName, Guard(provider, variableName), description);
                Variables.Add(("{{" + variableName + "}}", description));
            }
            catch (Exception ex)
            {
                RTMLog.Error("Could not register environment variable " + variableName + ": " + ex.Message);
            }
        }

        /// <summary>Drops every hook, injection and variable this mod registered.</summary>
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

        /// <summary>RimTalk's active preset, or null when it cannot be reached.</summary>
        public static RimTalk.Prompt.PromptPreset ActivePreset()
        {
            try
            {
                return RimTalk.Prompt.PromptManager.Instance?.GetActivePreset();
            }
            catch (Exception ex)
            {
                RTMLog.WarnOnce("Could not read RimTalk's active preset: " + ex.Message, 0x9A1);
                return null;
            }
        }

        // --- Provider wrapping ---------------------------------------------------------------

        /// <summary>
        /// Wraps a pawn provider so it counts its own invocations and a bug in it degrades one
        /// line of a prompt instead of killing the conversation.
        ///
        /// RimTalk calls injected providers with no net of its own, so an exception escaping here
        /// would take down the pawn's talk request. Returning empty means the section is skipped.
        /// </summary>
        private static Func<Pawn, string> GuardedPawn(InjectionDeclaration d)
        {
            return pawn =>
            {
                d.Calls++;
                try
                {
                    string text = d.PawnProvider(pawn) ?? "";
                    Record(d, text);
                    return text;
                }
                catch (Exception ex)
                {
                    RTMLog.WarnOnce("Section " + d.SectionName + " threw and was skipped: " + ex, d.SectionName.GetHashCode());
                    return "";
                }
            };
        }

        private static Func<Map, string> GuardedMap(InjectionDeclaration d)
        {
            return map =>
            {
                d.Calls++;
                try
                {
                    string text = d.MapProvider(map) ?? "";
                    Record(d, text);
                    return text;
                }
                catch (Exception ex)
                {
                    RTMLog.WarnOnce("Section " + d.SectionName + " threw and was skipped: " + ex, d.SectionName.GetHashCode());
                    return "";
                }
            };
        }

        private static void Record(InjectionDeclaration d, string text)
        {
            if (text.Length == 0) return;

            d.NonEmptyReturns++;
            d.LastEmitted = text;
            d.LastEmittedTick = Current.ProgramState == ProgramState.Playing && Find.TickManager != null
                ? Find.TickManager.TicksGame
                : 0;
        }

        /// <summary>Same protection for variable providers, which have no declaration to count into.</summary>
        private static Func<T, string> Guard<T>(Func<T, string> provider, string label)
        {
            return arg =>
            {
                try
                {
                    return provider(arg) ?? "";
                }
                catch (Exception ex)
                {
                    RTMLog.WarnOnce("Variable " + label + " threw and was skipped: " + ex, label.GetHashCode());
                    return "";
                }
            };
        }
    }
}
