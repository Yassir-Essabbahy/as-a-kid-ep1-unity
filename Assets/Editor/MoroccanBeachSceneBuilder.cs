using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MoroccanBeach;

public static class MoroccanBeachSceneBuilder
{
    [MenuItem("Tools/Build Moroccan Beach Scene")]
    public static string Build()
    {
        // 1. Create directories
        string baseDir = "Assets/MoroccanBeach";
        string texDir = baseDir + "/Textures";
        string matDir = baseDir + "/Materials";
        string scenesDir = "Assets/Scenes";
        
        EnsureDir(baseDir);
        EnsureDir(texDir);
        EnsureDir(matDir);
        EnsureDir(scenesDir);

        // 2. Generate PSX Textures
        CreateSandTexture(texDir + "/tex_moroccan_sand.png");
        CreateCliffTexture(texDir + "/tex_moroccan_cliff.png");
        CreateWaterTexture(texDir + "/tex_moroccan_ocean.png");
        CreateWoodTexture(texDir + "/tex_weathered_wood.png");
        CreateBoatTexture(texDir + "/tex_moroccan_boat_blue.png");
        CreateStrawTexture(texDir + "/tex_straw_umbrella.png");
        CreateTowelTexture(texDir + "/tex_striped_towel.png");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Configure texture importers for PSX point filtering
        ConfigureTextureImporter(texDir + "/tex_moroccan_sand.png");
        ConfigureTextureImporter(texDir + "/tex_moroccan_cliff.png");
        ConfigureTextureImporter(texDir + "/tex_moroccan_ocean.png");
        ConfigureTextureImporter(texDir + "/tex_weathered_wood.png");
        ConfigureTextureImporter(texDir + "/tex_moroccan_boat_blue.png");
        ConfigureTextureImporter(texDir + "/tex_straw_umbrella.png");
        ConfigureTextureImporter(texDir + "/tex_striped_towel.png");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Load imported textures
        var texSand = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_moroccan_sand.png");
        var texCliff = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_moroccan_cliff.png");
        var texWater = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_moroccan_ocean.png");
        var texWood = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_weathered_wood.png");
        var texBoat = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_moroccan_boat_blue.png");
        var texStraw = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_straw_umbrella.png");
        var texTowel = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/tex_striped_towel.png");

        // 3. Create Materials (Simple Lit for retro feel)
        var shaderSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");

        var matSand = CreateMaterial(matDir + "/Mat_MoroccanSand.mat", shaderSimpleLit, texSand, new Color(0.72f, 0.58f, 0.44f));
        var matCliff = CreateMaterial(matDir + "/Mat_MoroccanCliff.mat", shaderSimpleLit, texCliff, new Color(0.65f, 0.50f, 0.38f));
        var matWater = CreateMaterial(matDir + "/Mat_MoroccanOcean.mat", shaderSimpleLit, texWater, new Color(0.18f, 0.28f, 0.38f));
        var matWood = CreateMaterial(matDir + "/Mat_WeatheredWood.mat", shaderSimpleLit, texWood, new Color(0.48f, 0.38f, 0.28f));
        var matBoat = CreateMaterial(matDir + "/Mat_MoroccanBoat.mat", shaderSimpleLit, texBoat, new Color(0.15f, 0.35f, 0.65f));
        var matStraw = CreateMaterial(matDir + "/Mat_StrawUmbrella.mat", shaderSimpleLit, texStraw, new Color(0.68f, 0.58f, 0.38f));
        var matTowel = CreateMaterial(matDir + "/Mat_BeachTowel.mat", shaderSimpleLit, texTowel, Color.white);

        // 4. Create New Scene
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Root container
        var root = new GameObject("=== MOROCCAN BEACH (PSX PLACEHOLDERS) ===");

        // --- LIGHTING & ATMOSPHERE ---
        var lightGroup = new GameObject("--- LIGHTING & ATMOSPHERE ---");
        lightGroup.transform.SetParent(root.transform);

        var sunObj = new GameObject("Directional_Light_Twilight");
        sunObj.transform.SetParent(lightGroup.transform);
        var sunLight = sunObj.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(0.88f, 0.62f, 0.52f); // Warm twilight amber
        sunLight.intensity = 0.55f;
        sunLight.shadows = LightShadows.Soft;
        sunObj.transform.rotation = Quaternion.Euler(14f, 240f, 0f);

        // Ambience & Scene Controller
        var atmosObj = new GameObject("PSX_Atmosphere_Controller");
        atmosObj.transform.SetParent(lightGroup.transform);
        var atmosCtrl = atmosObj.AddComponent<PSXSceneController>();
        atmosCtrl.twilightFogColor = new Color(0.11f, 0.13f, 0.21f, 1f); // Deep indigo twilight
        atmosCtrl.ambientSkyColor = new Color(0.18f, 0.20f, 0.30f, 1f);
        atmosCtrl.fogStart = 15f;
        atmosCtrl.fogEnd = 85f;
        atmosCtrl.ApplyAtmosphere();

        // Audio Source
        var audioObj = new GameObject("Ocean_Waves_Audio");
        audioObj.transform.SetParent(lightGroup.transform);
        var audioSrc = audioObj.AddComponent<AudioSource>();
        var waveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MoroccanBeach/Audio/ocean_waves_twilight_loop.wav");
        if (waveClip != null)
        {
            audioSrc.clip = waveClip;
            audioSrc.loop = true;
            audioSrc.playOnAwake = true;
            audioSrc.volume = 0.65f;
        }
        atmosCtrl.oceanAudioSource = audioSrc;

        // --- ENVIRONMENT ---
        var envGroup = new GameObject("--- ENVIRONMENT ---");
        envGroup.transform.SetParent(root.transform);

        // 1. Sand Shore (sloping beach)
        var sandObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sandObj.name = "Sand_Beach_Cove";
        sandObj.transform.SetParent(envGroup.transform);
        sandObj.transform.position = new Vector3(10f, -0.4f, 0f);
        sandObj.transform.localScale = new Vector3(70f, 1f, 120f);
        sandObj.transform.rotation = Quaternion.Euler(0f, 0f, 1.5f); // Gentle slope toward ocean on -X
        sandObj.GetComponent<Renderer>().sharedMaterial = matSand;

        // 2. Ocean Water Plane
        var waterObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterObj.name = "Ocean_Water_Plane";
        waterObj.transform.SetParent(envGroup.transform);
        waterObj.transform.position = new Vector3(-35f, 0.05f, 0f);
        waterObj.transform.localScale = new Vector3(8f, 1f, 12f); // 80m x 120m
        waterObj.GetComponent<Renderer>().sharedMaterial = matWater;
        var waterComp = waterObj.AddComponent<PSXWaterSurface>();
        waterComp.uvScrollSpeed = new Vector2(0.02f, 0.05f);
        waterComp.waveHeight = 0.06f;

        // Remove collider from water plane so player can wade into the surf
        var waterCol = waterObj.GetComponent<Collider>();
        if (waterCol != null) UnityEngine.Object.DestroyImmediate(waterCol);

        // 3. Tide Pool / Lagoon (curved sandbar pool like user's photo)
        var tidePool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tidePool.name = "Tidal_Inlet_Pool";
        tidePool.transform.SetParent(envGroup.transform);
        tidePool.transform.position = new Vector3(12f, 0.12f, -15f);
        tidePool.transform.localScale = new Vector3(22f, 0.05f, 14f);
        tidePool.GetComponent<Renderer>().sharedMaterial = matWater;
        var tideCol = tidePool.GetComponent<Collider>();
        if (tideCol != null) UnityEngine.Object.DestroyImmediate(tideCol);

        // 4. Sandstone Cliff Wall (Framing the beach on the right, matching Moroccan coast)
        var cliffGroup = new GameObject("Cliff_Sandstone_Wall");
        cliffGroup.transform.SetParent(envGroup.transform);
        BuildCliffWall(cliffGroup.transform, matCliff);

        // 5. Sea Stack / Rock Pillar in Ocean (Iconic Moroccan coast feature like Legzira / Mirleft)
        var seaStack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seaStack.name = "Sea_Stack_Rock_Pillar";
        seaStack.transform.SetParent(envGroup.transform);
        seaStack.transform.position = new Vector3(-55f, 5f, 25f);
        seaStack.transform.localScale = new Vector3(10f, 12f, 8f);
        seaStack.transform.rotation = Quaternion.Euler(4f, 25f, -6f);
        seaStack.GetComponent<Renderer>().sharedMaterial = matCliff;

        // --- PLACEHOLDER PROPS (SWAP WITH BLENDER) ---
        var propGroup = new GameObject("--- PLACEHOLDER PROPS (SWAP WITH BLENDER) ---");
        propGroup.transform.SetParent(root.transform);

        // Prop 1: Moroccan Fishing Boat ("Fluka")
        CreateFishingBoat(propGroup.transform, new Vector3(2f, 0.45f, 8f), Quaternion.Euler(0f, 110f, -4f), matBoat, matWood);

        // Prop 2: Traditional Straw Umbrellas
        CreateStrawUmbrella(propGroup.transform, "[REPLACE]_BeachUmbrella_01", new Vector3(6f, 0.35f, -4f), matStraw, matWood);
        CreateStrawUmbrella(propGroup.transform, "[REPLACE]_BeachUmbrella_02", new Vector3(16f, 0.65f, 12f), matStraw, matWood);
        CreateStrawUmbrella(propGroup.transform, "[REPLACE]_BeachUmbrella_03", new Vector3(18f, 0.75f, -22f), matStraw, matWood);

        // Prop 3: Beach Towel / Mat
        var towel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        towel.name = "[REPLACE]_BeachTowel_Mat";
        towel.transform.SetParent(propGroup.transform);
        towel.transform.position = new Vector3(7.2f, 0.38f, -4.5f);
        towel.transform.rotation = Quaternion.Euler(90f, 25f, 0f);
        towel.transform.localScale = new Vector3(1.2f, 2.2f, 1f);
        towel.GetComponent<Renderer>().sharedMaterial = matTowel;

        // Prop 4: Lifeguard Lookout Tower
        CreateLifeguardTower(propGroup.transform, new Vector3(14f, 0.7f, 28f), matWood);

        // Prop 5: Solitary Wooden Bench
        CreateWoodenBench(propGroup.transform, new Vector3(24f, 1.0f, -5f), Quaternion.Euler(0f, 275f, 0f), matWood);

        // Prop 6: Boulder Clusters at cliff base
        CreateBoulderCluster(propGroup.transform, "[REPLACE]_CliffBoulder_Cluster_01", new Vector3(32f, 1.2f, 5f), matCliff);
        CreateBoulderCluster(propGroup.transform, "[REPLACE]_CliffBoulder_Cluster_02", new Vector3(28f, 1.0f, -32f), matCliff);

        // Prop 7: Driftwood Logs
        CreateDriftwood(propGroup.transform, new Vector3(-2f, 0.25f, -12f), matWood);

        // --- PLAYER ---
        var playerGroup = new GameObject("--- PLAYER ---");
        playerGroup.transform.SetParent(root.transform);

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab");
        if (playerPrefab != null)
        {
            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.name = "FirstPersonPlayer";
            playerInstance.transform.SetParent(playerGroup.transform);
            playerInstance.transform.position = new Vector3(8f, 1.8f, -8f); // On sand looking out at sea
            playerInstance.transform.rotation = Quaternion.Euler(0f, 260f, 0f); // Looking towards ocean & sunset
        }

        // 5. Save Scene
        string scenePath = scenesDir + "/MoroccanBeach.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);

        // Register in Build Settings if not already
        var existingScenes = EditorBuildSettings.scenes;
        bool alreadyInBuild = false;
        foreach (var s in existingScenes)
        {
            if (s.path == scenePath) { alreadyInBuild = true; break; }
        }
        if (!alreadyInBuild)
        {
            var newScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            existingScenes.CopyTo(newScenes, 0);
            newScenes[existingScenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }

        return "Successfully built MoroccanBeach.unity at " + scenePath;
    }

    private static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    private static void ConfigureTextureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point; // PSX point filtering
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
    }

