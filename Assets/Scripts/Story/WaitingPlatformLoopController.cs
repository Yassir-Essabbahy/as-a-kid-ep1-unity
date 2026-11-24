using System.Collections;
using System.Collections.Generic;
using AsAKid.Conversation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the 3-loop Waiting Platform psychological horror sequence across Story 2 / Story 1:
/// - Loop 1: Interact with all 5 platform NPCs -> Blackout #1 -> Upstairs Fuse Box -> Jumpscare #1 (Crowd Stare + Bully Lunge) & Collapse -> Clean VHS restored.
/// - Loop 2: 5 platform NPCs only (no Bully, clean shader, darker dialogue) -> Blackout #2 -> Fuse Box Reset -> Jumpscare #2 (Short-Out + Neck Breathing + 3-Step Strobe Stalker) & Collapse -> Clean VHS restored.
/// - Loop 3: Bully alone on empty platform -> Go upstairs past Fuse Box (2nd staircase unlocks + brief stairwell scare cue) -> Imported interrogation.blend room -> Teacher-Parents Cutscene -> Dark Room -> MoroccanBeach.
/// </summary>
[DisallowMultipleComponent]
public class WaitingPlatformLoopController : MonoBehaviour
{
    public enum LoopPhase
    {
        Loop1_Questioning = 0,
        Loop1_Blackout_FuseBox = 1,
        Loop1_JumpscareCollapse = 2,
        Loop2_WakeUp = 3,
        Loop2_Questioning = 4,
        Loop2_Blackout_FuseBox = 5,
        Loop2_JumpscareCollapse = 6,
        Loop3_WakeUp = 7,
        Loop3_BullyOnly = 8,
        Loop3_GoUpstairsToRoom = 9
    }

    public static WaitingPlatformLoopController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Loop State")]
    [SerializeField] private LoopPhase currentPhase = LoopPhase.Loop1_Questioning;
    public LoopPhase CurrentPhase => currentPhase;
    public bool IsFuseBoxTaskUnlocked =>
        currentPhase == LoopPhase.Loop1_Blackout_FuseBox ||
        currentPhase == LoopPhase.Loop2_Blackout_FuseBox;
    public bool IsJumpscareRunning => _jumpscareRunning;
    public bool DidTriggerLoop3HallwayScare { get; private set; } = false;
    public bool DidTriggerBeachTransition { get; private set; } = false;
    public bool SkipSceneLoadForTesting { get; set; } = false;

    public void NotifyBeachTransitionTriggered()
    {
        DidTriggerBeachTransition = true;
    }

    [Header("Scene References")]
    public GameObject bullyLoop2Npc;
    public GameObject scare2EntityTemplate;
    public GameObject upstairsMeetingRoom;
    public GameObject upperStairsBlocker;
    public Volume retroVhsVolume;

    [Header("Real Recorded Audio Clips (Non-AI CC0)")]
    public AudioClip paChimeClip;
    public AudioClip staticBurstClip;
    public AudioClip trainHornClip;
    public AudioClip oceanWavesClip;
    public AudioClip rumbleClip;
    public AudioClip impactSlamClip;
    public AudioClip heavyBreathingClip;
    public AudioClip horrorShriekClip;
    public AudioClip horrorGrowlClip;
    public AudioClip electricShortClip;
    public AudioClip doorSlamClip;
    public AudioClip stalkAmbienceClip;
    public AudioClip[] metroFootstepClips;

    [Header("Jumpscare & Atmosphere Tuning")]
    [SerializeField] private float scareStareTimeout = 2.2f;
    [SerializeField] private float scareBlackoutDuration = 0.85f;
    [SerializeField] private float scareLungeDuration = 0.65f;
    [SerializeField] private float collapseDuration = 0.50f;
    [SerializeField] private float loop2LightMultiplier = 0.78f;
    [SerializeField] private float loop2UpperLightMultiplier = 0.72f;
    [SerializeField] private Color loop2LightTint = new Color(0.92f, 0.96f, 1.0f, 1f);
    [SerializeField] private float loop3LightMultiplier = 0.72f;
    [SerializeField] private Color loop3LightTint = new Color(0.86f, 0.93f, 1.0f, 1f);

    private class LightSnapshot
    {
        public Light Light;
        public float BaseIntensity;
        public Color BaseColor;
        public bool WasEnabled;
        public bool IsUpperGuide;
    }

    private readonly List<LightSnapshot> _stationLights = new List<LightSnapshot>();
    private LightSnapshot _loop2FlickerLight;
    private readonly List<NpcQuestionSet> _loop1Npcs = new List<NpcQuestionSet>();
    private readonly List<NpcLookAt> _loop1LookAts = new List<NpcLookAt>();
    private readonly Dictionary<Transform, float> _originalLookSpeeds = new Dictionary<Transform, float>();

    private RetroVHSVolumeComponent _vhs;
    private float _baseScanlineIntensity = 0f;
    private float _baseTapeWobble = 0f;
    private float _baseTracking = 0.0015f;
    private float _baseChromatic = 0.0006f;
    private float _baseEdgeChromatic = 0.0024f;
    private float _baseGrain = 0f;
    private float _baseVignette = 0.8f;
    private float _baseFisheye = 0f;
    private Color _baseShadowTint = Color.black;

    private AudioSource _sfxSource;
    private AudioSource _footstepSfxSource;
    private AudioSource _loopSfxSource;
    private AudioSource _oceanAudioSource;

    private bool _jumpscareRunning = false;
    private bool _blackout1Started = false;
    private bool _blackout2Started = false;
    private bool _upstairsUnlocked = false;
    private bool _loop3HallwayScareTriggered = false;
    private float _flickerTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SetupAudioSources();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void Start()
    {
        ResolveSceneReferences();
        CacheStationLights();
        CacheVhsBaseline();
        CacheLoop1Npcs();

        if (bullyLoop2Npc != null)
        {
            bullyLoop2Npc.SetActive(false);
        }
        if (upstairsMeetingRoom != null)
        {
            upstairsMeetingRoom.SetActive(false);
        }
        if (upperStairsBlocker != null)
        {
            upperStairsBlocker.SetActive(true);
        }

        ConversationProgress.RequireAllQuestions = false;
        ConversationProgress.Refresh(forceNotify: false);
        SubscribeEvents();

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("obj_loop1_talk", "Talk to everyone on the platform...");
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        RestoreVhsBaseline();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        RestoreVhsBaseline();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if ((currentPhase == LoopPhase.Loop2_Questioning || currentPhase == LoopPhase.Loop3_BullyOnly) &&
            !_jumpscareRunning &&
            _loop2FlickerLight != null &&
            _loop2FlickerLight.Light != null)
        {
            _flickerTimer += Time.deltaTime;
            float wave = Mathf.PerlinNoise(_flickerTimer * 6.5f, 1.3f);
            float stutter = (Mathf.Sin(_flickerTimer * 17f) * Mathf.Cos(_flickerTimer * 7f)) > 0.90f ? 0.35f : 1f;
            float mult = currentPhase == LoopPhase.Loop3_BullyOnly ? loop3LightMultiplier : loop2LightMultiplier;
            _loop2FlickerLight.Light.intensity = _loop2FlickerLight.BaseIntensity * mult * Mathf.Lerp(0.75f, 1.05f, wave) * stutter;
        }

        if (currentPhase == LoopPhase.Loop3_GoUpstairsToRoom && !_loop3HallwayScareTriggered && Application.isPlaying)
        {
            var fpc = GetPlayerController();
            if (fpc != null && fpc.transform.position.x < -45.5f && fpc.transform.position.y > 6.8f)
            {
                _loop3HallwayScareTriggered = true;
                DidTriggerLoop3HallwayScare = true;
                StartCoroutine(Loop3HallwayScareCueRoutine());
            }
        }
    }

