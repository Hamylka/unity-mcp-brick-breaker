using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right stack of rows, one per active bonus: its name, then one circular timer per running instance
/// (so every multi-ball extra shows its own countdown). The ring drains clockwise with the seconds left in
/// the middle, blinks during the last seconds, and fades out when its timer ends. Untimed bonuses (the Shield)
/// show a full ring. Creates itself when the game starts, so no scene setup is needed.
/// </summary>
public class ActiveBonusHud : MonoBehaviour
{
    private const float RingSize = 64f;
    private const float RingGap = 10f;
    private const float RowGap = 14f;
    private const float Margin = 30f;
    private const float TopOffset = 60f;
    private const float LabelGap = 14f;
    private const float LabelHeight = 21f; // 7 font pixels at size 3
    private const float WarnTime = 2f;
    private const float FadeTime = 0.25f;

    private static readonly Color TrackColor = new(1f, 1f, 1f, 0.16f);

    private sealed class Ring
    {
        public string Id;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Image Fill;
        public PixelText Time;
        public float FadeLeft = FadeTime;
        public bool Alive;
    }

    private sealed class Row
    {
        public string Group;
        public PixelText Label;
        public readonly List<Ring> Rings = new();
    }

    private readonly Dictionary<string, Ring> rings = new();
    private readonly List<Row> rows = new();
    private readonly List<ActiveBonusTracker.Entry> active = new();
    private RectTransform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<ActiveBonusHud>() == null)
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

        foreach (Row row in rows)
            foreach (Ring ring in row.Rings) ring.Alive = false;

        foreach (ActiveBonusTracker.Entry entry in active)
        {
            if (!rings.TryGetValue(entry.Id, out Ring ring))
            {
                Row row = RowFor(entry.Group);
                ring = CreateRing(entry.Id);
                rings[entry.Id] = ring;
                row.Rings.Add(ring);
            }
            ring.Alive = true;
            ring.FadeLeft = FadeTime;
            Refresh(ring, entry);
            RowFor(entry.Group).Label.Text = entry.Label;
            RowFor(entry.Group).Label.color = entry.Color;
        }

        // Rings of ended timers fade out, then go; rows without rings go with them.
        for (int r = rows.Count - 1; r >= 0; r--)
        {
            Row row = rows[r];
            for (int i = row.Rings.Count - 1; i >= 0; i--)
            {
                Ring ring = row.Rings[i];
                if (ring.Alive) continue;
                ring.FadeLeft -= Time.unscaledDeltaTime;
                ring.Group.alpha = Mathf.Clamp01(ring.FadeLeft / FadeTime);
                if (ring.FadeLeft > 0f) continue;

                Destroy(ring.Rect.gameObject);
                rings.Remove(ring.Id);
                row.Rings.RemoveAt(i);
            }
            if (row.Rings.Count == 0)
            {
                Destroy(row.Label.gameObject);
                rows.RemoveAt(r);
            }
        }

        Layout();
    }

    // Rows stack from the top; each row's rings run right to left from the screen edge, the name sits left of them.
    private void Layout()
    {
        float y = -TopOffset;
        foreach (Row row in rows)
        {
            for (int i = 0; i < row.Rings.Count; i++)
                row.Rings[i].Rect.anchoredPosition = new Vector2(-Margin - i * (RingSize + RingGap), y);

            float labelRight = -Margin - row.Rings.Count * (RingSize + RingGap) + RingGap - LabelGap;
            row.Label.rectTransform.anchoredPosition = new Vector2(labelRight, y - (RingSize - LabelHeight) * 0.5f);
            y -= RingSize + RowGap;
        }
    }

    private Row RowFor(string group)
    {
        foreach (Row row in rows)
            if (row.Group == group) return row;

        var created = new Row { Group = group };
        PixelText label = NewText("Label_" + group, root, PixelText.Align.Right, 3f);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(300f, LabelHeight);
        created.Label = label;
        rows.Add(created);
        return created;
    }

    private static void Refresh(Ring ring, ActiveBonusTracker.Entry entry)
    {
        ring.Group.alpha = 1f;

        float fill = 1f;
        bool warning = false;
        if (entry.Timed)
        {
            fill = Mathf.Clamp01(entry.Remaining / entry.Total);
            warning = entry.Remaining <= WarnTime;
            ring.Time.Text = entry.Remaining < 3f
                ? entry.Remaining.ToString("0.0")
                : Mathf.CeilToInt(entry.Remaining).ToString();
        }
        else
        {
            ring.Time.Text = "";
        }

        ring.Fill.fillAmount = fill;
        Color color = entry.Color;
        if (warning) color.a = Mathf.Sin(Time.unscaledTime * 18f) > 0f ? 1f : 0.35f; // blink in the last seconds
        ring.Fill.color = color;
        ring.Time.color = warning ? color : Color.white;
    }

    private Ring CreateRing(string id)
    {
        var ring = new Ring { Id = id };

        Image track = NewImage("Ring_" + id, root, TrackColor);
        ring.Rect = track.rectTransform;
        ring.Rect.anchorMin = ring.Rect.anchorMax = ring.Rect.pivot = new Vector2(1f, 1f);
        ring.Rect.sizeDelta = new Vector2(RingSize, RingSize);
        ring.Group = track.gameObject.AddComponent<CanvasGroup>();

        ring.Fill = NewImage("Fill", ring.Rect, Color.white);
        ring.Fill.type = Image.Type.Filled;
        ring.Fill.fillMethod = Image.FillMethod.Radial360;
        ring.Fill.fillOrigin = (int)Image.Origin360.Top;
        ring.Fill.fillClockwise = false; // drains clockwise: the empty part grows from the top
        ring.Fill.fillAmount = 1f;
        RectTransform fillRect = ring.Fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

        ring.Time = NewText("Time", ring.Rect, PixelText.Align.Center, 2f);
        RectTransform timeRect = ring.Time.rectTransform;
        timeRect.anchorMin = Vector2.zero;
        timeRect.anchorMax = Vector2.one;
        timeRect.offsetMin = timeRect.offsetMax = Vector2.zero;
        return ring;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = UiSprites.Ring;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static PixelText NewText(string name, Transform parent, PixelText.Align align, float pixelSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelText));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<PixelText>();
        text.PixelSize = pixelSize;
        text.Alignment = align;
        text.color = Color.white;
        text.Text = "";
        return text;
    }
}
