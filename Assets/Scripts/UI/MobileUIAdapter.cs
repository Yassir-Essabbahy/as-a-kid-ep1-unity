using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Story
{
    /// <summary>
    /// Utility and runtime component for adapting UI prompts and interactions between PC (Keyboard/Mouse)
    /// and Mobile (Touchscreen). Automatically translates prompts like "Press 'E' to Talk" -> "Tap to Talk".
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileUIAdapter : MonoBehaviour
    {
        public static bool ForceMobileInEditor = true;

        public static bool IsMobile
        {
            get
            {
#if UNITY_EDITOR
                return ForceMobileInEditor || Application.isMobilePlatform;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        [Tooltip("If true, automatically adapts all child TMP_Text components on start and enable.")]
        public bool adaptChildTexts = true;

        private void Start()
        {
            if (adaptChildTexts && IsMobile)
            {
                AdaptAllChildTexts();
            }
        }

        private void OnEnable()
        {
            if (adaptChildTexts && IsMobile)
            {
                AdaptAllChildTexts();
            }
        }

        public void AdaptAllChildTexts()
        {
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmps)
            {
                if (t != null && !string.IsNullOrEmpty(t.text))
                {
                    string adapted = AdaptPrompt(t.text);
                    if (adapted != t.text)
                    {
                        t.text = adapted;
                    }
                }
            }
        }

        /// <summary>
        /// Converts keyboard/mouse prompt strings to mobile touch equivalents.
        /// Handles English, French, and Arabic string variants.
        /// </summary>
        public static string AdaptPrompt(string original)
        {
            if (string.IsNullOrEmpty(original)) return original;
            if (!IsMobile) return original;

            string s = original;

            // English adaptations
            s = Regex.Replace(s, @"Press\s+['""]?E['""]?\s+To\s+", "Tap To ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Press\s+['""]?E['""]?\s+to\s+", "Tap to ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Press\s+['""]?Space['""]?\s+To\s+", "Tap To ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Press\s+['""]?Space['""]?\s+to\s+", "Tap to ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\[\s*E\s*\]\s*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\[\s*1\s*\]\s*", "");
            s = Regex.Replace(s, @"\[\s*2\s*\]\s*", "");
            s = Regex.Replace(s, @"\[\s*3\s*\]\s*", "");
            s = Regex.Replace(s, @"\[\s*R\s*\]", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\[\s*PRESS ANY KEY TO RETURN TO MAIN MENU\s*\]", "[ TAP ANYWHERE TO RETURN TO MAIN MENU ]", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Use your mouse cursor to write and draw anywhere on this paper", "Touch and drag with your finger to write and draw anywhere on this paper", RegexOptions.IgnoreCase);

            // French adaptations
            s = Regex.Replace(s, @"Appuie sur ['""]?E['""]? pour ", "Appuyez pour ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"Appuyez sur ['""]?E['""]? pour ", "Appuyez pour ", RegexOptions.IgnoreCase);

            // Arabic adaptations
            s = s.Replace("اضغط 'E' لـ", "المس لـ");
            s = s.Replace("اضغط E لـ", "المس لـ");
            s = s.Replace("اضغط 'E' لـ", "المس لـ");
            s = s.Replace("اضغط على E", "المس");

            return s.Trim();
        }
    }
}
