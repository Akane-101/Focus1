using UnityEngine;

public static class FocusUnityLayers
{
    public const string Far = "FocusFar";
    public const string Mid = "FocusMid";
    public const string Near = "FocusNear";
    public const string Player = "FocusPlayer";

    public static int GetIndex(FocusLayer layer)
    {
        switch (layer)
        {
            case FocusLayer.Near:
                return LayerMask.NameToLayer(Near);
            case FocusLayer.Mid:
                return LayerMask.NameToLayer(Mid);
            default:
                return LayerMask.NameToLayer(Far);
        }
    }

    public static int GetCullingMask()
    {
        int far = LayerMask.NameToLayer(Far);
        int mid = LayerMask.NameToLayer(Mid);
        int near = LayerMask.NameToLayer(Near);
        int player = LayerMask.NameToLayer(Player);

        int mask = 0;

        if (far >= 0)
        {
            mask |= 1 << far;
        }

        if (mid >= 0)
        {
            mask |= 1 << mid;
        }

        if (near >= 0)
        {
            mask |= 1 << near;
        }

        if (player >= 0)
        {
            mask |= 1 << player;
        }

        return mask;
    }

    public static void Assign(Transform root, FocusLayer layer)
    {
        Assign(root, GetIndex(layer));
    }

    public static void Assign(Transform root, int unityLayerIndex)
    {
        if (root == null || unityLayerIndex < 0)
        {
            return;
        }

        AssignRecursive(root, unityLayerIndex);
    }

    private static void AssignRecursive(Transform root, int index)
    {
        root.gameObject.layer = index;

        for (int i = 0; i < root.childCount; i++)
        {
            AssignRecursive(root.GetChild(i), index);
        }
    }
}
