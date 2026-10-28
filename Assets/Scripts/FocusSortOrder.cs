using UnityEngine;

[DisallowMultipleComponent]
public sealed class FocusSortOrder : MonoBehaviour
{
    [SerializeField] [Tooltip("Added to the mid-layer base order (20). Negative draws behind other mid objects.")]
    private int orderOffset;
    [SerializeField] [Tooltip("Draw this object in front of the player while focused on the mid layer.")]
    private bool inFrontOfPlayer;

    public int GetOrderOffset()
    {
        if (inFrontOfPlayer)
        {
            return FocusLevelState.PlayerOrderOffset + 1 + orderOffset;
        }

        return orderOffset;
    }

    public static int Resolve(Transform current, int defaultOffset)
    {
        FocusSortOrder sortOrder = FindOnAncestors(current);
        return sortOrder != null ? sortOrder.GetOrderOffset() : defaultOffset;
    }

    public static FocusSortOrder FindOnAncestors(Transform current)
    {
        while (current != null)
        {
            FocusSortOrder sortOrder = current.GetComponent<FocusSortOrder>();

            if (sortOrder != null)
            {
                return sortOrder;
            }

            if (current.GetComponent<FocusLayerView>() != null)
            {
                return null;
            }

            current = current.parent;
        }

        return null;
    }
}
