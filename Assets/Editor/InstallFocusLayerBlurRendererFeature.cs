using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class InstallFocusLayerBlurRendererFeature
{
    private const string RendererPath = "Assets/Samples/Universal RP/14.0.12/URP Package Samples/SharedAssets/Settings/SamplesForwardRenderer.asset";

    [MenuItem("Tools/Focus/Install URP Layer Blur")]
    public static void Install()
    {
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            Debug.LogError("Could not find the active URP renderer: " + RendererPath);
            return;
        }

        foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
        {
            if (existing is FocusLayerBlurRendererFeature)
            {
                existing.SetActive(true);
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                Debug.Log("URP layer blur is already installed and has been enabled.");
                return;
            }
        }

        FocusLayerBlurRendererFeature feature = ScriptableObject.CreateInstance<FocusLayerBlurRendererFeature>();
        feature.name = "FocusLayerBlurRendererFeature";
        feature.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/FocusLayerFX.shader");
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        rendererData.rendererFeatures.Add(feature);

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
        SerializedObject serializedRenderer = new SerializedObject(rendererData);
        SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        if (featureMap != null)
        {
            featureMap.arraySize = rendererData.rendererFeatures.Count;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        rendererData.SetDirty();
        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(feature);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed URP layer blur on " + rendererData.name + ".");
    }
}
