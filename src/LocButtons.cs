using System;
using System.Collections.Generic;
using TMPro;
using SlickUi;
using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Keeps the PERSISTENT mod buttons (Import Part/Folder, Random, Shoe, Custom-only) in the game's
    /// language even when it changes AT RUNTIME. Those buttons are created once and cached (the creator
    /// reopening does not rebuild them), so the initial <see cref="Loc"/> translation in the UI funnel
    /// would stick to whatever language was active at creation. Here each button registers a "refresher";
    /// we subscribe once to the engine's <c>Translator.onLanguageUpdated</c> and re-apply the translation
    /// to every live button whenever the language changes. Transient panels rebuild themselves, so they
    /// don't need this.
    /// </summary>
    internal static class LocButtons
    {
        private static readonly List<Action> Refreshers = new List<Action>();
        private static bool _subscribed;

        /// <summary>Register a fixed-label button; its text is re-set to <c>Loc.T(pt)</c> on language change.</summary>
        internal static void Register(UiButton btn, string pt)
        {
            if (btn == null) return;
            Register(() =>
            {
                if (btn == null) return;
                var t = btn.GetComponentInChildren<TMP_Text>(true);
                if (t != null) t.text = Loc.T(pt);
            });
        }

        /// <summary>Register a custom refresher (for buttons whose label depends on state, e.g. a toggle).</summary>
        internal static void Register(Action refresh)
        {
            if (refresh == null) return;
            Refreshers.Add(refresh);
            try { refresh(); } catch { }
            EnsureSubscribed();
        }

        internal static void RelocalizeAll()
        {
            foreach (var r in Refreshers)
                try { r(); } catch { }
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            try
            {
                var t = UnityUtils.UniqueSingleton<UnityUtils.Translator>.instance;
                if (t != null && t.onLanguageUpdated != null)
                {
                    t.onLanguageUpdated.AddListener(_ => RelocalizeAll());
                    _subscribed = true;
                }
                Plugin.Log.LogInfo($"[loc] Language={SafeLang()} IsEnglish={Loc.IsEnglish} subscribed={_subscribed}");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[loc] subscribe: " + e.Message); }
        }

        private static string SafeLang()
        {
            try { return PlayerPrefs.GetString("Language", "?"); } catch { return "?"; }
        }
    }
}
