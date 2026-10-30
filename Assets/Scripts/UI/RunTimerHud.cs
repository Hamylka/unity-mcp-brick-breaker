using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Run clock (m:ss) centered right under the XP bar, in the same pixel font and dark-navy outline as the XP numbers.
/// Reads <see cref="GameManager.RunTime"/>, so it starts at the first launch and stops while paused or on a card screen.
/// Creates itself when the game starts, so no scene setup is needed.
/// </summary>
public class RunTimerHud : MonoBehaviour
{
    private const float PixelSize = 6f;
    private const float AnchorY = -170f;     // reference pixels below the top of the safe area (the XP bar ends at -124)

    // Pale cyan, a quieter cousin of the UI's cyan accents so it does not compete with the XP bar.
    private static readonly Color TextColor = new(0.72f, 0.95f, 1f, 0.9f);
    private static readonly Color OutlineColor = new(0.02f, 0.03f, 0.1f, 0.9f);

    private GameManager game;
    private PixelText text;
    private int shownSeconds = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Create();
        // The scene is reloaded by the Restart button: bring the timer back with it.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Create();
    }

    private static void Create()
    {
        if (FindAnyObjectByType<RunTimerHud>() == null)
            new GameObject("RunTimerHud").AddComponent<RunTimerHud>();
    }

    private void Awake() => BuildCanvas();

    private void Start() => game = FindAnyObjectByType<GameManager>();

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("RunTimerCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // under the level-up, pause and game over screens

        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        RectTransform safeArea = ScreenColumn.Create(canvasObject.transform, "SafeArea");

        var go = new GameObject("Time", typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelText));
        go.transform.SetParent(safeArea, false);
        text = go.GetComponent<PixelText>();
        text.PixelSize = PixelSize;
        text.Alignment = PixelText.Align.Center;
        text.color = TextColor;
        text.raycastTarget = false;
        text.Text = "0:00";

        var outline = go.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(3f, 3f);
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = OutlineColor;
        shadow.effectDistance = new Vector2(0f, -3f);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400f, 60f);
        rect.anchoredPosition = new Vector2(0f, AnchorY);
    }

    private void Update()
    {
        if (game == null) return;
        int seconds = Mathf.FloorToInt(game.RunTime);
        if (seconds == shownSeconds) return;
        shownSeconds = seconds;
        text.Text = $"{seconds / 60}:{seconds % 60:00}";
    }
}
