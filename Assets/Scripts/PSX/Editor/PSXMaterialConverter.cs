#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PSXRetro.Editor
{
    public static class PSXMaterialConverter
    {
        [MenuItem("Tools/PSX Retro/1. Setup PSX Renderers (Remove VHS)", false, 1)]
        public static void SetupPSXRenderers()
        {
            string[] rendererGuids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            int updatedCount = 0;

            Shader psxPostShader = Shader.Find("Hidden/PSX/PSX_PostProcess");

            foreach (string guid in rendererGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null) continue;

                bool modified = false;

                // 1. Remove or disable VHS features
                for (int i = rendererData.rendererFeatures.Count - 1; i >= 0; i--)
                {
                    var feat = rendererData.rendererFeatures[i];
                    if (feat != null && (feat.name.Contains("VHS") || feat.GetType().Name.Contains("VHS")))
                    {
                        Undo.RegisterCompleteObjectUndo(rendererData, "Remove VHS Renderer Feature");
                        feat.SetActive(false);
                        rendererData.rendererFeatures.RemoveAt(i);
                        ScriptableObject.DestroyImmediate(feat, true);
                        modified = true;
                        Debug.Log($"Removed VHS feature from {rendererData.name}");
                    }
                }

                // 2. Ensure PSXRenderFeature exists
                bool hasPSXFeature = false;
                foreach (var feat in rendererData.rendererFeatures)
                {
                    if (feat is PSXRenderFeature)
                    {
                        hasPSXFeature = true;
                        feat.SetActive(true);
                        break;
                    }
                }

                if (!hasPSXFeature)
                {
                    Undo.RegisterCompleteObjectUndo(rendererData, "Add PSX Renderer Feature");
                    var psxFeat = ScriptableObject.CreateInstance<PSXRenderFeature>();
                    psxFeat.name = "PSXRenderFeature";
                    psxFeat.settings.postProcessShader = psxPostShader;
                    psxFeat.settings.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                    psxFeat.settings.applyInSceneView = true;

                    AssetDatabase.AddObjectToAsset(psxFeat, rendererData);
                    rendererData.rendererFeatures.Add(psxFeat);
                    psxFeat.SetActive(true);
                    modified = true;
                    Debug.Log($"Added PSXRenderFeature to {rendererData.name}");
                }

                if (modified)
                {
                    EditorUtility.SetDirty(rendererData);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"SetupPSXRenderers completed. Updated {updatedCount} renderer assets.");
        }

        [MenuItem("Tools/PSX Retro/2. Ensure PSX System in Active Scene", false, 2)]
        public static void EnsurePSXSystemInActiveScene()
        {
            var system = Object.FindAnyObjectByType<PSXSystem>();
            if (system == null)
            {
                GameObject go = new GameObject("[PSX_System]");
                system = go.AddComponent<PSXSystem>();
                Undo.RegisterCreatedObjectUndo(go, "Create [PSX_System]");
                Debug.Log("Created [PSX_System] in active scene.");
            }
            system.UpdateShaderGlobals();
            EditorUtility.SetDirty(system);
        }

        [MenuItem("Tools/PSX Retro/3. Convert Selected Materials to PSX Lit", false, 3)]
        public static void ConvertSelectedMaterials()
        {
            Shader psxLit = Shader.Find("PSX/PSX_Lit");
            if (psxLit == null)
            {
                Debug.LogError("Shader PSX/PSX_Lit not found!");
                return;
            }

            Object[] selected = Selection.GetFiltered(typeof(Material), SelectionMode.Assets);
            int count = 0;
            foreach (Object obj in selected)
            {
                if (obj is Material mat)
                {
                    if (ConvertMaterialToPSX(mat, psxLit))
                        count++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Converted {count} selected materials to PSX/PSX_Lit.");
        }

        public static bool ConvertMaterialToPSX(Material mat, Shader psxLitShader)
        {
            if (mat == null || psxLitShader == null) return false;
            if (mat.shader == psxLitShader) return false;

            // Preserve existing textures and parameters
            Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") :
                             (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);

            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                             (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);

            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
            bool isAlphaClip = mat.IsKeywordEnabled("_ALPHATEST_ON");

            Undo.RecordObject(mat, "Convert to PSX Shader");
            mat.shader = psxLitShader;

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                // Also set texture filtering to Point if possible
                string texPath = AssetDatabase.GetAssetPath(baseMap);
                if (!string.IsNullOrEmpty(texPath))
                {
                    var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (importer != null && importer.filterMode != FilterMode.Point)
                    {
                        importer.filterMode = FilterMode.Point;
                        importer.SaveAndReimport();
                    }
                }
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_EmissionColor", emissionColor);
            mat.SetFloat("_Cutoff", cutoff);

            mat.SetFloat("_EnableJitter", 1.0f);
            mat.SetFloat("_JitterScale", 1.0f);
            mat.SetFloat("_EnableAffine", 1.0f);
            mat.SetFloat("_AffineStrength", 0.35f);
            mat.SetFloat("_VertexLighting", 1.0f);

            mat.EnableKeyword("_VERTEX_JITTER");
            mat.EnableKeyword("_AFFINE_WARP");
            mat.EnableKeyword("_VERTEX_LIGHTING");

            if (isAlphaClip)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 1.0f);
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 0.0f);
            }

            EditorUtility.SetDirty(mat);
            return true;
        }

        public static int ConvertMaterialsInFolder(string folderPath)
        {
            Shader psxLit = Shader.Find("PSX/PSX_Lit");
            if (psxLit == null) return 0;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            int converted = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    // Skip skybox, UI, font, or underwater specific materials
                    string nameLower = mat.name.ToLower();
                    if (nameLower.Contains("skybox") || nameLower.Contains("font") || nameLower.Contains("underwater") || nameLower.Contains("vhs"))
                        continue;

                    if (ConvertMaterialToPSX(mat, psxLit))
                        converted++;
                }
            }
            AssetDatabase.SaveAssets();
            return converted;
        }
    }
}
#endif
