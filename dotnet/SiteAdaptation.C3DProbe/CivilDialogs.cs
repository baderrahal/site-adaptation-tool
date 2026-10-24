using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SiteAdaptation.C3DProbe
{
    public enum DialogAction
    {
        LoadOnce,
        DoNotLoad,
        Safe,
        Leave,
    }

    public sealed class DialogAnswer
    {
        public DialogAction Action { get; set; }
        public string Button { get; set; }
        public string Why { get; set; }
    }

    // R161. What the watcher does with a dialog of the Civil 3D window it started (Bader, round DATA). Load Once only on the
    // security dialog that names the probe DLL, Do Not Load on every other security dialog, and on any other dialog the
    // safe button: never Yes, Save, Always Load or anything that keeps an answer
    public static class CivilDialogs
    {
        public const string LoadOnceButton = "Load Once";
        public const string DoNotLoadButton = "Do Not Load";

        // In this order, the first one the dialog holds is clicked. Each one leaves the drawing and the settings as they were
        private static readonly string[] SafeButtons = { "Do Not Load", "Don't Save", "Do not save", "No", "Ignore", "Continue", "Close", "Cancel", "OK" };

        private static readonly string[] Never = { "Yes", "Save", "Always Load", "Always", "Trust", "Add" };

        public static string Plain(string label)
        {
            return (label ?? "").Replace("&", "").Trim();
        }

        public static bool IsSecurity(string title, IEnumerable<string> texts)
        {
            string all = (title ?? "") + "\n" + string.Join("\n", texts ?? Enumerable.Empty<string>());
            return all.IndexOf("Security", StringComparison.OrdinalIgnoreCase) >= 0
                   || all.IndexOf("trusted location", StringComparison.OrdinalIgnoreCase) >= 0
                   || all.IndexOf("Unsigned Executable", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // The dialog must show the full path of the probe, a file name alone could be another DLL of the same name
        public static bool NamesProbe(IEnumerable<string> texts, string probePath)
        {
            if (string.IsNullOrWhiteSpace(probePath))
            {
                return false;
            }
            string wanted = Normal(probePath);
            return (texts ?? Enumerable.Empty<string>()).Any(text => Normal(text).IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static DialogAnswer Decide(string title, IList<string> texts, IList<string> buttons, string probePath)
        {
            var plain = (buttons ?? new List<string>()).Select(Plain).ToList();
            if (IsSecurity(title, texts))
            {
                if (NamesProbe(texts, probePath) && Has(plain, LoadOnceButton) != null)
                {
                    return new DialogAnswer { Action = DialogAction.LoadOnce, Button = Has(plain, LoadOnceButton), Why = "the security dialog names the probe DLL" };
                }
                string refuse = Has(plain, DoNotLoadButton);
                return new DialogAnswer
                {
                    Action = refuse != null ? DialogAction.DoNotLoad : DialogAction.Leave,
                    Button = refuse,
                    Why = refuse != null ? "a security dialog for another file" : "a security dialog with no Do Not Load button",
                };
            }
            foreach (string safe in SafeButtons)
            {
                string found = Has(plain, safe);
                if (found != null && !Never.Any(word => string.Equals(found, word, StringComparison.OrdinalIgnoreCase)))
                {
                    return new DialogAnswer { Action = DialogAction.Safe, Button = found, Why = "the safe button of another dialog" };
                }
            }
            return new DialogAnswer { Action = DialogAction.Leave, Why = "no safe button" };
        }

        // A button matches when its label is the word or starts with it and a space, so No does not match None
        private static string Has(IList<string> buttons, string wanted)
        {
            return buttons.FirstOrDefault(button => string.Equals(button, wanted, StringComparison.OrdinalIgnoreCase)
                                                    || button.StartsWith(wanted + " ", StringComparison.OrdinalIgnoreCase));
        }

        private static string Normal(string text)
        {
            return (text ?? "").Replace('/', Path.DirectorySeparatorChar).Replace("\r", " ").Replace("\n", " ");
        }
    }
}
