using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a number or pips over every brick for the Numbers / Pips display modes (<see cref="BrickHpDisplay"/>):
/// the estimated ball hits left to break it (<see cref="Brick.HitsLeft"/>), not its raw HP. One-hit bricks show nothing.
/// Special bricks also carry an icon in every display mode (a lock on Armored, a cross on Healer), drawn left of the
/// label as one centered group: the icon never replaces the label.
/// The labels live on a screen-space canvas, not in 3D: the scene is rendered at 640 px, where a brick is only
/// about 10 px tall, too small for crisp glyphs, while the UI stays at full resolution. Each label follows its
/// brick every frame (projected through the camera), scales with it (spawn pop-in, hit punch) and pops when the
/// HP changes. Builds itself at startup, no scene setup; bricks register themselves.
/// </summary>
public class BrickHpOverlay : MonoBehaviour
{
    private const int MaxPips = 5;      // above this many hits (one full row), Pips mode shows a skull
    private const int PipsPerRow = 5;
    private const float PopTime = 0.22f;
    private const float IconPopTime = 0.35f;
    private const float IconGap = 2f;   // font pixels between the icon and the label

    [Tooltip("Projected brick width (canvas units) per font pixel: smaller = bigger labels.")]
    [SerializeField, Min(1f)] private float widthPerPixel = 19f;
    [SerializeField, Min(1f)] private float minPixelSize = 2f;
    [SerializeField, Min(1f)] private float maxPixelSize = 6f;
    [Tooltip("0 = label in the HP color, 1 = pure white. Mostly white reads best over the colored outline.")]
    [SerializeField, Range(0f, 1f)] private float whiteness = 0.6f;
    [Tooltip("Icon colors: more saturated than the brick tints, so they read apart from the near-white labels.")]
    [SerializeField] private Color armoredIconColor = new(0.45f, 0.7f, 1f);
    [SerializeField] private Color healerIconColor = new(0.3f, 1f, 0.45f);
    [Tooltip("Height of the label point above the brick center, as a fraction of the brick height.")]
    [SerializeField] private float anchorLift = 0.15f;
    [SerializeField] private Color shadowColor = new(0.02f, 0.02f, 0.08f, 0.95f);

    private sealed class Label
    {
        public RectTransform rect;      // group: follows the brick, carries its scale and the hit pop
        public PixelText text;
        public RectTransform textRect;
        public PixelText icon;
        public RectTransform iconRect;
        public int hitPoints = -1;
        public int hits = -1;
        public bool hasText;
        public BrickHpDisplay.Mode mode;
        public BrickKind kind;
        public int healCasts;
        public float popT = 1f;
        public float iconPopT = 1f;
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
        BrickHpDisplay.Mode mode = BrickHpDisplay.Current;
        bool numbers = mode != BrickHpDisplay.Mode.Color;
        if (cam == null) cam = Camera.main;
        int frame = Time.frameCount;
        if (cam != null)
        {
            bricks.RemoveAll(b => b == null);
            float scaleFactor = Mathf.Max(0.0001f, canvas.scaleFactor);
            for (int i = 0; i < bricks.Count; i++)
            {
                Brick brick = bricks[i];
                if (!brick.ShowsHpLabel) continue;
                // One-hit bricks carry no number: only the bricks that take work stand out. Special bricks keep their icon.
                bool hasText = numbers && brick.HitsLeft > 1;
                if (!hasText && brick.Kind == BrickKind.Normal) continue;
                if (!assigned.TryGetValue(brick, out Label label))
                {
                    label = pool.Count > 0 ? pool.Pop() : CreateLabel();
                    label.hitPoints = -1;
                    label.hits = -1;
                    label.popT = 1f;
                    label.iconPopT = 1f;
                    label.kind = (BrickKind)(-1);
                    label.healCasts = brick.HealCasts;
                    assigned.Add(brick, label);
                }
                if (UpdateLabel(label, brick, mode, hasText, scaleFactor)) label.frame = frame;
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
            label.icon.enabled = false;
            pool.Push(label);
            assigned.Remove(brick);
        }
    }

    /// <returns>False when the brick is off screen (behind the camera).</returns>
    private bool UpdateLabel(Label label, Brick brick, BrickHpDisplay.Mode mode, bool hasText, float scaleFactor)
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

