using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class FocusGlareOverlay : MonoBehaviour
{
    [SerializeField] private Color glareColor = new Color(1f, 0.92f, 0.72f, 1f);

    private Material glareMaterial;

    public float Intensity { get; set; }

    public static FocusGlareOverlay EnsureOnMainCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            return null;
        }

        FocusGlareOverlay overlay = mainCamera.GetComponent<FocusGlareOverlay>();

        if (overlay == null)
        {
            overlay = mainCamera.gameObject.AddComponent<FocusGlareOverlay>();
        }

        return overlay;
    }

    private void Awake()
    {
        Shader glareShader = Shader.Find("Hidden/FocusGlareFX");

        if (glareShader == null)
        {
            Debug.LogError("Missing Hidden/FocusGlareFX shader.");
            enabled = false;
            return;
        }

        glareMaterial = new Material(glareShader);
        glareMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (glareMaterial == null || Intensity <= 0.001f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        glareMaterial.SetColor("_Color", glareColor);
        glareMaterial.SetFloat("_Intensity", Intensity);
        Graphics.Blit(source, destination, glareMaterial);
    }

    private void OnDestroy()
    {
        if (glareMaterial != null)
        {
            Destroy(glareMaterial);
        }
    }
}
