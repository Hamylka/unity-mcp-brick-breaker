using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right stack of chips, one per active bonus: name, time left and a draining bar in the bonus color.
/// Untimed bonuses (the Shield) show a full bar. The bar blinks during the last seconds, and a chip fades out when
/// its bonus ends. Creates itself when the game starts, so no scene setup is needed.
/// </summary>
public class ActiveBonusHud : MonoBehaviour
{
    private const float ChipWidth = 340f;
    private const float ChipHeight = 72f;
    private const float ChipGap = 12f;
    private const float Margin = 30f;
    private const float TopOffset = 60f;
    private const float WarnTime = 2f;
    private const float FadeTime = 0.25f;

    private static readonly Color PanelColor = new(0.07f, 0.09f, 0.2f, 0.95f);
    private static readonly Color TrackColor = new(1f, 1f, 1f, 0.12f);

    private sealed class Chip
    {
        public string Id;
        public RectTransform Rect;
        public CanvasGroup Group;
        public PixelText Label;
        public PixelText Time;
        public Image Fill;
        public RectTransform FillRect;
        public float FadeLeft = FadeTime;
        public bool Alive;
    }

    private readonly Dictionary<string, Chip> chips = new();
    private readonly List<Chip> order = new();
    private readonly List<ActiveBonusTracker.Entry> active = new();
    private readonly List<string> finished = new();
    private RectTransform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<ActiveBonusHud>() != null) return;
        new GameObject("ActiveBonusHud").AddComponent<ActiveBonusHud>();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
        {
            // The scene is reloaded by the Restart button: bring the HUD back with it.
            if (FindAnyObjectByType<ActiveBonusHud>() == null)
                new GameObject("ActiveBonusHud").AddComponent<ActiveBonusHud>();
        };
    }

    private void Awake() => BuildCanvas();

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("ActiveBonusCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // under the level-up and game over screens

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        root = (RectTransform)canvasObject.transform;
    }

    private void LateUpdate() // after the bonuses have reported this frame
    {
        ActiveBonusTracker.GetActive(active);

        foreach (Chip chip in order) chip.Alive = false;
        foreach (ActiveBonusTracker.Entry entry in active)
        {
            if (!chips.TryGetValue(entry.Id, out Chip chip))
            {
                chip = CreateChip(entry.Id);
                chips[entry.Id] = chip;
                order.Add(chip);
            }
            chip.Alive = true;
            chip.FadeLeft = FadeTime;
            Refresh(chip, entry);
        }

        // Chips of ended bonuses fade out, then go.
        finished.Clear();
        foreach (Chip chip in order)
        {
            if (chip.Alive) continue;
            chip.FadeLeft -= Time.unscaledDeltaTime;
            chip.Group.alpha = Mathf.Clamp01(chip.FadeLeft / FadeTime);
            if (chip.FadeLeft <= 0f) finished.Add(chip.Id);
        }
        foreach (string id in finished)
        {
            Chip chip = chips[id];
            Destroy(chip.Rect.gameObject);
            chips.Remove(id);
            order.Remove(chip);
        }

        // Stack from the top, new bonuses at the bottom.
        for (int i = 0; i < order.Count; i++)
            order[i].Rect.anchoredPosition = new Vector2(-Margin, -TopOffset - i * (ChipHeight + ChipGap));
    }

    private static void Refresh(Chip chip, ActiveBonusTracker.Entry entry)
    {
        chip.Label.Text = entry.Label;
        chip.Label.color = entry.Color;
        chip.Group.alpha = 1f;

        float fill = 1f;
        bool warning = false;
        if (entry.Timed)
        {
            fill = Mathf.Clamp01(entry.Remaining / entry.Total);
            warning = entry.Remaining <= WarnTime;
            chip.Time.Text = entry.Remaining < 3f
                ? entry.Remaining.ToString("0.0") + "S"
                : Mathf.CeilToInt(entry.Remaining) + "S";
        }
        else
        {
            chip.Time.Text = "";
        }

        chip.FillRect.anchorMax = new Vector2(fill, 1f);
        Color color = entry.Color;
        if (warning) color.a = Mathf.Sin(Time.unscaledTime * 18f) > 0f ? 1f : 0.35f; // blink in the last seconds
        chip.Fill.color = color;
    }

    private Chip CreateChip(string id)
    {
        var chip = new Chip { Id = id };

        Image panel = NewImage("Chip_" + id, root, PanelColor);
        chip.Rect = panel.rectTransform;
        chip.Rect.anchorMin = chip.Rect.anchorMax = chip.Rect.pivot = new Vector2(1f, 1f);
        chip.Rect.sizeDelta = new Vector2(ChipWidth, ChipHeight);
        chip.Group = panel.gameObject.AddComponent<CanvasGroup>();

        chip.Label = NewText("Label", chip.Rect, PixelText.Align.Left);
        SetRect(chip.Label.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f), new Vector2(18f, -33f), new Vector2(0f, -12f));
        chip.Time = NewText("Time", chip.Rect, PixelText.Align.Right);
        SetRect(chip.Time.rectTransform, new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(0f, -33f), new Vector2(-18f, -12f));

        Image track = NewImage("Track", chip.Rect, TrackColor);
        SetRect(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 12f), new Vector2(-18f, 24f));

        chip.Fill = NewImage("Fill", track.rectTransform, Color.white);
        chip.FillRect = chip.Fill.rectTransform;
        SetRect(chip.FillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return chip;
    }

    // Offsets are (left, bottom) and (right, top) of the rect relative to its anchors.
    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = UiSprites.RoundedRect;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static PixelText NewText(string name, Transform parent, PixelText.Align align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelText));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<PixelText>();
        text.PixelSize = 3f;
        text.Alignment = align;
        text.color = Color.white;
        text.Text = "";
        return text;
    }
}