    private void SetupAudioSources()
    {
        _sfxSource = GetOrCreateChildAudioSource("Loop_SFXSource", loop: false, volume: 1f);
        _footstepSfxSource = GetOrCreateChildAudioSource("Loop_FootstepSFXSource", loop: false, volume: 1f);
        _loopSfxSource = GetOrCreateChildAudioSource("Loop_RumbleSource", loop: true, volume: 0f);
        _oceanAudioSource = GetOrCreateChildAudioSource("Loop_OceanWavesSource", loop: true, volume: 0f);
    }

    private AudioSource GetOrCreateChildAudioSource(string childName, bool loop, float volume)
    {
        Transform t = transform.Find(childName);
        GameObject go = t != null ? t.gameObject : new GameObject(childName);
        if (t == null) go.transform.SetParent(transform, false);

        AudioSource src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        src.volume = volume;
        return src;
    }

    private void SubscribeEvents()
    {
        ConversationProgress.OnAllCompleted -= HandleAllConversationsCompleted;
        ConversationProgress.OnAllCompleted += HandleAllConversationsCompleted;

        if (MetroFuseBoxPuzzle.Instance != null)
        {
            MetroFuseBoxPuzzle.Instance.OnPostSolveInspectionClosed -= HandlePostFuseBoxClosed;
            MetroFuseBoxPuzzle.Instance.OnPostSolveInspectionClosed += HandlePostFuseBoxClosed;
        }

        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.OnCutsceneCompleted -= HandleFallCutsceneCompleted;
            FallCutscene.Instance.OnCutsceneCompleted += HandleFallCutsceneCompleted;
        }
    }

    private void UnsubscribeEvents()
    {
        ConversationProgress.OnAllCompleted -= HandleAllConversationsCompleted;

        if (MetroFuseBoxPuzzle.Instance != null)
        {
            MetroFuseBoxPuzzle.Instance.OnPostSolveInspectionClosed -= HandlePostFuseBoxClosed;
        }

        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.OnCutsceneCompleted -= HandleFallCutsceneCompleted;
        }
    }

    private void ResolveSceneReferences()
    {
        GameObject floor1Obj = GameObject.Find("Floor1");
        if (floor1Obj != null)
        {
            if (bullyLoop2Npc == null)
            {
                Transform b = floor1Obj.transform.Find("Bully_Loop2_NPC");
                if (b != null) bullyLoop2Npc = b.gameObject;
            }
            if (upstairsMeetingRoom == null)
            {
                Transform room = floor1Obj.transform.Find("UpstairsMeetingRoom");
                if (room != null) upstairsMeetingRoom = room.gameObject;
            }
            if (upperStairsBlocker == null)
            {
                Transform blk = floor1Obj.transform.Find("GroundObjects/UpperStairsBlocker");
                if (blk != null) upperStairsBlocker = blk.gameObject;
            }
        }

        if (retroVhsVolume == null)
        {
            GameObject vGo = GameObject.Find("RetroVHS_GlobalVolume");
            if (vGo != null) retroVhsVolume = vGo.GetComponent<Volume>();
        }
    }

    private FirstPersonController GetPlayerController()
    {
        var fpc = FirstPersonController.ActiveInstance;
        if (fpc == null) fpc = FindAnyObjectByType<FirstPersonController>();
        return fpc;
    }

    private void CacheStationLights()
    {
        _stationLights.Clear();
        _loop2FlickerLight = null;

        GameObject floor1 = GameObject.Find("Floor1");
        if (floor1 == null) return;

        Light cabinetLight = MetroFuseBoxPuzzle.Instance != null ? MetroFuseBoxPuzzle.Instance.cabinetLight : null;
        Transform metroT = floor1.transform.Find("Metro");
        if (metroT == null) return;

        Light[] lights = metroT.GetComponentsInChildren<Light>(true);
        float bestMidDist = float.MaxValue;

        foreach (Light l in lights)
        {
            if (l == null || l == cabinetLight) continue;
            if (!l.gameObject.activeSelf && !l.enabled) continue;

            bool isUpper = l.name.Contains("Room2") || l.transform.position.z > 42f;
            var snap = new LightSnapshot
            {
                Light = l,
                BaseIntensity = l.intensity,
                BaseColor = l.color,
                WasEnabled = l.enabled,
                IsUpperGuide = isUpper
            };
            _stationLights.Add(snap);

            if (!isUpper)
            {
                float distToMid = Mathf.Abs(l.transform.position.z - 20f);
                if (distToMid < bestMidDist)
                {
                    bestMidDist = distToMid;
                    _loop2FlickerLight = snap;
                }
            }
        }
    }

    private void CacheVhsBaseline()
    {
        _vhs = null;
        if (retroVhsVolume == null) return;

        // Access .profile in Play Mode so runtime clone never mutates .sharedProfile on disk
        VolumeProfile runtimeProfile = Application.isPlaying ? retroVhsVolume.profile : retroVhsVolume.sharedProfile;
        if (runtimeProfile != null && runtimeProfile.TryGet(out RetroVHSVolumeComponent comp))
        {
            _vhs = comp;
            _baseScanlineIntensity = _vhs.scanlineIntensity.value;
            _baseTapeWobble = _vhs.tapeWobble.value;
            _baseTracking = _vhs.trackingStrength.value;
            _baseChromatic = _vhs.chromaticBleed.value;
            _baseEdgeChromatic = _vhs.edgeChromatic.value;
            _baseGrain = _vhs.grainIntensity.value;
            _baseVignette = _vhs.vignetteIntensity.value;
            _baseFisheye = _vhs.fisheyeBend.value;
            _baseShadowTint = _vhs.shadowTint.value;
        }
    }

    private void RestoreVhsBaseline()
    {
        if (_vhs == null) return;
        _vhs.scanlineIntensity.value = _baseScanlineIntensity;
        _vhs.tapeWobble.value = _baseTapeWobble;
        _vhs.trackingStrength.value = _baseTracking;
        _vhs.chromaticBleed.value = _baseChromatic;
        _vhs.edgeChromatic.value = _baseEdgeChromatic;
        _vhs.grainIntensity.value = _baseGrain;
        _vhs.vignetteIntensity.value = _baseVignette;
        _vhs.fisheyeBend.value = _baseFisheye;
        _vhs.shadowTint.value = _baseShadowTint;
    }

    private void CacheLoop1Npcs()
    {
        _loop1Npcs.Clear();
        _loop1LookAts.Clear();
        _originalLookSpeeds.Clear();

        var allNpcs = FindObjectsByType<NpcQuestionSet>(FindObjectsInactive.Exclude);
        foreach (var npc in allNpcs)
        {
            if (npc == null || !npc.IncludeInProgress) continue;
            if (bullyLoop2Npc != null && npc.gameObject == bullyLoop2Npc) continue;

            _loop1Npcs.Add(npc);
            var look = npc.GetComponent<NpcLookAt>();
            if (look == null) look = npc.GetComponentInChildren<NpcLookAt>(true);
            if (look != null && !_loop1LookAts.Contains(look))
            {
                _loop1LookAts.Add(look);
                _originalLookSpeeds[look.transform] = look.lookSpeed;
            }
        }
    }

    private void HandleFallCutsceneCompleted()
    {
        if (currentPhase == LoopPhase.Loop1_Questioning)
        {
            if (MetroStorySequenceController.Instance != null)
            {
                MetroStorySequenceController.Instance.SetObjective("obj_loop1_talk", "Talk to everyone on the platform...");
            }
        }
        else if (currentPhase == LoopPhase.Loop2_WakeUp)
        {
            _jumpscareRunning = false;
            currentPhase = LoopPhase.Loop2_Questioning;
            StartCoroutine(PostLoop2WakeUpDialogueRoutine());
        }
        else if (currentPhase == LoopPhase.Loop3_WakeUp)
        {
            _jumpscareRunning = false;
            currentPhase = LoopPhase.Loop3_BullyOnly;
            StartCoroutine(PostLoop3WakeUpDialogueRoutine());
        }
    }

    private void HandleAllConversationsCompleted()
    {
        if (currentPhase == LoopPhase.Loop1_Questioning && !_blackout1Started)
        {
            _blackout1Started = true;
            StartCoroutine(BeginLoop1BlackoutRoutine());
        }
        else if (currentPhase == LoopPhase.Loop2_Questioning && !_blackout2Started)
        {
            _blackout2Started = true;
            StartCoroutine(BeginLoop2BlackoutRoutine());
        }
        else if (currentPhase == LoopPhase.Loop3_BullyOnly && !_upstairsUnlocked)
        {
            _upstairsUnlocked = true;
            StartCoroutine(UnlockUpstairsMeetingRoomRoutine());
        }
    }

    private void HandlePostFuseBoxClosed()
    {
        if (currentPhase == LoopPhase.Loop1_Blackout_FuseBox && !_jumpscareRunning)
        {
            StartCoroutine(PowerSurgeJumpscareCollapseRoutine());
        }
        else if (currentPhase == LoopPhase.Loop2_Blackout_FuseBox && !_jumpscareRunning)
        {
            StartCoroutine(Loop2HorrifyingJumpscareRoutine());
        }
    }

    // =========================================================================
    // LOOP 1 -> BLACKOUT #1 -> FUSE BOX #1 -> JUMPSCARE #1
    // =========================================================================

    private IEnumerator BeginLoop1BlackoutRoutine()
    {
        yield return null;
        while (ConversationUI.IsAnyActive || (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning))
        {
            yield return null;
        }

        if (currentPhase != LoopPhase.Loop1_Questioning) yield break;

        yield return new WaitForSeconds(0.45f);
        currentPhase = LoopPhase.Loop1_Blackout_FuseBox;

        PlayOneShot(_sfxSource, paChimeClip, 0.7f);
        PlayOneShot(_sfxSource, staticBurstClip, 0.55f);

        for (int f = 0; f < 3; f++)
        {
            SetAllStationLights(platformMult: 0.1f, platformTint: new Color(0.7f, 0.75f, 0.9f), upperMult: 0.3f, upperTint: Color.white);
            yield return new WaitForSeconds(0.08f + f * 0.03f);
            SetAllStationLights(platformMult: 1.0f, platformTint: Color.white, upperMult: 1.0f, upperTint: Color.white);
            yield return new WaitForSeconds(0.06f + f * 0.04f);
        }

        if (trainHornClip != null) PlayOneShot(_sfxSource, trainHornClip, 0.25f);

        SetAllStationLights(
            platformMult: 0.08f,
            platformTint: new Color(0.55f, 0.65f, 0.95f),
            upperMult: 0.35f,
            upperTint: new Color(1.0f, 0.85f, 0.65f)
        );

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("obj_restore_power_upstairs", "Restore the power at the fuse box upstairs...");
        }

        yield return new WaitForSeconds(0.6f);

        if (NpcDialogueManager.Instance != null)
        {
            string[] lines = new string[]
            {
                MetroStorySequenceController.GetLoc("loop1_blackout_01"),
                MetroStorySequenceController.GetLoc("loop1_blackout_02")
            };
            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                lines,
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    private IEnumerator PowerSurgeJumpscareCollapseRoutine()
    {
        _jumpscareRunning = true;
        currentPhase = LoopPhase.Loop1_JumpscareCollapse;

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("", "...");
        }

        var fpc = GetPlayerController();
        Camera playerCam = (fpc != null && fpc.playerCamera != null) ? fpc.playerCamera : Camera.main;

        // Beat 1: Brief false-relief power surge -> crimson warning shift
        SetAllStationLights(1.35f, Color.white, 1.35f, Color.white);
        yield return new WaitForSeconds(0.25f);
        SetAllStationLights(0.85f, Color.white, 0.85f, Color.white);
        yield return new WaitForSeconds(0.45f);

        SetAllStationLights(
            platformMult: 0.45f,
            platformTint: new Color(0.55f, 0.25f, 0.32f),
            upperMult: 0.45f,
            upperTint: new Color(0.75f, 0.35f, 0.42f)
        );

        // Beat 2: All 5 platform NPCs snap their heads up toward the player
        Transform camTarget = playerCam != null ? playerCam.transform : null;
        foreach (var look in _loop1LookAts)
        {
            if (look == null) continue;
            if (camTarget != null) look.LookAtObj = camTarget;
            look.lookSpeed = 12f;
            look.IKActive = true;
        }

        if (_loopSfxSource != null && rumbleClip != null)
        {
            _loopSfxSource.clip = rumbleClip;
            _loopSfxSource.volume = 0.28f;
            _loopSfxSource.Play();
        }

        float stareTimer = 0f;
        while (stareTimer < scareStareTimeout)
        {
            stareTimer += Time.deltaTime;
            if (stareTimer >= 0.45f && fpc != null && fpc.transform.position.z < 64.5f)
            {
                break;
            }
            yield return null;
        }

        // Beat 3: Sudden blackout + rapid footsteps rushing UP the stairs
        if (fpc != null)
        {
            fpc.SetControlLocked(true);
            fpc.SetCrosshairVisible(false);
            var rb = fpc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        SetAllStationLights(0f, Color.black, 0f, Color.black);
        if (MetroFuseBoxPuzzle.Instance != null && MetroFuseBoxPuzzle.Instance.cabinetLight != null)
        {
            MetroFuseBoxPuzzle.Instance.cabinetLight.intensity = 0f;
        }

        if (_loopSfxSource != null && _loopSfxSource.isPlaying)
        {
            _loopSfxSource.volume = 0.55f;
        }

        float startFov = playerCam != null ? playerCam.fieldOfView : 60f;
        int steps = 5;
        float stepInterval = scareBlackoutDuration / steps;
        for (int s = 0; s < steps; s++)
        {
            if (metroFootstepClips != null && metroFootstepClips.Length > 0)
            {
                AudioClip stepClip = metroFootstepClips[s % metroFootstepClips.Length];
                float vol = Mathf.Lerp(0.40f, 1.0f, (float)s / (steps - 1));
                _sfxSource.pitch = Mathf.Lerp(0.95f, 1.22f, (float)s / (steps - 1));
                PlayOneShot(_sfxSource, stepClip, vol);
            }

            float fovLerpT = (float)(s + 1) / steps;
            if (playerCam != null) playerCam.fieldOfView = Mathf.Lerp(startFov, Mathf.Max(48f, startFov - 6f), fovLerpT);
            yield return new WaitForSeconds(stepInterval);
        }
        _sfxSource.pitch = 1.0f;

        // Beat 4: Face lunge + real recorded scream/static + temporary VHS spike
        GameObject scareFigure = SpawnScareFigureFromTemplate(bullyLoop2Npc, playerCam, "ScareFigure_Loop1_Temp", 0.88f);
        Light scareLight = SpawnScareLight(playerCam, new Color(1f, 0.35f, 0.35f, 1f), 6.5f, 3.5f);

        PlayOneShot(_sfxSource, staticBurstClip, 0.90f);
        PlayOneShot(_sfxSource, trainHornClip, 0.85f);
        PlayOneShot(_sfxSource, horrorGrowlClip, 0.90f);

        if (_vhs != null)
        {
            _vhs.tapeWobble.value = 0.01f;
            _vhs.trackingStrength.value = 0.02f;
            _vhs.chromaticBleed.value = 0.02f;
            _vhs.edgeChromatic.value = 0.02f;
            _vhs.grainIntensity.value = 0.24f;
            _vhs.fisheyeBend.value = 0.16f;
        }

        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.SetFlashOverlayColor(new Color(0.85f, 0.95f, 1.0f, 0.85f));
        }

        float lungeElapsed = 0f;
        Vector3 camOrigLocalPos = playerCam != null ? playerCam.transform.localPosition : Vector3.zero;
        while (lungeElapsed < scareLungeDuration)
        {
            lungeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(lungeElapsed / scareLungeDuration);

            if (FallCutscene.Instance != null)
            {
                float flashAlpha = Mathf.Clamp01(1f - (lungeElapsed / 0.12f)) * 0.85f;
                FallCutscene.Instance.SetFlashOverlayColor(new Color(0.85f, 0.95f, 1.0f, flashAlpha));
            }

            if (playerCam != null)
            {
                Vector3 shake = new Vector3(
                    Random.Range(-1f, 1f) * 0.085f,
                    Random.Range(-1f, 1f) * 0.085f,
                    0f
                );
                playerCam.transform.localPosition = camOrigLocalPos + shake;

                if (scareFigure != null)
                {
                    Vector3 flatFwd = GetFlatForward(playerCam);
                    float dist = Mathf.Lerp(0.88f, 0.52f, t);
                    scareFigure.transform.position = playerCam.transform.position + flatFwd * dist + Vector3.down * 1.48f;
                    scareFigure.transform.rotation = Quaternion.LookRotation(-flatFwd, Vector3.up);
                }

                if (scareLight != null)
                {
                    scareLight.transform.position = playerCam.transform.position + playerCam.transform.forward * 0.35f;
                }
            }

            yield return null;
        }

        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.SetFlashOverlayColor(new Color(1f, 1f, 1f, 0f));
        }
        if (scareFigure != null) Destroy(scareFigure);
        if (scareLight != null) Destroy(scareLight.gameObject);

        // Beat 5: Collapse to floor + fade to black + IMMEDIATELY restore clean VHS baseline
        if (_loopSfxSource != null) _loopSfxSource.Stop();
        PlayOneShot(_sfxSource, impactSlamClip, 0.95f);

        yield return StartCoroutine(CollapseCameraToBlackRoutine(playerCam, camOrigLocalPos, startFov, collapseDuration));

        RestoreVhsBaseline();
        yield return new WaitForSeconds(0.85f);

        TransitionToLoop2();
    }

    // =========================================================================
    // LOOP 2 (5 CHARACTERS ONLY, NO BULLY, CLEAN SHADER) -> BLACKOUT #2 -> JUMPSCARE #2
    // =========================================================================

    private void TransitionToLoop2()
    {
        RestoreVhsBaseline();

        SetAllStationLights(
            platformMult: loop2LightMultiplier,
            platformTint: loop2LightTint,
            upperMult: loop2UpperLightMultiplier,
            upperTint: loop2LightTint
        );

        if (MetroFuseBoxPuzzle.Instance != null && MetroFuseBoxPuzzle.Instance.cabinetLight != null)
        {
            MetroFuseBoxPuzzle.Instance.cabinetLight.intensity = 0.25f;
            MetroFuseBoxPuzzle.Instance.cabinetLight.color = Color.white;
        }

        if (_oceanAudioSource != null)
        {
            _oceanAudioSource.Stop();
            _oceanAudioSource.volume = 0f;
            _oceanAudioSource.clip = null;
        }

        foreach (var look in _loop1LookAts)
        {
            if (look == null) continue;
            look.IKActive = false;
            if (_originalLookSpeeds.TryGetValue(look.transform, out float origSpeed))
            {
                look.lookSpeed = origSpeed;
            }
        }

        foreach (var npc in _loop1Npcs)
        {
            if (npc == null) continue;
            Vector3 p = npc.transform.position;
            if (p.x < -41f) p.x += 0.55f;
            else if (p.x > -39f) p.x -= 0.35f;
            npc.transform.position = p;
        }

        // Keep Bully hidden in Loop 2; only the 5 platform NPCs remain
        if (bullyLoop2Npc != null)
        {
            bullyLoop2Npc.SetActive(false);
        }

        foreach (var qs in _loop1Npcs)
        {
            if (qs == null || !qs.IncludeInProgress) continue;
            string id = qs.NpcId.ToLowerInvariant();
            ApplyLoop2QuestionsForNpc(qs, id);
        }

        ConversationProgress.Refresh(forceNotify: false);

        currentPhase = LoopPhase.Loop2_WakeUp;
        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.ReplayWakeUpFromStart(suppressDefaultObjective: true);
        }
        else
        {
            var fpc = GetPlayerController();
            if (fpc != null)
            {
                fpc.SetControlLocked(false);
                fpc.SetCrosshairVisible(true);
            }
            _jumpscareRunning = false;
            currentPhase = LoopPhase.Loop2_Questioning;
            StartCoroutine(PostLoop2WakeUpDialogueRoutine());
        }
    }

    private IEnumerator PostLoop2WakeUpDialogueRoutine()
    {
        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("obj_loop2_talk", "Talk to them again... something changed.");
        }

        yield return new WaitForSeconds(0.4f);

        if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning && !ConversationUI.IsAnyActive)
        {
            string line = MetroStorySequenceController.GetLoc("loop2_wakeup_01");
            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { line },
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    private IEnumerator BeginLoop2BlackoutRoutine()
    {
        yield return null;
        while (ConversationUI.IsAnyActive || (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning))
        {
            yield return null;
        }

        if (currentPhase != LoopPhase.Loop2_Questioning) yield break;

        yield return new WaitForSeconds(0.45f);
        currentPhase = LoopPhase.Loop2_Blackout_FuseBox;

        if (MetroFuseBoxPuzzle.Instance != null)
        {
            MetroFuseBoxPuzzle.Instance.PrepareForNextLoopBlackout(skipTeddyHint: true);
        }

        PlayOneShot(_sfxSource, electricShortClip, 0.75f);
        PlayOneShot(_sfxSource, staticBurstClip, 0.65f);

        for (int f = 0; f < 4; f++)
        {
            SetAllStationLights(platformMult: 0.05f, platformTint: new Color(0.55f, 0.65f, 0.9f), upperMult: 0.15f, upperTint: Color.white);
            yield return new WaitForSeconds(0.06f + f * 0.025f);
            SetAllStationLights(platformMult: 0.95f, platformTint: loop2LightTint, upperMult: 0.95f, upperTint: loop2LightTint);
            yield return new WaitForSeconds(0.05f + f * 0.03f);
        }

        SetAllStationLights(
            platformMult: 0.06f,
            platformTint: new Color(0.50f, 0.60f, 0.92f),
            upperMult: 0.28f,
            upperTint: new Color(1.0f, 0.80f, 0.58f)
        );

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("obj_restore_power_upstairs", "Restore the power at the fuse box upstairs...");
        }

        yield return new WaitForSeconds(0.55f);

        if (NpcDialogueManager.Instance != null)
        {
            string[] lines = new string[]
            {
                MetroStorySequenceController.GetLoc("loop2_blackout_01"),
                MetroStorySequenceController.GetLoc("loop2_blackout_02")
            };
            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                lines,
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    private IEnumerator Loop2HorrifyingJumpscareRoutine()
    {
        _jumpscareRunning = true;
        currentPhase = LoopPhase.Loop2_JumpscareCollapse;

        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("", "...");
        }

        var fpc = GetPlayerController();
        Camera playerCam = (fpc != null && fpc.playerCamera != null) ? fpc.playerCamera : Camera.main;
        float startFov = playerCam != null ? playerCam.fieldOfView : 60f;
        Vector3 camOrigLocalPos = playerCam != null ? playerCam.transform.localPosition : Vector3.zero;

        // Beat 1 (~1.1s): Power Short & Blackout + Low Dread Rumble
        PlayOneShot(_sfxSource, electricShortClip, 0.95f);
        PlayOneShot(_sfxSource, staticBurstClip, 0.70f);
        SetAllStationLights(0f, Color.black, 0f, Color.black);

        if (_loopSfxSource != null && rumbleClip != null)
        {
            _loopSfxSource.clip = rumbleClip;
            _loopSfxSource.pitch = 0.85f;
            _loopSfxSource.volume = 0.18f;
            if (!_loopSfxSource.isPlaying) _loopSfxSource.Play();
        }

        Light cabLight = MetroFuseBoxPuzzle.Instance != null ? MetroFuseBoxPuzzle.Instance.cabinetLight : null;
        if (cabLight != null)
        {
            cabLight.color = Color.red;
            cabLight.intensity = 0.55f;
            yield return new WaitForSeconds(0.22f);
            cabLight.intensity = 0.05f;
            yield return new WaitForSeconds(0.14f);
            cabLight.intensity = 0.38f;
            yield return new WaitForSeconds(0.24f);
            cabLight.intensity = 0f;
            cabLight.color = Color.white;
            yield return new WaitForSeconds(0.50f);
        }
        else
        {
            yield return new WaitForSeconds(1.10f);
        }

        // Beat 2 (~1.8s): Echoing slow footsteps from down the dark corridor + forced look turn
        Vector3 playerPos = fpc != null ? fpc.transform.position : (playerCam != null ? playerCam.transform.position : new Vector3(-38.68f, 6.58f, 66.0f));
        Vector3 corridorDir = ResolveCorridorDirection(playerPos, playerCam);

        PlayFootstepPitched(0, 0.80f, 0.42f);
        PlayOneShot(_sfxSource, stalkAmbienceClip, 0.30f);

        float freeLookTimer = 0f;
        bool secondStepPlayed = false;
        while (freeLookTimer < 1.25f)
        {
            freeLookTimer += Time.deltaTime;
            if (!secondStepPlayed && freeLookTimer >= 0.58f)
            {
                secondStepPlayed = true;
                PlayFootstepPitched(1, 0.80f, 0.48f);
            }
            if (freeLookTimer >= 0.35f && playerCam != null && Vector3.Dot(GetFlatForward(playerCam), corridorDir) > 0.55f)
            {
                break;
            }
            yield return null;
        }

        if (fpc != null)
        {
            fpc.SetControlLocked(true);
            fpc.SetCrosshairVisible(false);
            var rb = fpc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        float turnStartYaw = fpc != null ? fpc.Yaw : (playerCam != null ? playerCam.transform.eulerAngles.y : 180f);
        float turnStartPitch = fpc != null ? fpc.Pitch : 0f;
        float turnTargetYaw = Quaternion.LookRotation(corridorDir, Vector3.up).eulerAngles.y;
        float turnElapsed = 0f;
        const float turnDuration = 0.55f;
        while (turnElapsed < turnDuration)
        {
            turnElapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(turnElapsed / turnDuration));
            if (fpc != null)
            {
                fpc.SetViewAngles(Mathf.LerpAngle(turnStartYaw, turnTargetYaw, u), Mathf.LerpAngle(turnStartPitch, -2f, u));
                if (playerCam != null) playerCam.transform.localPosition = camOrigLocalPos;
            }
            yield return null;
        }

        // Beat 3 (~1.7s): Still Dark Silhouette Reveal at 8.2m with glowing white eyes
        GameObject template = scare2EntityTemplate != null ? scare2EntityTemplate : bullyLoop2Npc;
        float startDist = 8.2f;
        float stopDist = 1.15f;
        float fallbackGroundY = playerPos.z > 60f ? 5.58f : (playerPos.y - 1.0f);
        Vector3 spawnXZ = playerPos + corridorDir * startDist;
        float initialGroundY = SampleCorridorGroundY(spawnXZ, fallbackGroundY);

        GameObject entity = SpawnScareFigureFromTemplate(template, playerCam, "Scare2_Stalker_Temp", startDist);
        Animator stalkerAnim = null;
        DarkStalkerHeadTwitch headTwitch = null;
        AudioSource breathSrc = null;

        if (entity != null)
        {
            entity.transform.position = new Vector3(spawnXZ.x, initialGroundY, spawnXZ.z);
            entity.transform.rotation = Quaternion.LookRotation(-corridorDir, Vector3.up);
            entity.SetActive(true);

            stalkerAnim = entity.GetComponentInChildren<Animator>();
            if (stalkerAnim != null)
            {
                stalkerAnim.applyRootMotion = false;
                stalkerAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                stalkerAnim.speed = 0.04f;
            }

            headTwitch = entity.GetComponent<DarkStalkerHeadTwitch>();
            if (headTwitch == null) headTwitch = entity.AddComponent<DarkStalkerHeadTwitch>();
            headTwitch.lookTarget = playerCam != null ? playerCam.transform : null;
            headTwitch.neckRollDegrees = 8f;
            headTwitch.twitchAmplitude = 0.6f;
            headTwitch.lookWeight = 0.94f;
            headTwitch.lockOnTarget = true;

            if (heavyBreathingClip != null)
            {
                breathSrc = entity.AddComponent<AudioSource>();
                breathSrc.clip = heavyBreathingClip;
                breathSrc.loop = true;
                breathSrc.playOnAwake = false;
                breathSrc.spatialBlend = 0.85f;
                breathSrc.minDistance = 0.8f;
                breathSrc.maxDistance = 12f;
                breathSrc.volume = 0.18f;
                breathSrc.Play();
            }
        }

        Color coldRimColor = new Color(0.65f, 0.78f, 0.92f, 1f);
        Light corridorLight = SpawnScareLight(playerCam, coldRimColor, 2.2f, 12.0f);
        if (corridorLight != null && entity != null)
        {
            corridorLight.transform.position = entity.transform.position + corridorDir * 1.6f + Vector3.up * 2.8f;
        }

        if (_sfxSource != null && stalkAmbienceClip != null)
        {
            _sfxSource.pitch = 0.78f;
            PlayOneShot(_sfxSource, stalkAmbienceClip, 0.35f);
        }

        yield return new WaitForSeconds(1.25f);
        if (corridorLight != null) corridorLight.intensity = 0f;
        if (_sfxSource != null) _sfxSource.pitch = 1.0f;
        PlayOneShot(_sfxSource, staticBurstClip, 0.22f);
        yield return new WaitForSeconds(0.25f);
        if (corridorLight != null) corridorLight.intensity = 2.1f;
        yield return new WaitForSeconds(0.20f);

        // Beat 4 (~7.2s): Unbearably Slow Relentless Approach (Clean VHS preserved!)
        if (stalkerAnim != null)
        {
            stalkerAnim.speed = 0.78f;
        }

        const float walkDuration = 7.2f;
        float walkElapsed = 0f;
        float nextStepTime = 0.15f;
        int stepIndex = 0;
        bool blackout1Entered = false;
        bool blackout1Recovered = false;
        bool blackout2Entered = false;
        bool blackout2Recovered = false;

        while (walkElapsed < walkDuration)
        {
            walkElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(walkElapsed / walkDuration);
            float dist = Mathf.Lerp(startDist, stopDist, u);

            if (entity != null)
            {
                Vector3 posXZ = playerPos + corridorDir * dist;
                float gY = SampleCorridorGroundY(posXZ, initialGroundY);
                entity.transform.position = new Vector3(posXZ.x, gY, posXZ.z);
                Vector3 toPl = playerPos - entity.transform.position;
                toPl.y = 0f;
                if (toPl.sqrMagnitude > 0.001f)
                {
                    entity.transform.rotation = Quaternion.LookRotation(toPl.normalized, Vector3.up);
                }
            }

            if (corridorLight != null && entity != null)
            {
                corridorLight.transform.position = entity.transform.position + corridorDir * 1.6f + Vector3.up * 2.7f;
            }

            // Mid-walk flicker blackouts (white eyes keep gliding closer through pitch darkness)
            if (walkElapsed >= 2.50f && walkElapsed < 2.88f)
            {
                if (!blackout1Entered)
                {
                    blackout1Entered = true;
                    PlayOneShot(_sfxSource, staticBurstClip, 0.28f);
                }
                if (corridorLight != null) corridorLight.intensity = 0f;
            }
            else if (walkElapsed >= 2.88f && walkElapsed < 5.05f && !blackout1Recovered)
            {
                blackout1Recovered = true;
                if (corridorLight != null) corridorLight.intensity = 2.0f;
                if (headTwitch != null)
                {
                    headTwitch.neckRollDegrees = 28f;
                    headTwitch.twitchAmplitude = 1.8f;
                }
            }
            else if (walkElapsed >= 5.05f && walkElapsed < 5.53f)
            {
                if (!blackout2Entered)
                {
                    blackout2Entered = true;
                    PlayOneShot(_sfxSource, electricShortClip, 0.35f);
                }
                if (corridorLight != null) corridorLight.intensity = 0f;
            }
            else if (walkElapsed >= 5.53f && !blackout2Recovered)
            {
                blackout2Recovered = true;
                if (corridorLight != null)
                {
                    corridorLight.color = new Color(0.88f, 0.55f, 0.55f, 1f);
                    corridorLight.intensity = 1.35f;
                }
                if (headTwitch != null)
                {
                    headTwitch.neckRollDegrees = -32f;
                    headTwitch.twitchAmplitude = 4.5f;
                }
            }

            if (walkElapsed >= nextStepTime)
            {
                nextStepTime += 0.72f;
                PlayFootstepPitched(stepIndex, Mathf.Lerp(0.78f, 0.88f, u), Mathf.Lerp(0.35f, 0.95f, u));
                stepIndex++;
            }

            if (breathSrc != null) breathSrc.volume = Mathf.Lerp(0.18f, 0.90f, u);
            if (_loopSfxSource != null) _loopSfxSource.volume = Mathf.Lerp(0.18f, 0.55f, u);

            if (playerCam != null)
            {
                Vector3 eyeTarget = headTwitch != null ? headTwitch.EyeWorldPosition : (playerPos + corridorDir * dist + Vector3.up * 1.95f);
                AimPlayerCameraAtPoint(fpc, playerCam, eyeTarget, 6f);
                float tremorAmp = Mathf.Lerp(0.002f, 0.011f, u);
                playerCam.transform.localPosition = camOrigLocalPos + new Vector3(
                    Random.Range(-1f, 1f) * tremorAmp,
                    Random.Range(-1f, 1f) * tremorAmp,
                    0f
                );
                playerCam.fieldOfView = Mathf.Lerp(startFov, Mathf.Max(46f, startFov - 10f), u);
            }

            yield return null;
        }

        // Beat 5 (~1.35s): Dead Stop Inches Away + Uncanny Neck Snap & Growl
        if (stalkerAnim != null) stalkerAnim.speed = 0.03f;
        if (corridorLight != null) corridorLight.intensity = 0.65f;
        if (_loopSfxSource != null) _loopSfxSource.volume = 0.05f;
        if (breathSrc != null) breathSrc.volume = 0.95f;

        float freezeElapsed = 0f;
        while (freezeElapsed < 0.95f)
        {
            freezeElapsed += Time.deltaTime;
            if (playerCam != null)
            {
                Vector3 eyeTarget = headTwitch != null ? headTwitch.EyeWorldPosition : (playerPos + corridorDir * stopDist + Vector3.up * 1.95f);
                AimPlayerCameraAtPoint(fpc, playerCam, eyeTarget, 10f);
                playerCam.transform.localPosition = camOrigLocalPos;
            }
            yield return null;
        }

        if (headTwitch != null)
        {
            headTwitch.neckRollDegrees = 38f;
            headTwitch.twitchAmplitude = 10f;
        }
        PlayOneShot(_sfxSource, horrorGrowlClip, 0.92f);
        yield return new WaitForSeconds(0.40f);

        // Beat 6 (~0.87s lunge + 0.42s collapse + 0.85s black): Violent Close-Up Lunge, Shriek & VHS Tear -> Clean Reset
        if (corridorLight != null) corridorLight.intensity = 0f;
        yield return new WaitForSeconds(0.07f);

        if (stalkerAnim != null)
        {
            stalkerAnim.SetBool("Lunge", true);
            stalkerAnim.speed = 1.25f;
        }
        if (headTwitch != null)
        {
            headTwitch.neckRollDegrees = -14f;
            headTwitch.twitchAmplitude = 14f;
        }

        PlayOneShot(_sfxSource, horrorShriekClip, 1.0f);
        PlayOneShot(_sfxSource, horrorGrowlClip, 0.85f);
        PlayOneShot(_sfxSource, impactSlamClip, 0.95f);
        PlayOneShot(_sfxSource, staticBurstClip, 0.85f);

        Light strobeLight = SpawnScareLight(playerCam, Color.white, 13.0f, 6.0f);

        if (_vhs != null)
        {
            _vhs.scanlineIntensity.value = 0.55f;
            _vhs.tapeWobble.value = 0.018f;
            _vhs.trackingStrength.value = 0.030f;
            _vhs.chromaticBleed.value = 0.032f;
            _vhs.edgeChromatic.value = 0.030f;
            _vhs.grainIntensity.value = 0.30f;
            _vhs.fisheyeBend.value = 0.22f;
        }

        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.SetFlashOverlayColor(new Color(1f, 0.9f, 0.9f, 0.90f));
        }

        float climaxElapsed = 0f;
        const float climaxDur = 0.80f;
        int frameToggle = 0;
        while (climaxElapsed < climaxDur)
        {
            climaxElapsed += Time.deltaTime;
            frameToggle++;

            if (FallCutscene.Instance != null)
            {
                float flashAlpha = Mathf.Clamp01(1f - (climaxElapsed / 0.14f)) * 0.90f;
                FallCutscene.Instance.SetFlashOverlayColor(new Color(1f, 0.9f, 0.9f, flashAlpha));
            }

            if (playerCam != null)
            {
                Vector3 fwd = GetFlatForward(playerCam);
                Vector3 shake = new Vector3(
                    Random.Range(-1f, 1f) * 0.11f,
                    Random.Range(-1f, 1f) * 0.11f,
                    0f
                );
                playerCam.transform.localPosition = camOrigLocalPos + shake;
                playerCam.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-5f, 5f), Random.Range(-16f, 16f));

                if (entity != null)
                {
                    Vector3 twitch = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(-0.04f, 0.04f), 0f);
                    Vector3 targetEyePos = playerCam.transform.position + fwd * 0.38f + twitch;
                    entity.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
                    if (headTwitch != null)
                    {
                        entity.transform.position += (targetEyePos - headTwitch.EyeWorldPosition);
                    }
                    else
                    {
                        entity.transform.position = targetEyePos + Vector3.down * 1.95f;
                    }
                }

                if (strobeLight != null)
                {
                    strobeLight.transform.position = playerCam.transform.position + fwd * 0.20f;
                    bool even = (frameToggle / 2) % 2 == 0;
                    strobeLight.color = even ? Color.white : new Color(1.0f, 0.15f, 0.15f, 1f);
                    strobeLight.intensity = even ? 14.0f : 11.0f;
                }
            }

            yield return null;
        }

        if (_loopSfxSource != null)
        {
            _loopSfxSource.Stop();
            _loopSfxSource.pitch = 1.0f;
            _loopSfxSource.volume = 0f;
        }
        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.SetFlashOverlayColor(new Color(1f, 1f, 1f, 0f));
        }
        if (entity != null) Destroy(entity);
        if (corridorLight != null) Destroy(corridorLight.gameObject);
        if (strobeLight != null) Destroy(strobeLight.gameObject);

        // Violent collapse + IMMEDIATE clean VHS reset
        PlayOneShot(_sfxSource, impactSlamClip, 0.95f);
        yield return StartCoroutine(CollapseCameraToBlackRoutine(playerCam, camOrigLocalPos, startFov, 0.42f));

        RestoreVhsBaseline();
        yield return new WaitForSeconds(0.85f);

        TransitionToLoop3();
    }

    // =========================================================================
    // LOOP 3: BULLY ONLY -> GO UPSTAIRS TO IMPORTED ROOM -> HALLWAY SCARE CUE
    // =========================================================================

    private void TransitionToLoop3()
    {
        RestoreVhsBaseline();

        // Hide all 5 platform characters
        foreach (var npc in _loop1Npcs)
        {
            if (npc != null)
            {
                npc.gameObject.SetActive(false);
            }
        }

        // Activate ONLY Bully in Loop 3 facing spawn
        if (bullyLoop2Npc != null)
        {
            bullyLoop2Npc.transform.position = new Vector3(-40.5f, 1.42f, 24.5f);
            bullyLoop2Npc.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            bullyLoop2Npc.SetActive(true);

            var bullyQs = bullyLoop2Npc.GetComponent<NpcQuestionSet>();
            if (bullyQs != null)
            {
                ApplyLoop2QuestionsForNpc(bullyQs, "bully");
            }
        }

        // Clean cool Loop 3 platform lighting (shader untouched!)
        SetAllStationLights(
            platformMult: loop3LightMultiplier,
            platformTint: loop3LightTint,
            upperMult: loop3LightMultiplier,
            upperTint: loop3LightTint
        );

        if (_oceanAudioSource != null)
        {
            _oceanAudioSource.Stop();
            _oceanAudioSource.volume = 0f;
            _oceanAudioSource.clip = null;
        }

        ConversationProgress.Refresh(forceNotify: false);

        currentPhase = LoopPhase.Loop3_WakeUp;
        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.ReplayWakeUpFromStart(suppressDefaultObjective: true);
        }
        else
        {
            var fpc = GetPlayerController();
            if (fpc != null)
            {
                fpc.SetControlLocked(false);
                fpc.SetCrosshairVisible(true);
            }
            _jumpscareRunning = false;
            currentPhase = LoopPhase.Loop3_BullyOnly;
            StartCoroutine(PostLoop3WakeUpDialogueRoutine());
        }
    }

    private IEnumerator PostLoop3WakeUpDialogueRoutine()
    {
        if (MetroStorySequenceController.Instance != null)
        {
            MetroStorySequenceController.Instance.SetObjective("obj_loop3_bully", "Everyone is gone... talk to him.");
        }

        yield return new WaitForSeconds(0.4f);

        if (NpcDialogueManager.Instance != null && !NpcDialogueManager.Instance.IsDialogueRunning && !ConversationUI.IsAnyActive)
        {
            string line = MetroStorySequenceController.GetLoc("loop3_wakeup_01");
            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                new string[] { line },
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }
    }

    private IEnumerator UnlockUpstairsMeetingRoomRoutine()
    {
        yield return null;
        while (ConversationUI.IsAnyActive || (NpcDialogueManager.Instance != null && NpcDialogueManager.Instance.IsDialogueRunning))
        {
            yield return null;
        }

        if (currentPhase != LoopPhase.Loop3_BullyOnly) yield break;

        currentPhase = LoopPhase.Loop3_GoUpstairsToRoom;
        _loop3HallwayScareTriggered = false;

        if (upperStairsBlocker != null)
        {
            upperStairsBlocker.SetActive(false);
        }

        if (upstairsMeetingRoom != null)
        {
            upstairsMeetingRoom.SetActive(true);
        }

        // Brighten upper landing / stairs slightly so the open 2nd staircase & DoorLamp invite the player up
        SetAllStationLights(
            platformMult: 0.45f,
            platformTint: loop3LightTint,
            upperMult: 0.70f,
            upperTint: new Color(1.0f, 0.90f, 0.78f, 1f)
        );

        var msc = MetroStorySequenceController.Instance;
        if (msc != null)
        {
            if (msc.cinemachineBrain != null)
            {
                msc.cinemachineBrain.enabled = false;
            }
            GameObject targetDoorObj = msc.doorObject;
            BoxCollider targetDoorCol = msc.doorTrigger;
            if (upstairsMeetingRoom != null)
            {
                Transform db = upstairsMeetingRoom.transform.Find("Door_Behind");
                if (db != null)
                {
                    targetDoorObj = db.gameObject;
                    targetDoorCol = db.GetComponent<BoxCollider>();
                    var doorInteract = db.GetComponent<DoorInteractable>();
                    if (doorInteract != null) doorInteract.enabled = true;
                }
            }
            msc.PrepareDoorWaiting(targetDoorObj, targetDoorCol);
            msc.SetObjective("obj_go_upstairs_room", "Go upstairs... follow the voices.");
        }

        var fpc = GetPlayerController();
        EnsurePlayerUnlockedAndMobileReady(fpc);

        PlayOneShot(_sfxSource, doorSlamClip, 0.50f);

        yield return new WaitForSeconds(0.48f);

        if (NpcDialogueManager.Instance != null)
        {
            string[] lines = new string[]
            {
                MetroStorySequenceController.GetLoc("loop3_upstairs_01"),
                MetroStorySequenceController.GetLoc("loop3_upstairs_02")
            };
            yield return StartCoroutine(NpcDialogueManager.Instance.ShowDialogue(
                lines,
                hasChoice: false,
                dialogueColor: NpcDialogueManager.GetSpeakerColor("teddy"),
                dialogueFont: NpcDialogueManager.DefaultDialogueFont
            ));
        }

        EnsurePlayerUnlockedAndMobileReady(GetPlayerController());
    }

    private void EnsurePlayerUnlockedAndMobileReady(FirstPersonController fpc)
    {
        if (fpc != null)
        {
            fpc.enabled = true;
            fpc.SetControlLocked(false);
            fpc.SetCrosshairVisible(true);
            var rb = fpc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }

        bool isPaused = Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused;
        if (!Application.isMobilePlatform && !Story.MobileUIAdapter.IsMobile && !isPaused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private IEnumerator Loop3HallwayScareCueRoutine()
    {
        PlayOneShot(_sfxSource, doorSlamClip, 0.90f);
        PlayOneShot(_sfxSource, staticBurstClip, 0.70f);

        SetAllStationLights(0.06f, new Color(0.5f, 0.55f, 0.8f), 0.08f, new Color(0.6f, 0.25f, 0.25f));

        GameObject template = scare2EntityTemplate != null ? scare2EntityTemplate : bullyLoop2Npc;
        GameObject silhouette = SpawnScareFigureFromTemplate(template, null, "Scare3_HallwaySilhouette_Temp", 0f);
        if (silhouette != null)
        {
            silhouette.transform.position = new Vector3(-51.5f, 11.09f, 65.50f);
            silhouette.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var sAnim = silhouette.GetComponentInChildren<Animator>();
            if (sAnim != null)
            {
                sAnim.applyRootMotion = false;
                sAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                sAnim.speed = 0.04f;
            }
            silhouette.SetActive(true);
        }

        if (_vhs != null)
        {
            _vhs.scanlineIntensity.value = 0.40f;
            _vhs.tapeWobble.value = 0.010f;
            _vhs.trackingStrength.value = 0.020f;
            _vhs.chromaticBleed.value = 0.018f;
            _vhs.grainIntensity.value = 0.20f;
        }

        yield return new WaitForSeconds(0.28f);

        SetAllStationLights(0f, Color.black, 0f, Color.black);
        if (silhouette != null) Destroy(silhouette);
        yield return new WaitForSeconds(0.14f);

        RestoreVhsBaseline();
        SetAllStationLights(
            platformMult: 0.45f,
            platformTint: loop3LightTint,
            upperMult: 0.70f,
            upperTint: new Color(1.0f, 0.90f, 0.78f, 1f)
        );
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void ApplyLoop2QuestionsForNpc(NpcQuestionSet qs, string id)
    {
        var entries = new NpcQuestionSet.QuestionEntry[]
        {
            new NpcQuestionSet.QuestionEntry { questionKey = $"npc_{id}_l2_q1", answerKey = $"npc_{id}_l2_a1" },
            new NpcQuestionSet.QuestionEntry { questionKey = $"npc_{id}_l2_q2", answerKey = $"npc_{id}_l2_a2" },
            new NpcQuestionSet.QuestionEntry { questionKey = $"npc_{id}_l2_q3", answerKey = $"npc_{id}_l2_a3" }
        };

        qs.ApplyQuestionSet($"npc_{id}_l2_greeting", $"npc_{id}_l2_done", entries, resetAskedState: true);
    }

    private float SampleCorridorGroundY(Vector3 worldPos, float fallbackGroundY)
    {
        if (Physics.Raycast(new Vector3(worldPos.x, worldPos.y + 2.5f, worldPos.z), Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        return fallbackGroundY;
    }

    private Vector3 ResolveCorridorDirection(Vector3 playerPos, Camera playerCam)
    {
        if (playerPos.z > 60f) return Vector3.back;

        Vector3 backDir = playerCam != null ? -GetFlatForward(playerCam) : Vector3.back;
        Vector3[] candidates = new Vector3[] { backDir, Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
        Vector3 origin = playerPos + Vector3.up * 0.4f;
        Vector3 bestDir = backDir;
        float bestDist = -1f;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 dir = candidates[i];
            if (Physics.Raycast(origin, dir, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance > bestDist)
                {
                    bestDist = hit.distance;
                    bestDir = dir;
                }
            }
            else
            {
                return dir;
            }
        }
        return bestDir;
    }

    private void PlayFootstepPitched(int index, float pitch, float volume)
    {
        if (_footstepSfxSource == null || metroFootstepClips == null || metroFootstepClips.Length == 0) return;
        AudioClip clip = metroFootstepClips[index % metroFootstepClips.Length];
        if (clip == null) return;
        _footstepSfxSource.pitch = pitch;
        _footstepSfxSource.PlayOneShot(clip, volume);
    }

    private void AimPlayerCameraAtPoint(FirstPersonController fpc, Camera playerCam, Vector3 targetWorldPos, float lerpSpeed)
    {
        if (playerCam == null) return;
        Vector3 dir = targetWorldPos - playerCam.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        float desiredYaw = flatDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatDir.normalized, Vector3.up).eulerAngles.y
            : (fpc != null ? fpc.Yaw : playerCam.transform.eulerAngles.y);
        float desiredPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -0.8f, 0.8f)) * Mathf.Rad2Deg;

        if (fpc != null)
        {
            float yaw = Mathf.LerpAngle(fpc.Yaw, desiredYaw, Time.deltaTime * lerpSpeed);
            float pitch = Mathf.LerpAngle(fpc.Pitch, desiredPitch, Time.deltaTime * lerpSpeed);
            fpc.SetViewAngles(yaw, pitch);
        }
        else
        {
            playerCam.transform.rotation = Quaternion.Slerp(playerCam.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * lerpSpeed);
        }
    }

    private Vector3 GetFlatForward(Camera cam)
    {
        if (cam == null) return Vector3.forward;
        Vector3 flatFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = cam.transform.forward;
        return flatFwd.normalized;
    }

    private GameObject SpawnScareFigureFromTemplate(GameObject template, Camera playerCam, string objName, float forwardDist)
    {
        if (template == null) return null;

        GameObject clone = Instantiate(template);
        clone.name = objName;

        foreach (var qs in clone.GetComponentsInChildren<NpcQuestionSet>(true)) DestroyImmediate(qs);
        foreach (var conv in clone.GetComponentsInChildren<NpcConversation>(true)) DestroyImmediate(conv);
        foreach (var look in clone.GetComponentsInChildren<NpcLookAt>(true)) DestroyImmediate(look);
        foreach (var col in clone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);

        foreach (var tr in clone.GetComponentsInChildren<Transform>(true))
        {
            tr.gameObject.layer = 3;
        }

        if (playerCam != null)
        {
            Vector3 flatFwd = GetFlatForward(playerCam);
            clone.transform.position = playerCam.transform.position + flatFwd * forwardDist + Vector3.down * 1.48f;
            clone.transform.rotation = Quaternion.LookRotation(-flatFwd, Vector3.up);
        }

        clone.SetActive(true);
        return clone;
    }

    private Light SpawnScareLight(Camera playerCam, Color col, float intensity, float range)
    {
        var lGo = new GameObject("ScareFaceLight_Temp");
        if (playerCam != null)
        {
            lGo.transform.position = playerCam.transform.position + playerCam.transform.forward * 0.35f;
        }
        var l = lGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.cullingMask = -1;
        return l;
    }

    private IEnumerator CollapseCameraToBlackRoutine(Camera playerCam, Vector3 camOrigLocalPos, float startFov, float duration)
    {
        float colElapsed = 0f;
        Quaternion camOrigLocalRot = playerCam != null ? playerCam.transform.localRotation : Quaternion.identity;
        while (colElapsed < duration)
        {
            colElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(colElapsed / duration));

            if (playerCam != null)
            {
                playerCam.transform.localPosition = Vector3.Lerp(camOrigLocalPos, camOrigLocalPos + new Vector3(0f, -1.05f, 0f), t);
                playerCam.transform.localRotation = Quaternion.Slerp(camOrigLocalRot, Quaternion.Euler(25f, 0f, 68f), t);
                playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, startFov, t);
            }

            if (FallCutscene.Instance != null)
            {
                FallCutscene.Instance.SetBlackOverlayAlpha(t);
            }

            yield return null;
        }

        if (FallCutscene.Instance != null)
        {
            FallCutscene.Instance.SetBlackOverlayAlpha(1f);
        }

        if (playerCam != null)
        {
            playerCam.transform.localPosition = Vector3.zero;
            playerCam.transform.localRotation = Quaternion.identity;
            playerCam.fieldOfView = startFov;
        }
    }

    private void SetAllStationLights(float platformMult, Color platformTint, float upperMult, Color upperTint)
    {
        for (int i = 0; i < _stationLights.Count; i++)
        {
            var s = _stationLights[i];
            if (s.Light == null) continue;
            if (s.IsUpperGuide)
            {
                s.Light.intensity = s.BaseIntensity * upperMult;
                s.Light.color = s.BaseColor * upperTint;
            }
            else
            {
                s.Light.intensity = s.BaseIntensity * platformMult;
                s.Light.color = s.BaseColor * platformTint;
            }
        }
    }

    private void PlayOneShot(AudioSource src, AudioClip clip, float volume)
    {
        if (src != null && clip != null)
        {
            src.PlayOneShot(clip, volume);
        }
    }
}