    private static Material CreateMaterial(string path, Shader shader, Texture2D tex, Color tint)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.mainTexture = tex;
        mat.color = tint;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // --- TEXTURE GENERATORS (Authentic PSX 128x128 & 64x64) ---

    private static void CreateSandTexture(string path)
    {
        int w = 128, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var baseColor = new Color(0.76f, 0.62f, 0.46f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float noise = UnityEngine.Random.Range(-0.06f, 0.06f);
                float wave = Mathf.Sin((x + y * 0.3f) * 0.15f) * 0.03f;
                Color col = new Color(
                    Mathf.Clamp01(baseColor.r + noise + wave),
                    Mathf.Clamp01(baseColor.g + noise + wave * 0.8f),
                    Mathf.Clamp01(baseColor.b + noise * 0.7f),
                    1f
                );
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void CreateCliffTexture(string path)
    {
        int w = 128, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var stratum1 = new Color(0.60f, 0.46f, 0.34f);
        var stratum2 = new Color(0.50f, 0.38f, 0.28f);
        for (int y = 0; y < h; y++)
        {
            float band = Mathf.Sin(y * 0.22f) * 0.5f + 0.5f;
            Color baseBand = Color.Lerp(stratum1, stratum2, band);
            for (int x = 0; x < w; x++)
            {
                float n = UnityEngine.Random.Range(-0.05f, 0.05f);
                tex.SetPixel(x, y, new Color(
                    Mathf.Clamp01(baseBand.r + n),
                    Mathf.Clamp01(baseBand.g + n),
                    Mathf.Clamp01(baseBand.b + n * 0.8f),
                    1f
                ));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void CreateWaterTexture(string path)
    {
        int w = 128, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var deepWater = new Color(0.10f, 0.18f, 0.28f, 1f);
        var waveFoam = new Color(0.40f, 0.55f, 0.65f, 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float wave = Mathf.Sin(x * 0.18f + y * 0.25f) * Mathf.Cos(x * 0.12f - y * 0.2f);
                Color c = Color.Lerp(deepWater, waveFoam, Mathf.Clamp01(wave * 0.6f + 0.2f));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void CreateWoodTexture(string path)
    {
        int w = 64, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var woodLight = new Color(0.45f, 0.35f, 0.26f);
        var woodDark = new Color(0.32f, 0.24f, 0.18f);
        for (int y = 0; y < h; y++)
        {
            int plankIndex = y % 16;
            float plankEdge = (plankIndex == 0 || plankIndex == 15) ? 0.3f : 0f;
            for (int x = 0; x < w; x++)
            {
                float grain = Mathf.Sin(x * 0.8f) * 0.1f + UnityEngine.Random.Range(-0.03f, 0.03f) - plankEdge;
                Color c = Color.Lerp(woodDark, woodLight, Mathf.Clamp01(0.5f + grain));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void CreateBoatTexture(string path)
    {
        int w = 64, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var boatBlue = new Color(0.12f, 0.32f, 0.62f);
        var boatWhite = new Color(0.85f, 0.85f, 0.82f);
        for (int y = 0; y < h; y++)
        {
            Color c = (y > 48 || (y > 20 && y < 24)) ? boatWhite : boatBlue;
            for (int x = 0; x < w; x++)
            {
                float n = UnityEngine.Random.Range(-0.04f, 0.04f);
                tex.SetPixel(x, y, new Color(c.r + n, c.g + n, c.b + n, 1f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void CreateStrawTexture(string path)
    {
        int w = 64, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var strawLight = new Color(0.68f, 0.58f, 0.38f);
        var strawDark = new Color(0.48f, 0.38f, 0.24f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float weave = ((x + y) % 6 < 3) ? 0.3f : -0.2f;
                float n = UnityEngine.Random.Range(-0.05f, 0.05f);
                tex.SetPixel(x, y, Color.Lerp(strawDark, strawLight, Mathf.Clamp01(0.5f + weave + n)));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void CreateTowelTexture(string path)
    {
        int w = 32, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var stripe1 = new Color(0.82f, 0.28f, 0.24f);
        var stripe2 = new Color(0.92f, 0.88f, 0.80f);
        for (int y = 0; y < h; y++)
        {
            Color c = ((y / 8) % 2 == 0) ? stripe1 : stripe2;
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    // --- PROCEDURAL PLACEHOLDER BUILDERS ---

    private static void BuildCliffWall(Transform parent, Material mat)
    {
        float[] angles = new float[] { -40f, -25f, -10f, 5f, 20f, 35f, 50f, 65f };
        float radius = 55f;
        Vector3 center = new Vector3(-10f, 0f, 0f);

        for (int i = 0; i < angles.Length; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(rad) * radius;
            float z = center.z + Mathf.Sin(rad) * radius;
            float height = UnityEngine.Random.Range(14f, 24f);
            float width = UnityEngine.Random.Range(14f, 18f);

            var cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cliff.name = "Cliff_Segment_" + (i + 1).ToString("D2");
            cliff.transform.SetParent(parent);
            cliff.transform.position = new Vector3(x, height * 0.5f - 1f, z);
            cliff.transform.localScale = new Vector3(width, height, width);
            cliff.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-3f, 3f));
            cliff.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    private static void CreateFishingBoat(Transform parent, Vector3 pos, Quaternion rot, Material matBoat, Material matWood)
    {
        var boatRoot = new GameObject("[REPLACE]_MoroccanFishingBoat");
        boatRoot.transform.SetParent(parent);
        boatRoot.transform.position = pos;
        boatRoot.transform.rotation = rot;

        var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hull.name = "Boat_Hull";
        hull.transform.SetParent(boatRoot.transform);
        hull.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        hull.transform.localScale = new Vector3(2.2f, 0.8f, 5.5f);
        hull.GetComponent<Renderer>().sharedMaterial = matBoat;

        var bow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bow.name = "Boat_Bow";
        bow.transform.SetParent(boatRoot.transform);
        bow.transform.localPosition = new Vector3(0f, 0.5f, 3.2f);
        bow.transform.localScale = new Vector3(1.6f, 0.8f, 1.6f);
        bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        bow.GetComponent<Renderer>().sharedMaterial = matBoat;

        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.name = "Boat_Bench";
        seat.transform.SetParent(boatRoot.transform);
        seat.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        seat.transform.localScale = new Vector3(2.0f, 0.12f, 0.6f);
        seat.GetComponent<Renderer>().sharedMaterial = matWood;

        var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mast.name = "Boat_Mast";
        mast.transform.SetParent(boatRoot.transform);
        mast.transform.localPosition = new Vector3(0f, 1.8f, 0.5f);
        mast.transform.localScale = new Vector3(0.08f, 1.4f, 0.08f);
        mast.GetComponent<Renderer>().sharedMaterial = matWood;
    }

    private static void CreateStrawUmbrella(Transform parent, string name, Vector3 pos, Material matStraw, Material matWood)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = pos;

        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Umbrella_Pole";
        pole.transform.SetParent(root.transform);
        pole.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        pole.transform.localScale = new Vector3(0.08f, 1.25f, 0.08f);
        pole.GetComponent<Renderer>().sharedMaterial = matWood;

        var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.name = "Umbrella_StrawCanopy";
        top.transform.SetParent(root.transform);
        top.transform.localPosition = new Vector3(0f, 2.45f, 0f);
        top.transform.localScale = new Vector3(2.8f, 0.35f, 2.8f);
        top.GetComponent<Renderer>().sharedMaterial = matStraw;

        var peak = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        peak.name = "Umbrella_StrawPeak";
        peak.transform.SetParent(root.transform);
        peak.transform.localPosition = new Vector3(0f, 2.7f, 0f);
        peak.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);
        peak.GetComponent<Renderer>().sharedMaterial = matStraw;
    }

    private static void CreateLifeguardTower(Transform parent, Vector3 pos, Material matWood)
    {
        var root = new GameObject("[REPLACE]_Lifeguard_Lookout_Tower");
        root.transform.SetParent(parent);
        root.transform.position = pos;

        float w = 1.4f;
        Vector3[] stiltOffsets = new Vector3[] {
            new Vector3(-w, 1.8f, -w),
            new Vector3(w, 1.8f, -w),
            new Vector3(-w, 1.8f, w),
            new Vector3(w, 1.8f, w)
        };
        for (int i = 0; i < 4; i++)
        {
            var stilt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stilt.name = "Stilt_" + i;
            stilt.transform.SetParent(root.transform);
            stilt.transform.localPosition = stiltOffsets[i];
            stilt.transform.localScale = new Vector3(0.18f, 3.6f, 0.18f);
            stilt.GetComponent<Renderer>().sharedMaterial = matWood;
        }

        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "Lookout_Platform";
        deck.transform.SetParent(root.transform);
        deck.transform.localPosition = new Vector3(0f, 3.65f, 0f);
        deck.transform.localScale = new Vector3(3.2f, 0.2f, 3.2f);
        deck.GetComponent<Renderer>().sharedMaterial = matWood;

        var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Lookout_Cabin";
        cabin.transform.SetParent(root.transform);
        cabin.transform.localPosition = new Vector3(0f, 4.65f, 0f);
        cabin.transform.localScale = new Vector3(2.4f, 1.8f, 2.4f);
        cabin.GetComponent<Renderer>().sharedMaterial = matWood;

        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Lookout_Roof";
        roof.transform.SetParent(root.transform);
        roof.transform.localPosition = new Vector3(0f, 5.65f, 0f);
        roof.transform.localScale = new Vector3(3.4f, 0.2f, 3.4f);
        roof.GetComponent<Renderer>().sharedMaterial = matWood;
    }

    private static void CreateWoodenBench(Transform parent, Vector3 pos, Quaternion rot, Material matWood)
    {
        var root = new GameObject("[REPLACE]_Solitary_Bench");
        root.transform.SetParent(parent);
        root.transform.position = pos;
        root.transform.rotation = rot;

        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.name = "Bench_Seat";
        seat.transform.SetParent(root.transform);
        seat.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        seat.transform.localScale = new Vector3(1.8f, 0.1f, 0.5f);
        seat.GetComponent<Renderer>().sharedMaterial = matWood;

        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Bench_Backrest";
        back.transform.SetParent(root.transform);
        back.transform.localPosition = new Vector3(0f, 0.85f, -0.22f);
        back.transform.localScale = new Vector3(1.8f, 0.5f, 0.08f);
        back.GetComponent<Renderer>().sharedMaterial = matWood;

        for (int i = -1; i <= 1; i += 2)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "Bench_Leg";
            leg.transform.SetParent(root.transform);
            leg.transform.localPosition = new Vector3(i * 0.7f, 0.22f, 0f);
            leg.transform.localScale = new Vector3(0.1f, 0.45f, 0.45f);
            leg.GetComponent<Renderer>().sharedMaterial = matWood;
        }
    }

    private static void CreateBoulderCluster(Transform parent, string name, Vector3 pos, Material matCliff)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = pos;

        for (int i = 0; i < 4; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Boulder_" + i;
            rock.transform.SetParent(root.transform);
            rock.transform.localPosition = new Vector3(
                UnityEngine.Random.Range(-1.5f, 1.5f),
                UnityEngine.Random.Range(0.2f, 0.8f),
                UnityEngine.Random.Range(-1.5f, 1.5f)
            );
            float scale = UnityEngine.Random.Range(1.0f, 2.5f);
            rock.transform.localScale = new Vector3(scale * UnityEngine.Random.Range(0.8f, 1.2f), scale * 0.7f, scale * UnityEngine.Random.Range(0.8f, 1.2f));
            rock.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(10f, 35f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(10f, 35f));
            rock.GetComponent<Renderer>().sharedMaterial = matCliff;
        }
    }

    private static void CreateDriftwood(Transform parent, Vector3 pos, Material matWood)
    {
        var root = new GameObject("[REPLACE]_Driftwood_Logs");
        root.transform.SetParent(parent);
        root.transform.position = pos;

        var log1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log1.name = "Log_01";
        log1.transform.SetParent(root.transform);
        log1.transform.localPosition = Vector3.zero;
        log1.transform.localRotation = Quaternion.Euler(85f, 30f, 10f);
        log1.transform.localScale = new Vector3(0.25f, 1.8f, 0.25f);
        log1.GetComponent<Renderer>().sharedMaterial = matWood;

        var log2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        log2.name = "Log_02";
        log2.transform.SetParent(root.transform);
        log2.transform.localPosition = new Vector3(0.6f, 0.1f, -0.4f);
        log2.transform.localRotation = Quaternion.Euler(80f, -40f, 15f);
        log2.transform.localScale = new Vector3(0.18f, 1.2f, 0.18f);
        log2.GetComponent<Renderer>().sharedMaterial = matWood;
    }
}
