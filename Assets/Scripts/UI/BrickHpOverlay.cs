using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a number or pips over every brick for the Numbers / Pips display modes (<see cref="BrickHpDisplay"/>):
/// the estimated ball hits left to break it (<see cref="Brick.HitsLeft"/>), not its raw HP. One-hit bricks show nothing.
/// The labels live on a screen-space canvas, not in 3D: the scene is rendered at 640 px, where a brick is only
/// about 10 px tall, too small for crisp glyphs, while the UI stays at full resolution. Each label follows its
/// brick every frame (projected through the camera), scales with it (spawn pop-in, hit punch) and pops when the
/// HP changes. Builds itself at startup, no scene setup; bricks register themselves.
/// </summary>
public class BrickHpOverlay : MonoBehaviour
{
    private const int MaxPips = 10;     // above this many hits, Pips mode shows the number
    private const int PipsPerRow = 5;
    private const float PopTime = 0.22f;

    [Tooltip("Projected brick width (canvas units) per font pixel: smaller = bigger labels.")]
    [SerializeField, Min(1f)] private float widthPerPixel = 19f;
    [SerializeField, Min(1f)] private float minPixelSize = 2f;
    [SerializeField, Min(1f)] private float maxPixelSize = 6f;
    [Tooltip("0 = label in the HP color, 1 = pure white. Mostly white reads best over the colored outline.")]
    [SerializeField, Range(0f, 1f)] private float whiteness = 0.6f;
    [Tooltip("Height of the label point above the brick center, as a fraction of the brick height.")]
    [SerializeField] private float anchorLift = 0.15f;
    [SerializeField] private Color shadowColor = new(0.02f, 0.02f, 0.08f, 0.95f);

    private sealed class Label
    {
        public PixelText text;
        public RectTransform rect;
        public int hitPoints = -1;
        public int hits = -1;
        public BrickHpDisplay.Mode mode;
        public float popT = 1f;
        public int frame;
    }

    private static readonly List<Brick> bricks = new();
    private readonly Dictionary<Brick, Label> assigned = new();
    private readonly Stack<Label> pool = new();
    private readonly List<Brick> released = new();
    private RectTransform root;
    private Canvas canvas;
    private Camera cam;

    public static void Register(Brick brick)
    {
        if (!bricks.Contains(brick)) bricks.Add(brick);
    }

