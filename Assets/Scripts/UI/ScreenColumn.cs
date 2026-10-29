using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The playfield column: a centered 9:16 strip of the screen that holds the game on any screen shape.
/// On a portrait phone (9:16 or taller) the column is the whole width, so nothing changes. On a wider screen
/// (Windows in landscape, tablets) the column keeps the phone layout in the middle and the sides are left free.
/// The camera, the paddle drag and every HUD use the same rule, so they always line up.
/// Add the component to a RectTransform under a canvas: it stretches it over the column, inside the safe area,
/// and follows window resizes.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class ScreenColumn : MonoBehaviour
{
    /// <summary>Width / height of the column. Matches the 1080x1920 reference layout of every canvas.</summary>
    public const float Aspect = 9f / 16f;

    /// <summary>Reference resolution shared by all the canvases.</summary>
    public static readonly Vector2 ReferenceResolution = new(1080f, 1920f);

    /// <summary>Column width in screen pixels (the full width on portrait phones).</summary>
    public static float PixelWidth => Mathf.Min(Screen.width, Screen.height * Aspect);

    /// <summary>True when the screen is wider than the column, so there is free space on the sides.</summary>
    public static bool HasSideSpace => Screen.width > Screen.height * Aspect + 0.5f;

    /// <summary>Column in screen pixels, centered, full height.</summary>
    public static Rect PixelRect
    {
        get
        {
            float width = PixelWidth;
            return new Rect((Screen.width - width) * 0.5f, 0f, width, Screen.height);
        }
    }

    /// <summary>
    /// Scales a canvas so the 1080x1920 reference always fits on screen: on portrait phones it matches the width
    /// (taller phones just get more height, as before), on wider screens it matches the height, so the reference
    /// layout fills exactly the column. Expand mode (the smaller of the two scales, so the canvas area grows to
    /// contain the reference) does both and keeps working when the window is resized.
    /// </summary>
    public static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    /// <summary>Creates a stretched child rect that follows the column, for HUD parts anchored to the screen edges.</summary>
    public static RectTransform Create(Transform canvas, string name = "Column")
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.AddComponent<ScreenColumn>().Apply();
        return (RectTransform)go.transform;
    }

    [Tooltip("Also keep out of the notch / rounded corners (Screen.safeArea).")]
    [SerializeField] private bool useSafeArea = true;

    private RectTransform rect;
    private Rect lastSafeArea;
    private Vector2Int lastScreen;

    private void Awake() => Apply();

    private void LateUpdate()
    {
        if (Screen.width != lastScreen.x || Screen.height != lastScreen.y || Screen.safeArea != lastSafeArea) Apply();
    }

    private void Apply()
    {
        if (rect == null) rect = (RectTransform)transform;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;

        Rect area = PixelRect;
        if (useSafeArea)
        {
            float xMin = Mathf.Max(area.xMin, lastSafeArea.xMin);
            float xMax = Mathf.Min(area.xMax, lastSafeArea.xMax);
            float yMin = Mathf.Max(area.yMin, lastSafeArea.yMin);
            float yMax = Mathf.Min(area.yMax, lastSafeArea.yMax);
            area = Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        }

        float width = Mathf.Max(1, Screen.width);
        float height = Mathf.Max(1, Screen.height);
        rect.anchorMin = new Vector2(area.xMin / width, area.yMin / height);
        rect.anchorMax = new Vector2(area.xMax / width, area.yMax / height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
