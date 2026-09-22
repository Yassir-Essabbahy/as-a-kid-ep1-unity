using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Credits
{
    public static class CreditsSceneBuilder
    {
        [MenuItem("Tools/Build Credits Scene")]
        public static void Build()
        {
            Debug.Log("[CreditsSceneBuilder] Starting End-Credits scene construction...");

            // 1. Create new empty scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string scenePath = "Assets/Scenes/Credits.unity";

            // 2. Load Fonts & Assets
            TMP_FontAsset geistFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/GeistPixel-Regular-VariableFont_ELSH SDF.asset");
            AudioClip musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/CreditsMusic.mp3");
            AudioClip railwayClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Train/Railway.mp3");
            Material sandMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/MoroccanBeach/Materials/tex_moroccan_sand.mat");
            Material oceanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imports/ocean_surface/Mat_OceanSurface.mat");
            Material clockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Assets/Episode 1/Beach/Objects/Materials/clock_broken_glass.mat");

            GameObject metroStationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MetroStationEnv.prefab");
            GameObject metroTrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MetroTrainFull.prefab");
            GameObject schoolModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/Episode 1/School.fbx");
            GameObject parasolModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/Episode 1/Beach/Objects/parasol.blend");
            GameObject chairsTablesModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/Episode 1/Beach/Objects/DirtyPlasticChairs&Tables.fbx");
            GameObject teddyModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/Episode 1/Characters/Doll/Post.fbx");
            GameObject raftModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/Episode 1/Beach/raft.blend");

            Mesh clockMesh = null;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath("Assets/Assets/Episode 1/Beach/Objects/Clocks.fbx"))
            {
                if (sub is Mesh m && m.name == "Wall Clock.008")
                {
                    clockMesh = m;
                    break;
                }
            }

            // 3. Setup Main Camera & CinemachineBrain
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<UniversalAdditionalCameraData>();
            CinemachineBrain brain = cameraObj.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            // Configure Cozy Sunset Skybox & Atmospheric Environment
            Material cozySkyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/MoroccanBeach/Materials/Skybox_CozySunset.mat");
            if (cozySkyMat != null)
            {
                cozySkyMat.SetFloat("_Exposure", 0.85f);
                RenderSettings.skybox = cozySkyMat;
            }
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.88f, 0.76f, 0.65f, 1f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 250f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.42f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.30f, 0.25f);

            // 4. Setup AudioSource for Music
            GameObject audioObj = new GameObject("CreditsAudio");
            AudioSource musicSource = audioObj.AddComponent<AudioSource>();
            musicSource.clip = musicClip;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0f;

            // 5. Build Root Containers
            GameObject envRoot = new GameObject("--- Environments ---");

            // =========================================================================
            // VIGNETTE 1: Metro Platform & Moving Train Wipe Cut (Coords around 0, 0, 0)
            // =========================================================================
            GameObject metroRoot = new GameObject("Vignette_01_Metro");
            metroRoot.transform.SetParent(envRoot.transform, false);

            if (metroStationPrefab != null)
            {
                GameObject stInst = (GameObject)PrefabUtility.InstantiatePrefab(metroStationPrefab, metroRoot.transform);
                stInst.name = "MetroStationEnvironment";
            }

            CreditsTrainMover trainMover = null;
            if (metroTrainPrefab != null)
            {
                GameObject trainInst = (GameObject)PrefabUtility.InstantiatePrefab(metroTrainPrefab, metroRoot.transform);
                trainInst.name = "MetroTrain_Wipe";

                AudioSource trainAudio = trainInst.AddComponent<AudioSource>();
                trainAudio.clip = railwayClip;
                trainAudio.loop = true;
                trainAudio.spatialBlend = 0.5f;

                trainMover = trainInst.AddComponent<CreditsTrainMover>();
                trainMover.audioSource = trainAudio;
                trainMover.railwayAudio = railwayClip;
                trainMover.Initialize(
                    new Vector3(0f, 0f, -40f),
                    new Vector3(0f, 0f, 60f),
                    moveSpeed: 24f,
                    triggerProgress: 0.48f
                );
            }

            // Tunnel Black End-Caps to block skybox leaks down subway tunnels
            GameObject capFar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capFar.name = "MetroTunnelCap_Far";
            capFar.transform.SetParent(metroRoot.transform, false);
            capFar.transform.localPosition = new Vector3(-10f, 5f, 68f);
            capFar.transform.localScale = new Vector3(60f, 30f, 2f);
            Material unlitBlackMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            unlitBlackMat.color = Color.black;
            capFar.GetComponent<MeshRenderer>().sharedMaterial = unlitBlackMat;

            GameObject capNear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capNear.name = "MetroTunnelCap_Near";
            capNear.transform.SetParent(metroRoot.transform, false);
            capNear.transform.localPosition = new Vector3(-10f, 5f, -25f);
            capNear.transform.localScale = new Vector3(60f, 30f, 2f);
            capNear.GetComponent<MeshRenderer>().sharedMaterial = unlitBlackMat;

            // Metro Lighting Root
            GameObject metroLightsRoot = new GameObject("MetroLights");
            metroLightsRoot.transform.SetParent(metroRoot.transform, false);

            GameObject metroLight1 = new GameObject("MetroLight_Platform");
            metroLight1.transform.SetParent(metroLightsRoot.transform, false);
            metroLight1.transform.localPosition = new Vector3(-12.0f, 4.8f, 2.5f);
            Light mLight1 = metroLight1.AddComponent<Light>();
            mLight1.type = LightType.Point;
            mLight1.range = 25f;
            mLight1.intensity = 1.3f;
            mLight1.color = new Color(1f, 0.94f, 0.86f);

            GameObject metroLight2 = new GameObject("MetroLight_Track");
            metroLight2.transform.SetParent(metroLightsRoot.transform, false);
            metroLight2.transform.localPosition = new Vector3(-8.0f, 5.0f, 25.0f);
            Light mLight2 = metroLight2.AddComponent<Light>();
            mLight2.type = LightType.Point;
            mLight2.range = 30f;
            mLight2.intensity = 1.4f;
            mLight2.color = new Color(0.85f, 0.9f, 1f);

            GameObject metroCamObj = new GameObject("Vcam_01_Metro");
            metroCamObj.transform.SetParent(metroRoot.transform, false);
            metroCamObj.transform.localPosition = new Vector3(-12.2f, 3.6f, -1.5f);
            metroCamObj.transform.localRotation = Quaternion.Euler(-2f, 12f, 0f);
            CinemachineCamera vcamMetro = metroCamObj.AddComponent<CinemachineCamera>();
            vcamMetro.Priority.Value = 100;
            vcamMetro.Lens.FieldOfView = 50f;

            // =========================================================================
            // VIGNETTE 2: Moroccan Beach Table with Lone Teddy (Coords around 300, 0, 0)
            // =========================================================================
            GameObject beachRoot = new GameObject("Vignette_02_Beach");
            beachRoot.transform.SetParent(envRoot.transform, false);
            beachRoot.transform.position = new Vector3(300f, 0f, 0f);

            GameObject sandObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sandObj.name = "SandGround";
            sandObj.transform.SetParent(beachRoot.transform, false);
            sandObj.transform.localPosition = new Vector3(0f, 0f, 12f);
            sandObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sandObj.transform.localScale = new Vector3(80f, 80f, 1f);
            if (sandMat != null) sandObj.GetComponent<MeshRenderer>().sharedMaterial = sandMat;

            GameObject beachOcean = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beachOcean.name = "BeachOceanWater";
            beachOcean.transform.SetParent(beachRoot.transform, false);
            beachOcean.transform.localPosition = new Vector3(0f, -0.1f, 50f);
            beachOcean.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            beachOcean.transform.localScale = new Vector3(200f, 120f, 1f);
            if (oceanMat != null) beachOcean.GetComponent<MeshRenderer>().sharedMaterial = oceanMat;

            if (parasolModel != null)
            {
                GameObject parasol = (GameObject)PrefabUtility.InstantiatePrefab(parasolModel, beachRoot.transform);
                parasol.name = "Parasol";
                parasol.transform.localPosition = new Vector3(0.5f, 0.22f, 10.5f);
                parasol.transform.localRotation = Quaternion.Euler(0f, 0f, 347.2f);
                parasol.transform.localScale = Vector3.one * 1.1f;

                // Strip unwanted Blender lights (intensity 100 Lamp)
                foreach (var l in parasol.GetComponentsInChildren<Light>(true))
                {
                    Object.DestroyImmediate(l.gameObject);
                }
            }

            if (chairsTablesModel != null)
            {
                GameObject ct = (GameObject)PrefabUtility.InstantiatePrefab(chairsTablesModel, beachRoot.transform);
                ct.name = "TableAndChairs";
                ct.transform.localPosition = new Vector3(2.14f, 0.33f, 10f);
                ct.transform.localRotation = Quaternion.identity;
                ct.transform.localScale = Vector3.one;

                for (int i = 0; i < ct.transform.childCount; i++)
                {
                    var child = ct.transform.GetChild(i);
                    if (child.name == "Cube.002" || child.name == "Cube.008" || child.name == "Cube.010")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            if (teddyModel != null)
            {
                GameObject teddy = (GameObject)PrefabUtility.InstantiatePrefab(teddyModel, beachRoot.transform);
                teddy.name = "Teddy";
                teddy.transform.localPosition = new Vector3(0f, 1.92f, 9.9f);
                teddy.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
                teddy.transform.localScale = Vector3.one * 5f;
            }

            // Beach Lighting Root
            GameObject beachLightsRoot = new GameObject("BeachLights");
            beachLightsRoot.transform.SetParent(beachRoot.transform, false);

            GameObject beachLight = new GameObject("BeachSunsetLight");
            beachLight.transform.SetParent(beachLightsRoot.transform, false);
            beachLight.transform.localPosition = new Vector3(0f, 10f, 0f);
            Light bLight = beachLight.AddComponent<Light>();
            bLight.type = LightType.Directional;
            beachLight.transform.rotation = Quaternion.Euler(15f, 165f, 0f);
            bLight.intensity = 1.05f;
            bLight.color = new Color(1f, 0.85f, 0.70f);

            GameObject beachCamObj = new GameObject("Vcam_02_BeachTeddy");
            beachCamObj.transform.SetParent(beachRoot.transform, false);
            beachCamObj.transform.localPosition = new Vector3(-1.1f, 2.3f, 7.8f);
            beachCamObj.transform.localRotation = Quaternion.Euler(6f, 22f, 0f);
            CinemachineCamera vcamBeachTeddy = beachCamObj.AddComponent<CinemachineCamera>();
            vcamBeachTeddy.Priority.Value = 10;
            vcamBeachTeddy.Lens.FieldOfView = 38f;

            // =========================================================================
            // VIGNETTE 3: Empty Classroom Desks & WallClock (Coords around 600, 0, 0)
            // =========================================================================
            GameObject schoolRoot = new GameObject("Vignette_03_Classroom");
            schoolRoot.transform.SetParent(envRoot.transform, false);
            schoolRoot.transform.position = new Vector3(600f, 0f, 0f);

            if (schoolModel != null)
            {
                GameObject school = (GameObject)PrefabUtility.InstantiatePrefab(schoolModel, schoolRoot.transform);
                school.name = "ClassroomModel";
                school.transform.localPosition = Vector3.zero;
                school.transform.localRotation = Quaternion.identity;

                // Strip unwanted Blender lights (Point, Point.001)
                foreach (var l in school.GetComponentsInChildren<Light>(true))
                {
                    Object.DestroyImmediate(l.gameObject);
                }
            }

            // Classroom Ceiling (Cube to prevent backface culling and fully enclose classroom interior)
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "ClassroomCeiling";
            ceiling.transform.SetParent(schoolRoot.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, 6.0f, 0f);
            ceiling.transform.localScale = new Vector3(25f, 0.5f, 30f);
            Material ceilMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ceilMat.color = new Color(0.25f, 0.22f, 0.20f, 1f);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = ceilMat;

            GameObject clockObj = new GameObject("WallClock");
            clockObj.transform.SetParent(schoolRoot.transform, false);
            clockObj.transform.localPosition = new Vector3(5.24f, 4.14f, 0.59f);
            clockObj.transform.localRotation = Quaternion.Euler(270f, 270f, 0f);
            clockObj.transform.localScale = Vector3.one * 250f;
            var mf = clockObj.AddComponent<MeshFilter>();
            mf.sharedMesh = clockMesh;
            var mr = clockObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = clockMat;

            // Classroom Lighting Root
            GameObject classroomLightsRoot = new GameObject("ClassroomLights");
            classroomLightsRoot.transform.SetParent(schoolRoot.transform, false);

            GameObject classLight1 = new GameObject("ClassroomPointLight1");
            classLight1.transform.SetParent(classroomLightsRoot.transform, false);
            classLight1.transform.localPosition = new Vector3(-0.48f, 5.2f, 5.60f);
            Light cLight1 = classLight1.AddComponent<Light>();
            cLight1.type = LightType.Point;
            cLight1.range = 18f;
            cLight1.intensity = 1.5f;
            cLight1.color = new Color(1f, 0.92f, 0.82f);

            GameObject classLight2 = new GameObject("ClassroomPointLight2");
            classLight2.transform.SetParent(classroomLightsRoot.transform, false);
            classLight2.transform.localPosition = new Vector3(-0.48f, 5.2f, -4.49f);
            Light cLight2 = classLight2.AddComponent<Light>();
            cLight2.type = LightType.Point;
            cLight2.range = 18f;
            cLight2.intensity = 1.5f;
            cLight2.color = new Color(1f, 0.92f, 0.82f);

            GameObject windowSun = new GameObject("ClassroomWindowSun");
            windowSun.transform.SetParent(classroomLightsRoot.transform, false);
            windowSun.transform.localPosition = new Vector3(5f, 4f, 0f);
            Light wLight = windowSun.AddComponent<Light>();
            wLight.type = LightType.Directional;
            windowSun.transform.rotation = Quaternion.Euler(20f, 110f, 0f);
            wLight.intensity = 0.5f;
            wLight.color = new Color(1f, 0.85f, 0.70f);

            GameObject classCamObj = new GameObject("Vcam_03_Classroom");
            classCamObj.transform.SetParent(schoolRoot.transform, false);
            classCamObj.transform.localPosition = new Vector3(-2.4f, 2.1f, -5.0f);
            classCamObj.transform.localRotation = Quaternion.Euler(-6f, 52f, 0f);
            CinemachineCamera vcamClassroom = classCamObj.AddComponent<CinemachineCamera>();
            vcamClassroom.Priority.Value = 10;
            vcamClassroom.Lens.FieldOfView = 50f;

            // =========================================================================
            // VIGNETTE 4: Raft Drifting in Open Ocean (Coords around 900, 0, 0)
            // =========================================================================
            GameObject oceanRoot = new GameObject("Vignette_04_RaftOcean");
            oceanRoot.transform.SetParent(envRoot.transform, false);
            oceanRoot.transform.position = new Vector3(900f, 0f, 0f);

            GameObject vastOcean = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vastOcean.name = "VastOceanSurface";
            vastOcean.transform.SetParent(oceanRoot.transform, false);
            vastOcean.transform.localPosition = new Vector3(0f, 0f, 60f);
            vastOcean.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            vastOcean.transform.localScale = new Vector3(300f, 300f, 1f);
            if (oceanMat != null) vastOcean.GetComponent<MeshRenderer>().sharedMaterial = oceanMat;

            CreditsRaftBobber raftBobber = null;
            if (raftModel != null)
            {
                GameObject raft = (GameObject)PrefabUtility.InstantiatePrefab(raftModel, oceanRoot.transform);
                raft.name = "DriftingRaft";
                raft.transform.localPosition = new Vector3(0f, 0.2f, 22f);
                raft.transform.localRotation = Quaternion.Euler(270.02f, 335.00f, 0f);
                raft.transform.localScale = Vector3.one * 1.2f;

                for (int i = 0; i < raft.transform.childCount; i++)
                {
                    raft.transform.GetChild(i).gameObject.SetActive(true);
                }

                raftBobber = raft.AddComponent<CreditsRaftBobber>();
                raftBobber.driftDirection = Vector3.forward;
                raftBobber.driftSpeed = 0.9f;
                raftBobber.waveFrequency = 1.3f;
                raftBobber.waveAmplitude = 0.08f;
            }

            // Ocean Lighting Root
            GameObject oceanLightsRoot = new GameObject("OceanLights");
            oceanLightsRoot.transform.SetParent(oceanRoot.transform, false);

            GameObject oceanLight = new GameObject("OceanTwilightLight");
            oceanLight.transform.SetParent(oceanLightsRoot.transform, false);
            oceanLight.transform.localPosition = new Vector3(0f, 15f, 0f);
            Light oLight = oceanLight.AddComponent<Light>();
            oLight.type = LightType.Directional;
            oceanLight.transform.rotation = Quaternion.Euler(16f, 205f, 0f);
            oLight.intensity = 0.95f;
            oLight.color = new Color(0.85f, 0.88f, 1.0f);

            GameObject raftCamObj = new GameObject("Vcam_04_RaftOcean");
            raftCamObj.transform.SetParent(oceanRoot.transform, false);
            raftCamObj.transform.localPosition = new Vector3(-2.0f, 2.3f, 12.0f);
            raftCamObj.transform.localRotation = Quaternion.Euler(6f, 13f, 0f);
            CinemachineCamera vcamRaftOcean = raftCamObj.AddComponent<CinemachineCamera>();
            vcamRaftOcean.Priority.Value = 10;
            vcamRaftOcean.Lens.FieldOfView = 44f;

            // =========================================================================
            // 6. UI CANVAS: Letterbox Bands, Credits Text, and Screen Fader
            // =========================================================================
            GameObject canvasObj = new GameObject("CreditsCanvas", typeof(RectTransform));
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Letterbox Bands Group (Full screen stretched)
            GameObject bandsObj = new GameObject("LetterboxBands", typeof(RectTransform));
            bandsObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bandsRect = bandsObj.GetComponent<RectTransform>();
            bandsRect.anchorMin = Vector2.zero;
            bandsRect.anchorMax = Vector2.one;
            bandsRect.offsetMin = Vector2.zero;
            bandsRect.offsetMax = Vector2.zero;
            CanvasGroup bandsGroup = bandsObj.AddComponent<CanvasGroup>();

            // Top Band (140px)
            GameObject topBandObj = new GameObject("TopBand", typeof(RectTransform));
            topBandObj.transform.SetParent(bandsObj.transform, false);
            RectTransform topRect = topBandObj.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 140f);
            Image topImg = topBandObj.AddComponent<Image>();
            topImg.color = Color.black;

            // Bottom Band (240px)
            GameObject bottomBandObj = new GameObject("BottomBand", typeof(RectTransform));
            bottomBandObj.transform.SetParent(bandsObj.transform, false);
            RectTransform bottomRect = bottomBandObj.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 240f);
            Image bottomImg = bottomBandObj.AddComponent<Image>();
            bottomImg.color = Color.black;

            // Credits Text Group (Positioned gracefully in the bottom band)
            GameObject creditsGroupObj = new GameObject("CreditTextGroup", typeof(RectTransform));
            creditsGroupObj.transform.SetParent(canvasObj.transform, false);
            RectTransform creditGroupRect = creditsGroupObj.GetComponent<RectTransform>();
            creditGroupRect.anchorMin = new Vector2(0f, 0f);
            creditGroupRect.anchorMax = new Vector2(1f, 0f);
            creditGroupRect.pivot = new Vector2(0.5f, 0f);
            creditGroupRect.anchoredPosition = new Vector2(0f, 35f);
            creditGroupRect.sizeDelta = new Vector2(0f, 180f);
            CanvasGroup creditTextGroup = creditsGroupObj.AddComponent<CanvasGroup>();

            // Credit Role Text
            GameObject roleObj = new GameObject("CreditRoleText", typeof(RectTransform));
            roleObj.transform.SetParent(creditsGroupObj.transform, false);
            RectTransform roleRect = roleObj.GetComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0f, 0.55f);
            roleRect.anchorMax = new Vector2(1f, 1f);
            roleRect.offsetMin = Vector2.zero;
            roleRect.offsetMax = Vector2.zero;
            TextMeshProUGUI roleTMP = roleObj.AddComponent<TextMeshProUGUI>();
            roleTMP.font = geistFont;
            roleTMP.fontSize = 22f;
            roleTMP.color = new Color(0.85f, 0.88f, 0.95f, 1f);
            roleTMP.alignment = TextAlignmentOptions.Center;
            roleTMP.characterSpacing = 6f;

            // Credit Name Text
            GameObject nameObj = new GameObject("CreditNameText", typeof(RectTransform));
            nameObj.transform.SetParent(creditsGroupObj.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0.55f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            TextMeshProUGUI nameTMP = nameObj.AddComponent<TextMeshProUGUI>();
            nameTMP.font = geistFont;
            nameTMP.fontSize = 36f;
            nameTMP.color = Color.white;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.lineSpacing = 14f;

            // Return Prompt Text
            GameObject promptObj = new GameObject("ReturnPromptText", typeof(RectTransform));
            promptObj.transform.SetParent(canvasObj.transform, false);
            RectTransform promptRect = promptObj.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0f, 0f);
            promptRect.anchorMax = new Vector2(1f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 85f);
            promptRect.sizeDelta = new Vector2(0f, 60f);
            TextMeshProUGUI promptTMP = promptObj.AddComponent<TextMeshProUGUI>();
            promptTMP.font = geistFont;
            promptTMP.fontSize = 22f;
            promptTMP.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            promptTMP.alignment = TextAlignmentOptions.Center;
            promptTMP.characterSpacing = 4f;

            // Full Screen Fader Group
            GameObject faderObj = new GameObject("ScreenFaderOverlay", typeof(RectTransform));
            faderObj.transform.SetParent(canvasObj.transform, false);
            RectTransform faderRect = faderObj.GetComponent<RectTransform>();
            faderRect.anchorMin = Vector2.zero;
            faderRect.anchorMax = Vector2.one;
            faderRect.offsetMin = Vector2.zero;
            faderRect.offsetMax = Vector2.zero;
            Image faderImg = faderObj.AddComponent<Image>();
            faderImg.color = Color.black;
            CanvasGroup faderGroup = faderObj.AddComponent<CanvasGroup>();
            faderGroup.alpha = 1f;

            // =========================================================================
            // 7. MASTER SEQUENCE CONTROLLER
            // =========================================================================
            GameObject masterCtrlObj = new GameObject("CreditsSequenceController");
            CreditsSequenceController ctrl = masterCtrlObj.AddComponent<CreditsSequenceController>();
            ctrl.cinemachineBrain = brain;
            ctrl.vcamMetro = vcamMetro;
            ctrl.vcamBeachTeddy = vcamBeachTeddy;
            ctrl.vcamClassroom = vcamClassroom;
            ctrl.vcamRaftOcean = vcamRaftOcean;

            ctrl.metroLightsRoot = metroLightsRoot;
            ctrl.beachLightsRoot = beachLightsRoot;
            ctrl.classroomLightsRoot = classroomLightsRoot;
            ctrl.oceanLightsRoot = oceanLightsRoot;

            ctrl.trainMover = trainMover;
            ctrl.raftBobber = raftBobber;

            ctrl.musicSource = musicSource;
            ctrl.creditsMusic = musicClip;

            ctrl.letterboxBands = bandsGroup;
            ctrl.screenFaderGroup = faderGroup;
            ctrl.creditTextGroup = creditTextGroup;
            ctrl.creditRoleText = roleTMP;
            ctrl.creditNameText = nameTMP;
            ctrl.returnPromptText = promptTMP;

            // 8. Save Scene
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreditsSceneBuilder] Scene successfully created and saved to {scenePath}!");
        }
    }
}