    public static void Unregister(Brick brick) => bricks.Remove(brick);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Spawn();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Spawn();
    }

    private static void Spawn()
    {
        if (FindAnyObjectByType<BrickHpOverlay>() == null && FindAnyObjectByType<GameManager>() != null)
            new GameObject("BrickHpOverlay").AddComponent<BrickHpOverlay>();
    }

    private void Awake()
    {
        var canvasObject = new GameObject("BrickHpCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4; // under the HUD, the cards and every menu
        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());
        root = (RectTransform)canvasObject.transform;
    }

    private void LateUpdate()
    {
        bool show = BrickHpDisplay.Current != BrickHpDisplay.Mode.Color;
        if (show && cam == null) cam = Camera.main;
        int frame = Time.frameCount;
        if (show && cam != null)
        {
            bricks.RemoveAll(b => b == null);
            BrickHpDisplay.Mode mode = BrickHpDisplay.Current;
            float scaleFactor = Mathf.Max(0.0001f, canvas.scaleFactor);
            for (int i = 0; i < bricks.Count; i++)
            {
                Brick brick = bricks[i];
                // One-hit bricks carry no label: only the bricks that take work stand out.
                if (!brick.ShowsHpLabel || brick.HitsLeft <= 1) continue;
                if (!assigned.TryGetValue(brick, out Label label))
                {
                    label = pool.Count > 0 ? pool.Pop() : CreateLabel();
                    label.hitPoints = -1;
                    label.hits = -1;
                    label.popT = 1f;
                    assigned.Add(brick, label);
                }
                if (UpdateLabel(label, brick, mode, scaleFactor)) label.frame = frame;
            }
        }

        // Labels whose brick died, hid or left the screen this frame go back to the pool.
        released.Clear();
        foreach (KeyValuePair<Brick, Label> pair in assigned)
            if (pair.Value.frame != frame) released.Add(pair.Key);
        foreach (Brick brick in released)
        {
            Label label = assigned[brick];
            label.text.enabled = false;
            pool.Push(label);
            assigned.Remove(brick);
        }
    }

    /// <returns>False when the brick is off screen (behind the camera).</returns>
    private bool UpdateLabel(Label label, Brick brick, BrickHpDisplay.Mode mode, float scaleFactor)
    {
        float dt = Time.deltaTime;
        Transform t = brick.transform;
        Vector3 size = brick.Size;
        Vector3 anchor = t.position + Vector3.up * (size.y * anchorLift);
        Vector3 screen = cam.WorldToScreenPoint(anchor);
        if (screen.z <= 0f) return false;

        // Label size follows the brick's size on screen (perspective), in whole font pixels for crisp glyphs.
        Vector3 half = t.right * (size.x * 0.5f);
        float widthUnits = Mathf.Abs(cam.WorldToScreenPoint(t.position + half).x - cam.WorldToScreenPoint(t.position - half).x) / scaleFactor;
        float pixel = Mathf.Clamp(Mathf.Floor(widthUnits / widthPerPixel), minPixelSize, maxPixelSize);

        // Shown value: ball hits left to break the brick, not its raw HP (much smaller numbers, fewer pips).
        int hp = brick.HitPoints;
        int hits = brick.HitsLeft;
        bool pips = mode == BrickHpDisplay.Mode.Pips && hits <= MaxPips;
        if (hits != label.hits || mode != label.mode)
        {
            label.hits = hits;
            label.mode = mode;
            label.text.Text = pips ? PipText(hits) : hits.ToString();
            label.text.Spacing = pips ? 0f : 1f;
            label.text.LineGap = pips ? 0f : 3f;
            label.rect.sizeDelta = new Vector2(400f, 120f);
        }
        // Pop when this brick was hit, not when a damage upgrade lowers every label at once.
        if (label.hitPoints > 0 && hp < label.hitPoints) label.popT = 0f;
        label.hitPoints = hp;
        // A row of 5 pips is 25 font pixels wide (4-pixel squares, 1 apart): size them so a full row fits the brick.
        float pixelSize = pips ? Mathf.Clamp(Mathf.Floor(widthUnits / 26f), minPixelSize, maxPixelSize) : pixel;
        if (!Mathf.Approximately(label.text.PixelSize, pixelSize)) label.text.PixelSize = pixelSize; // the setter rebuilds the mesh

        Color color = Color.Lerp(brick.HpColor, Color.white, whiteness);
        color.a = 1f;
        label.text.color = color;

        label.popT = Mathf.Min(1f, label.popT + dt / PopTime);
        float pop = 1f + 0.5f * (1f - label.popT) * (1f - label.popT);
        label.rect.localScale = Vector3.one * (brick.VisualScale * pop);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        label.rect.anchoredPosition = local;
        label.text.enabled = true;
        return true;
    }

    // "*" is the pip glyph of the pixel font; full rows of 5 on top.
    private static string PipText(int hitPoints)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < hitPoints; i++)
        {
            if (i > 0 && i % PipsPerRow == 0) text.Append('\n');
            text.Append('*');
        }
        return text.ToString();
    }

    private Label CreateLabel()
    {
        var go = new GameObject("HpLabel", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        var text = go.AddComponent<PixelText>();
        text.Alignment = PixelText.Align.Center;
        text.enabled = false;
        // A drop shadow, not an outline: an outline fills the 1-pixel holes of small glyphs (0, 8, 4).
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(3f, -3f);
        return new Label { text = text, rect = rect };
    }
}
