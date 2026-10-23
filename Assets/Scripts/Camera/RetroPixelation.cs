using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Retro pixel look: the 3D scene is rendered at a fixed low vertical resolution and scaled up with
/// nearest-neighbour filtering (URP render scale + Point upscaling), so pixels stay the same size on any screen.
/// The UI is drawn afterwards at full resolution and stays crisp. Lower resolution is also cheaper on phones.
/// The render pipeline asset's original render scale is restored when this component is disabled,
/// so playing in the editor does not leave the asset modified.
/// </summary>
public class RetroPixelation : MonoBehaviour
{
    [Tooltip("Vertical resolution of the 3D scene, in pixels. Lower = chunkier pixels.")]
    [SerializeField, Range(120, 1440)] private int targetHeight = 640;
    [SerializeField] private bool pixelate = true;

    private UniversalRenderPipelineAsset pipeline;
    private float originalScale = 1f;
    private UpscalingFilterSelection originalFilter;
    private int lastScreenHeight;

    private void OnEnable()
    {
        pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return;

        originalScale = pipeline.renderScale;
        originalFilter = pipeline.upscalingFilter;
        lastScreenHeight = 0;
        Apply();
    }

    private void OnDisable()
    {
        if (pipeline == null) return;
        pipeline.renderScale = originalScale;
        pipeline.upscalingFilter = originalFilter;
    }

    private void Update()
    {
        if (pipeline != null && Screen.height != lastScreenHeight) Apply();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && pipeline != null) Apply();
    }

    private void Apply()
    {
        lastScreenHeight = Screen.height;
        if (!pixelate)
        {
            pipeline.renderScale = originalScale;
            pipeline.upscalingFilter = originalFilter;
            return;
        }

        pipeline.upscalingFilter = UpscalingFilterSelection.Point;
        pipeline.renderScale = Mathf.Clamp(targetHeight / (float)Mathf.Max(1, Screen.height), 0.1f, 1f);
    }
}
