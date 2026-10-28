using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GenerateCameraBodyUV
{
    private const string ModelPath = "Assets/Model/Null.fbx";
    private const string MeshPath = "Assets/Model/UMesh_PolySphere2_6_UV.asset";
    private const string MaterialPath = "Assets/Materials/Leather/beige_leather_5/beige_leather_5.mat";
    private const string ObjectPath = "Null/UMesh_PolySphere2_6";

    [MenuItem("Tools/Camera/Generate UV for UMesh_PolySphere2_6")]
    private static void Generate()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Generate camera UV", "Could not find Assets/Model/Null.fbx.", "OK");
            return;
        }

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            EditorUtility.DisplayDialog("Generate camera UV", "Unity has reimported the FBX with Read/Write enabled. Run Tools > Camera > Generate UV for UMesh_PolySphere2_6 again after import finishes.", "OK");
            return;
        }

        var shell = GameObject.Find(ObjectPath);
        var filter = shell == null ? null : shell.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Generate camera UV", "Could not find the camera body mesh in the active scene.", "OK");
            return;
        }

        var source = filter.sharedMesh;
        if (!source.isReadable)
        {
            EditorUtility.DisplayDialog("Generate camera UV", "The imported mesh is still not readable. Wait for Unity to finish importing and run the menu item again.", "OK");
            return;
        }

        if (source.uv != null && source.uv.Length == source.vertexCount)
        {
            EditorUtility.DisplayDialog("Generate camera UV", "This mesh already has a UV channel.", "OK");
            return;
        }

        var vertices = source.vertices;
        var normals = source.normals;
        if (normals.Length != vertices.Length)
        {
            EditorUtility.DisplayDialog("Generate camera UV", "The mesh does not have per-vertex normals, so a safe projection could not be generated.", "OK");
            return;
        }

        var bounds = source.bounds;
        var size = bounds.size;
        var scale = Mathf.Max(Mathf.Max(size.x, size.y), Mathf.Max(size.z, 0.0001f));
        var uv = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var normal = normals[i];
            var ax = Mathf.Abs(normal.x);
            var ay = Mathf.Abs(normal.y);
            var az = Mathf.Abs(normal.z);
            if (az >= ax && az >= ay)
                uv[i] = new Vector2((vertices[i].x - bounds.min.x) / scale, (vertices[i].y - bounds.min.y) / scale);
            else if (ax >= ay)
                uv[i] = new Vector2((vertices[i].z - bounds.min.z) / scale, (vertices[i].y - bounds.min.y) / scale);
            else
                uv[i] = new Vector2((vertices[i].x - bounds.min.x) / scale, (vertices[i].z - bounds.min.z) / scale);
        }

        var copy = Object.Instantiate(source);
        copy.name = "UMesh_PolySphere2_6_UV";
        copy.uv = uv;

        var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (savedMesh == null)
            AssetDatabase.CreateAsset(copy, MeshPath);
        else
        {
            EditorUtility.CopySerialized(copy, savedMesh);
            Object.DestroyImmediate(copy);
            EditorUtility.SetDirty(savedMesh);
        }

        filter.sharedMesh = savedMesh == null ? copy : savedMesh;
        var renderer = shell.GetComponent<Renderer>();
        var leather = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (renderer != null && leather != null)
            renderer.sharedMaterial = leather;

        EditorUtility.SetDirty(filter);
        if (renderer != null)
            EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(shell.scene);
        EditorSceneManager.SaveScene(shell.scene);
        EditorUtility.DisplayDialog("Generate camera UV", "UVs generated and saved on a separate mesh asset. The original FBX remains unchanged, and the standard beige leather material is assigned to the shell.", "OK");
    }
}
