using System;
using UnityEngine;
using UnityEngine.Events;

namespace AsAKid.Conversation
{
    /// <summary>
    /// Holds 3 localized question/answer pairs for an NPC, remembers which questions
    /// were already asked during this session, and exposes both Inspector UnityEvents
    /// and static C# hooks for future systems (truth/trust scoring, scares, train arrival).
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcQuestionSet : MonoBehaviour
    {
        public const int QuestionCount = 3;

        [Serializable]
        public struct QuestionEntry
        {
            [Tooltip("Localization key in dialogue.csv for this question button (e.g. npc_neighbor_q1).")]
            public string questionKey;

            [Tooltip("Localization key in dialogue.csv for the NPC's reply (e.g. npc_neighbor_a1).")]
            public string answerKey;
        }

        [Header("NPC Identity & Localization")]
        [Tooltip("Unique identifier for this NPC, used in events and default localization key lookups (e.g. neighbor).")]
        [SerializeField] private string npcId = "neighbor";

        [Tooltip("Optional localization key for the NPC's name header. If empty, tries npc_<npcId>_name.")]
        [SerializeField] private string npcNameKey = "";

        [Tooltip("Optional localization key for a short opening greeting before showing the 3 questions.")]
        [SerializeField] private string greetingKey = "";

        [Tooltip("Optional localization key for a short line after all 3 questions are finished. Defaults to npc_<npcId>_done.")]
        [SerializeField] private string doneKey = "";

        [Tooltip("Optional localization key for Doudou's greeting-time whisper. Defaults to doudou_<npcId>_whisper.")]
        [SerializeField] private string doudouWhisperKey = "";

        [Tooltip("Optional localization key for Doudou's reaction after Q3 is answered. Defaults to doudou_<npcId>_reaction.")]
        [SerializeField] private string doudouReactionKey = "";

        [Tooltip("If enabled, this NPC is counted in ConversationProgress.")]
        [SerializeField] private bool includeInProgress = true;

        [Header("3 Questions & Answers (Exact Count = 3)")]
        [SerializeField] private QuestionEntry[] questions = new QuestionEntry[QuestionCount];

        [Header("Events")]
        [Tooltip("Invoked with the question index (0, 1, or 2) when a question is answered.")]
        public UnityEvent<int> OnQuestionAnswered = new UnityEvent<int>();

        [Tooltip("Invoked once when all 3 questions for this NPC have been answered.")]
        public UnityEvent OnAllQuestionsAnswered = new UnityEvent();

        /// <summary>
        /// Static C# hook fired whenever any NPC answers a question: (npcId, questionIndex).
        /// </summary>
        public static event Action<string, int> OnAnyQuestionAnswered;

        /// <summary>
        /// Static C# hook fired when any NPC has had all 3 questions answered: (npcId).
        /// </summary>
        public static event Action<string> OnAnyNpcCompleted;

        /// <summary>
        /// Static C# hook fired when a conversation starts with an NPC: (npcId).
        /// </summary>
        public static event Action<string> OnAnyConversationStarted;

        /// <summary>
        /// Static C# hook fired when a conversation ends with an NPC: (npcId).
        /// </summary>
        public static event Action<string> OnAnyConversationEnded;

        private readonly bool[] _asked = new bool[QuestionCount];
        private bool _allAnsweredFired = false;
        private bool _doudouWhisperPlayed = false;
        private bool _doudouReactionPlayed = false;

        public bool HasPlayedDoudouWhisper => _doudouWhisperPlayed;
        public bool HasPlayedDoudouReaction => _doudouReactionPlayed;

        public string NpcId => string.IsNullOrWhiteSpace(npcId) ? gameObject.name : npcId.Trim();
        public bool IncludeInProgress => includeInProgress;

        public int AskedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < QuestionCount; i++)
                {
                    if (_asked[i])
                        count++;
                }
                return count;
            }
        }

        public bool AreAllAsked => AskedCount >= QuestionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnAnyQuestionAnswered = null;
            OnAnyNpcCompleted = null;
            OnAnyConversationStarted = null;
            OnAnyConversationEnded = null;
        }

        private void Awake()
        {
            EnsureQuestionArraySize();
        }

        private void OnEnable()
        {
            EnsureQuestionArraySize();
            ConversationProgress.Register(this);
        }

        private void OnDisable()
        {
            ConversationProgress.Unregister(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureQuestionArraySize();
        }
#endif

        /// <summary>
        /// Ensures the serialized array always has exactly 3 question slots in the Inspector and at runtime.
        /// </summary>
        public void EnsureQuestionArraySize()
        {
            if (questions == null)
            {
                questions = new QuestionEntry[QuestionCount];
                return;
            }

            if (questions.Length != QuestionCount)
            {
                var resized = new QuestionEntry[QuestionCount];
                int copyLen = Mathf.Min(questions.Length, QuestionCount);
                for (int i = 0; i < copyLen; i++)
                {
                    resized[i] = questions[i];
                }
                questions = resized;
            }
        }

        public bool HasAsked(int index)
        {
            if (index < 0 || index >= QuestionCount)
                return false;
            return _asked[index];
        }

        public bool HasGreeting()
        {
            return !string.IsNullOrWhiteSpace(greetingKey);
        }

        private string ResolveOptionalKey(string explicitKey, string defaultSuffix)
        {
            string key = !string.IsNullOrWhiteSpace(explicitKey)
                ? explicitKey.Trim()
                : $"npc_{NpcId.ToLowerInvariant()}_{defaultSuffix}";

            return LocalizationManager.TryResolve(key, out string resolved) ? resolved : string.Empty;
        }

        private string ResolveDoudouKey(string explicitKey, string suffix)
        {
            string key = !string.IsNullOrWhiteSpace(explicitKey)
                ? explicitKey.Trim()
                : $"doudou_{NpcId.ToLowerInvariant()}_{suffix}";

            return LocalizationManager.TryResolve(key, out string resolved) ? resolved : string.Empty;
        }

        public bool HasDoneLine()
        {
            return !string.IsNullOrEmpty(GetDoneText());
        }

        public string GetDoneText()
        {
            string specific = ResolveOptionalKey(doneKey, "done");
            if (!string.IsNullOrEmpty(specific))
                return specific;

            return LocalizationManager.TryResolve("ui_conv_nothing_more", out string generic)
                ? generic
                : string.Empty;
        }

        public bool HasDoudouWhisper()
        {
            return !string.IsNullOrEmpty(GetDoudouWhisperText());
        }

        public string GetDoudouWhisperText()
        {
            return ResolveDoudouKey(doudouWhisperKey, "whisper");
        }

        public bool HasDoudouReaction()
        {
            return !string.IsNullOrEmpty(GetDoudouReactionText());
        }

        public string GetDoudouReactionText()
        {
            return ResolveDoudouKey(doudouReactionKey, "reaction");
        }

        public void MarkDoudouWhisperPlayed() => _doudouWhisperPlayed = true;
        public void MarkDoudouReactionPlayed() => _doudouReactionPlayed = true;

        public string GetDisplayName()
        {
            string id = NpcId;
            string key = !string.IsNullOrWhiteSpace(npcNameKey)
                ? npcNameKey.Trim()
                : $"npc_{id.ToLowerInvariant()}_name";

            string resolved = LocalizationManager.Resolve(key, key);
            if (!string.IsNullOrEmpty(resolved) && resolved != key)
                return resolved;

            if (!string.IsNullOrWhiteSpace(npcNameKey))
                return resolved;

            return char.ToUpperInvariant(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
        }

        public string GetGreetingText()
        {
            if (string.IsNullOrWhiteSpace(greetingKey))
                return string.Empty;

            string key = greetingKey.Trim();
            return LocalizationManager.Resolve(key, key);
        }

        public string GetQuestionText(int index)
        {
            EnsureQuestionArraySize();
            if (index < 0 || index >= QuestionCount)
                return string.Empty;

            string key = questions[index].questionKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = $"npc_{NpcId.ToLowerInvariant()}_q{index + 1}";
            }
            else
            {
                key = key.Trim();
            }

            return LocalizationManager.Resolve(key, key);
        }

        public string GetAnswerText(int index)
        {
            EnsureQuestionArraySize();
            if (index < 0 || index >= QuestionCount)
                return string.Empty;

            string key = questions[index].answerKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = $"npc_{NpcId.ToLowerInvariant()}_a{index + 1}";
            }
            else
            {
                key = key.Trim();
            }

            return LocalizationManager.Resolve(key, key);
        }

        /// <summary>
        /// Marks the question at <paramref name="index"/> as asked and fires per-question hooks.
        /// Call <see cref="FinalizeCompletionIfAllAsked"/> once the reply finishes to fire completion hooks.
        /// </summary>
        public void MarkQuestionAsked(int index)
        {
            if (index < 0 || index >= QuestionCount)
                return;

            bool firstTime = !_asked[index];
            _asked[index] = true;

            if (firstTime)
            {
                OnQuestionAnswered?.Invoke(index);
                OnAnyQuestionAnswered?.Invoke(NpcId, index);
                ConversationProgress.NotifyNpcChanged(this);
            }
        }

        /// <summary>
        /// Fires <see cref="OnAllQuestionsAnswered"/> once if all 3 questions have been asked.
        /// </summary>
        public void FinalizeCompletionIfAllAsked()
        {
            if (_allAnsweredFired || !AreAllAsked)
                return;

            _allAnsweredFired = true;
            ConversationProgress.NotifyNpcChanged(this);
            OnAllQuestionsAnswered?.Invoke();
            OnAnyNpcCompleted?.Invoke(NpcId);
        }

        internal void NotifyConversationStarted()
        {
            OnAnyConversationStarted?.Invoke(NpcId);
        }

        internal void NotifyConversationEnded()
        {
            FinalizeCompletionIfAllAsked();
            OnAnyConversationEnded?.Invoke(NpcId);
        }

        /// <summary>
        /// Resets this NPC's asked state for the current session (helpful for replay/testing).
        /// </summary>
        public void ResetSessionState()
        {
            for (int i = 0; i < QuestionCount; i++)
            {
                _asked[i] = false;
            }
            _allAnsweredFired = false;
            _doudouWhisperPlayed = false;
            _doudouReactionPlayed = false;
            ConversationProgress.NotifyNpcChanged(this);
        }
    }
}