        // Icon: same size as the digits, in the brick type's color so it reads apart from the hits-left label.
        BrickKind kind = brick.Kind;
        bool hasIcon = kind != BrickKind.Normal;
        if (kind != label.kind)
        {
            label.kind = kind;
            label.icon.Text = kind == BrickKind.Armored ? "$" : kind == BrickKind.Healer ? "&" : "";
            label.icon.color = kind == BrickKind.Armored ? armoredIconColor : healerIconColor;
        }
        float iconWidth = 0f, gap = 0f;
        if (hasIcon)
        {
            if (!Mathf.Approximately(label.icon.PixelSize, pixel)) label.icon.PixelSize = pixel;
            iconWidth = PixelFont.GlyphWidth * pixel;
            gap = hasText ? IconGap * pixel : 0f;
            // A Healer's cross pops each time it heals its neighbours.
            if (brick.HealCasts != label.healCasts)
            {
                label.healCasts = brick.HealCasts;
                label.iconPopT = 0f;
            }
            label.iconPopT = Mathf.Min(1f, label.iconPopT + dt / IconPopTime);
            float iconPop = 1f + 0.8f * (1f - label.iconPopT) * (1f - label.iconPopT);
            label.iconRect.localScale = Vector3.one * iconPop;
        }

        // Shown value: ball hits left to break the brick, not its raw HP (much smaller numbers, fewer pips).
        int hp = brick.HitPoints;
        int hits = brick.HitsLeft;
        bool pips = mode == BrickHpDisplay.Mode.Pips && hits <= MaxPips;
        // Too many hits to count at a glance: one skull ("@" glyph) that just says "tough, leave it for later".
        bool skull = mode == BrickHpDisplay.Mode.Pips && hits > MaxPips;
        float textWidth = 0f;
        if (hasText)
        {
            if (hits != label.hits || mode != label.mode || !label.hasText)
            {
                label.hits = hits;
                label.mode = mode;
                // Pips count the hits beyond the first (1 pip = 2 hits, 4 pips = 5 hits): one-hit bricks have no label anyway.
                label.text.Text = pips ? PipText(hits - 1) : skull ? "@" : hits.ToString();
                label.text.Spacing = pips ? 0f : 1f;
                label.text.LineGap = pips ? 0f : 3f;
                label.textRect.sizeDelta = new Vector2(400f, 120f);
            }
            // A row of 5 pips is 25 font pixels wide (4-pixel squares, 1 apart): size them so a full row fits the brick.
            // Next to an icon, the 4 pips at most (20 font pixels) share the width with it.
            float pipSize = Mathf.Floor(widthUnits / 26f);
            if (hasIcon) pipSize = Mathf.Min(pipSize, Mathf.Floor((widthUnits - iconWidth - gap) / 21f));
            float pixelSize = pips ? Mathf.Clamp(pipSize, minPixelSize, maxPixelSize)
                : skull ? Mathf.Min(pixel + 1f, maxPixelSize) // a lone icon, one step bigger than digits so its face reads
                : pixel;
            if (!Mathf.Approximately(label.text.PixelSize, pixelSize)) label.text.PixelSize = pixelSize; // the setter rebuilds the mesh

            Color color = Color.Lerp(brick.HpColor, Color.white, whiteness);
            color.a = 1f;
            label.text.color = color;
            textWidth = label.text.Measure(0f).x;
        }
        label.hasText = hasText;
        label.text.enabled = hasText;
        label.icon.enabled = hasIcon;

        // Icon then label, centered together on the brick.
        float total = iconWidth + gap + textWidth;
        label.iconRect.anchoredPosition = new Vector2(-total * 0.5f + iconWidth * 0.5f, 0f);
        label.textRect.anchoredPosition = new Vector2(total * 0.5f - textWidth * 0.5f, 0f);

        // Pop when this brick was hit or healed, not when a damage upgrade lowers every label at once.
        if (label.hitPoints > 0 && hp != label.hitPoints) label.popT = 0f;
        label.hitPoints = hp;
        label.popT = Mathf.Min(1f, label.popT + dt / PopTime);
        float pop = 1f + 0.5f * (1f - label.popT) * (1f - label.popT);
        label.rect.localScale = Vector3.one * (brick.VisualScale * pop);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        label.rect.anchoredPosition = local;
        return true;
    }

    // "*" is the pip glyph of the pixel font; full rows of 5 on top.
    private static string PipText(int count)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
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
        rect.sizeDelta = Vector2.zero;
        PixelText text = CreateText(rect, "Hits");
        PixelText icon = CreateText(rect, "Icon");
        ((RectTransform)icon.transform).sizeDelta = new Vector2(120f, 120f);
        return new Label
        {
            rect = rect,
            text = text,
            textRect = (RectTransform)text.transform,
            icon = icon,
            iconRect = (RectTransform)icon.transform
        };
    }

    private PixelText CreateText(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        var text = go.AddComponent<PixelText>();
        text.Alignment = PixelText.Align.Center;
        text.Text = "";
        text.enabled = false;
        // A drop shadow, not an outline: an outline fills the 1-pixel holes of small glyphs (0, 8, 4).
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(3f, -3f);
        return text;
    }
}
