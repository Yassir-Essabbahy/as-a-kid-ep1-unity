using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AsAKid.Conversation
{
    /// <summary>
    /// Tracks how many conversation NPCs (<see cref="NpcQuestionSet"/>) in the active scene
    /// have had all 3 questions answered. Future systems (such as the train arrival trigger)
    /// can subscribe to <see cref="OnProgressChanged"/> or <see cref="OnAllCompleted"/>.
    /// </summary>
    public static class ConversationProgress
    {
        private static readonly List<NpcQuestionSet> s_RegisteredNpcs = new List<NpcQuestionSet>();
        private static int s_LastDoneCount = -1;
        private static int s_LastTotalCount = -1;
        private static bool s_Initialized = false;

        /// <summary>
        /// Fired whenever the number of completed NPCs or total registered NPCs changes: (done, total).
        /// </summary>
        public static event Action<int, int> OnProgressChanged;

        /// <summary>
        /// Fired when TotalCount &gt; 0 and DoneCount reaches TotalCount.
        /// </summary>
        public static event Action OnAllCompleted;

        public static int DoneCount { get; private set; }
        public static int TotalCount { get; private set; }
        public static bool AllDone => TotalCount > 0 && DoneCount >= TotalCount;
        public static float NormalizedProgress => TotalCount > 0 ? (float)DoneCount / TotalCount : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnDomainReload()
        {
            s_RegisteredNpcs.Clear();
            s_LastDoneCount = -1;
            s_LastTotalCount = -1;
            DoneCount = 0;
            TotalCount = 0;
            OnProgressChanged = null;
            OnAllCompleted = null;
            s_Initialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureSceneHook()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Refresh();
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            Refresh();
        }

        /// <summary>
        /// Registers an NPC question set (called automatically by <see cref="NpcQuestionSet"/> on OnEnable).
        /// </summary>
        public static void Register(NpcQuestionSet npc)
        {
            EnsureSceneHook();
            if (npc == null)
                return;

            if (!s_RegisteredNpcs.Contains(npc))
            {
                s_RegisteredNpcs.Add(npc);
            }

            Recalculate(forceNotify: false);
        }

        /// <summary>
        /// Unregisters an NPC question set (called automatically by <see cref="NpcQuestionSet"/> on OnDisable/OnDestroy).
        /// </summary>
        public static void Unregister(NpcQuestionSet npc)
        {
            if (npc != null)
            {
                s_RegisteredNpcs.Remove(npc);
            }

            Recalculate(forceNotify: false);
        }

        /// <summary>
        /// Call when an NPC finishes all questions or changes its completion/inclusion state.
        /// </summary>
        public static void NotifyNpcChanged(NpcQuestionSet npc)
        {
            if (npc != null && npc.isActiveAndEnabled && !s_RegisteredNpcs.Contains(npc))
            {
                s_RegisteredNpcs.Add(npc);
            }

            Recalculate(forceNotify: false);
        }

        /// <summary>
        /// Re-evaluates all registered NPCs (also pruning any destroyed instances) and updates counts.
        /// </summary>
        public static void Refresh(bool forceNotify = false)
        {
            Recalculate(forceNotify);
        }

        private static void Recalculate(bool forceNotify)
        {
            int done = 0;
            int total = 0;

            for (int i = s_RegisteredNpcs.Count - 1; i >= 0; i--)
            {
                NpcQuestionSet npc = s_RegisteredNpcs[i];
                if (npc == null || !npc.gameObject.scene.IsValid())
                {
                    s_RegisteredNpcs.RemoveAt(i);
                    continue;
                }

                if (!npc.isActiveAndEnabled || !npc.IncludeInProgress)
                    continue;

                total++;
                if (npc.AreAllAsked)
                {
                    done++;
                }
            }

            DoneCount = done;
            TotalCount = total;

            bool changed = forceNotify || done != s_LastDoneCount || total != s_LastTotalCount;
            if (!changed)
                return;

            bool wasAllDone = s_LastTotalCount > 0 && s_LastDoneCount >= s_LastTotalCount;
            s_LastDoneCount = done;
            s_LastTotalCount = total;

            OnProgressChanged?.Invoke(done, total);

            bool isNowAllDone = total > 0 && done >= total;
            if (isNowAllDone && !wasAllDone)
            {
                OnAllCompleted?.Invoke();
            }
        }
    }
}
