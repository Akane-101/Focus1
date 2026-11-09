using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraFocusDrag))]
public sealed class CameraFocusDragEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        CameraFocusDrag drag = (CameraFocusDrag)target;
        string orderError = drag.OrderError();
        if (!string.IsNullOrEmpty(orderError))
        {
            EditorGUILayout.HelpBox(orderError, MessageType.Error);
        }
        else if (drag.EscapeDistance <= drag.SnapRange)
        {
            EditorGUILayout.HelpBox("脱离距离必须大于吸附范围，否则松手后会立刻被同一档位吸回去。", MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox("吸附范围和脱离距离都按 FocusTrack 宽度的比例计算。脱离距离要大于吸附范围，拖出吸附圈后才不会被同一档立刻吸回。", MessageType.Info);
        }

        if (GUILayout.Button("创建或补齐五个标记点"))
        {
            FocusTrackMarkerEditorUtility.EnsureMarkers(drag);
        }

        if (PrefabUtility.IsPartOfPrefabInstance(drag.gameObject))
        {
            if (GUILayout.Button("保存到共享 CameraFrameHud Prefab"))
            {
                GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(drag.gameObject);
                PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.UserAction);
            }
        }
    }

    private void OnSceneGUI()
    {
        CameraFocusDrag drag = (CameraFocusDrag)target;
        DrawHandle(drag.LeftBound, FocusTrackMarker.Kind.LeftBound);
        DrawHandle(drag.NearStop, FocusTrackMarker.Kind.Near);
        DrawHandle(drag.MidStop, FocusTrackMarker.Kind.Mid);
        DrawHandle(drag.FarStop, FocusTrackMarker.Kind.Far);
        DrawHandle(drag.RightBound, FocusTrackMarker.Kind.RightBound);
    }

    private static void DrawHandle(RectTransform marker, FocusTrackMarker.Kind kind)
    {
        if (marker == null)
        {
            return;
        }

        RectTransform track = marker.parent as RectTransform;
        if (track == null)
        {
            return;
        }

        Handles.color = FocusTrackMarker.ColorFor(kind);
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(marker.position, 12f, Vector3.zero, Handles.SphereHandleCap);
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        Undo.RecordObject(marker, "Move Focus Marker");
        Vector3 local = track.InverseTransformPoint(moved);
        float normalized = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
        marker.anchorMin = new Vector2(Mathf.Clamp01(normalized), 0.5f);
        marker.anchorMax = new Vector2(Mathf.Clamp01(normalized), 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(18f, 18f);
        EditorUtility.SetDirty(marker);
    }
}

public static class FocusTrackMarkerEditorUtility
{
    private static readonly string[] Names =
    {
        "LeftBound", "NearStop", "MidStop", "FarStop", "RightBound"
    };

    private static readonly float[] Defaults = { 0.24f, 0.37f, 0.50f, 0.62f, 0.74f };

    private static readonly FocusTrackMarker.Kind[] Kinds =
    {
        FocusTrackMarker.Kind.LeftBound,
        FocusTrackMarker.Kind.Near,
        FocusTrackMarker.Kind.Mid,
        FocusTrackMarker.Kind.Far,
        FocusTrackMarker.Kind.RightBound
    };

    public static void EnsureMarkers(CameraFocusDrag drag)
    {
        RectTransform track = drag.transform as RectTransform;
        Undo.RegisterFullObjectHierarchyUndo(track.gameObject, "Create Focus Markers");
        RectTransform[] markers = new RectTransform[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            markers[i] = CreateOrUpdate(track, Names[i], Defaults[i], Kinds[i]);
        }

        SerializedObject serialized = new SerializedObject(drag);
        serialized.FindProperty("leftBound").objectReferenceValue = markers[0];
        serialized.FindProperty("nearStop").objectReferenceValue = markers[1];
        serialized.FindProperty("midStop").objectReferenceValue = markers[2];
        serialized.FindProperty("farStop").objectReferenceValue = markers[3];
        serialized.FindProperty("rightBound").objectReferenceValue = markers[4];
        Transform handle = track.Find("FocusHandle");
        if (handle != null)
        {
            serialized.FindProperty("handle").objectReferenceValue = handle;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(drag);
    }

    private static RectTransform CreateOrUpdate(RectTransform track, string markerName, float normalized, FocusTrackMarker.Kind kind)
    {
        Transform existing = track.Find(markerName);
        RectTransform marker;
        if (existing == null)
        {
            GameObject markerObject = new GameObject(markerName, typeof(RectTransform));
            markerObject.layer = track.gameObject.layer;
            markerObject.transform.SetParent(track, false);
            marker = markerObject.GetComponent<RectTransform>();
        }
        else
        {
            marker = existing as RectTransform;
        }

        marker.anchorMin = new Vector2(normalized, 0.5f);
        marker.anchorMax = new Vector2(normalized, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(18f, 18f);
        FocusTrackMarker tag = marker.GetComponent<FocusTrackMarker>();
        if (tag == null)
        {
            tag = marker.gameObject.AddComponent<FocusTrackMarker>();
        }

        SerializedObject serialized = new SerializedObject(tag);
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.ApplyModifiedProperties();
        return marker;
    }
}
