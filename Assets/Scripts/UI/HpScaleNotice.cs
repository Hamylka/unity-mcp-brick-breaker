using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Subtle one-line notice ("DIFFICULTY INCREASING!") that fades in and out when <see cref="BrickGrid"/> scales up the
/// brick HP window. Small on purpose: the bricks already recolor with a pulse, this just says why.
/// Creates itself when the game starts, so no scene setup is needed.
/// </summary>
public class HpScaleNotice : MonoBehaviour
{
    private const string Message = "DIFFICULTY INCREASING!";
    private const float PixelSize = 5f;
    private const float AnchorY = 0.88f;     // fraction of the screen height: just under the XP bar, above the bricks
    private const float FadeIn = 0.25f;
    private const float Hold = 1.8f;
    private const float FadeOut = 0.7f;
    private const float Drift = 24f;         // pixels it floats upward over its life
    private const float PeakAlpha = 1f;
    private const float PopScale = 0.35f;    // extra scale at the start, settles to 1 during the fade in

    // Hot amber: a warning color that stands out on the dark scene and the cyan/magenta UI.
    private static readonly Color NoticeColor = new(1f, 0.62f, 0.12f);

    private BrickGrid grid;
    private RectTransform textRect;
    private PixelText text;
    private float time = float.MaxValue;     // seconds since the notice started; "done" when past the full life

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Create();
        // The scene is reloaded by the Restart button: bring the notice back with it.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Create();
    }

    private static void Create()
    {
        if (FindAnyObjectByType<HpScaleNotice>() == null)
            new GameObject("HpScaleNotice").AddComponent<HpScaleNotice>();
    }

    private void Awake() => BuildCanvas();

    private void Start()
    {
        grid = FindAnyObjectByType<BrickGrid>();
        if (grid != null) grid.HpScaled += OnHpScaled;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.HpScaled -= OnHpScaled;
    }

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("HpScaleCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // under the level-up and game over screens

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        var go = new GameObject("Notice", typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelText));
        go.transform.SetParent(canvasObject.transform, false);
        text = go.GetComponent<PixelText>();
        text.PixelSize = PixelSize;
        text.Alignment = PixelText.Align.Center;
        text.Text = Message;
        text.color = Color.clear;
        text.raycastTarget = false;

        textRect = (RectTransform)go.transform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, AnchorY);
        textRect.sizeDelta = new Vector2(900f, 60f);
        textRect.anchoredPosition = Vector2.zero;
    }

    private void OnHpScaled(int minHp, int maxHp)
    {
        if (grid != null && grid.IsGameOver) return;
        time = 0f;
    }

    private void Update()
    {
        float life = FadeIn + Hold + FadeOut;
        if (time >= life) return;

        time += Time.unscaledDeltaTime; // keeps playing while the game is slowed for a level-up

        float alpha;
        if (time < FadeIn) alpha = time / FadeIn;
        else if (time < FadeIn + Hold) alpha = 1f;
        else alpha = 1f - (time - FadeIn - Hold) / FadeOut;
        alpha = Mathf.Clamp01(alpha) * PeakAlpha;

        Color c = NoticeColor;
        c.a = alpha;
        text.color = c;
        textRect.anchoredPosition = new Vector2(0f, Drift * Mathf.Clamp01(time / life));
        textRect.localScale = Vector3.one * (1f + PopScale * (1f - Mathf.Clamp01(time / FadeIn)));
    }
}
