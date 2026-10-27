using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Segmented bar near the bottom of the screen that fills with each hit toward the next Cleave sweep (one segment
/// per hit), and flashes when the sweep goes off. Only shows once the Cleave power-up has been picked.
/// Creates itself when the game starts, so no scene setup is needed.
/// </summary>
public class CleaveMeter : MonoBehaviour
{
    private const float BarWidth = 420f;
    private const float BarHeight = 22f;
    private const float SegmentGap = 6f;
    private const float BottomOffset = 44f;
    private const float LabelHeight = 21f; // 7 font pixels at size 3
    private const float LabelGap = 10f;
    private const float FlashTime = 0.35f;

    private static readonly Color CleaveColor = new(0.8f, 1f, 1f);
    private static readonly Color EmptyColor = new(1f, 1f, 1f, 0.16f);

    private readonly List<Image> segments = new();
    private AreaDamage area;
    private RectTransform root;
    private CanvasGroup group;
    private PixelText label;
    private float findTimer;
    private float flash;
    private int builtFor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<CleaveMeter>() == null)
            new GameObject("CleaveMeter").AddComponent<CleaveMeter>();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
        {
            // The scene is reloaded by the Restart button: bring the meter back with it.
            if (FindAnyObjectByType<CleaveMeter>() == null)
                new GameObject("CleaveMeter").AddComponent<CleaveMeter>();
        };
    }

    private void Awake()
    {
        var canvasObject = new GameObject("CleaveMeterCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // same layer as the bonus HUD, under the level-up and game over screens
        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());

        root = ScreenColumn.Create(canvasObject.transform);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelText));
        labelObject.transform.SetParent(root, false);
        label = labelObject.GetComponent<PixelText>();
        label.PixelSize = 3f;
        label.Alignment = PixelText.Align.Center;
        label.color = CleaveColor;
        label.Text = "CLEAVE";
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(300f, LabelHeight);
        labelRect.anchoredPosition = new Vector2(0f, BottomOffset + BarHeight + LabelGap);
    }

    private void OnDestroy()
    {
        if (area != null) area.CleaveFired -= HandleFired;
    }

    private void Update()
    {
        if (area == null)
        {
            // The AreaDamage component appears on the first AoE pick: look for it twice a second.
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer > 0f) return;
            findTimer = 0.5f;
            area = FindAnyObjectByType<AreaDamage>();
            if (area != null) area.CleaveFired += HandleFired;
            return;
        }

        int every = area.CleaveEvery;
        group.alpha = every > 0 ? 1f : 0f;
        if (every <= 0) return;

        if (builtFor != every) Rebuild(every);

        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime);
        float flashK = flash / FlashTime;
        int charge = area.CleaveCharge;
        for (int i = 0; i < segments.Count; i++)
        {
            Color color = i < charge ? CleaveColor : EmptyColor;
            segments[i].color = Color.Lerp(color, Color.white, flashK);
        }

        // The last segment pulses while the next hit sets the sweep off.
        if (charge == every - 1)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
            segments[every - 1].color = Color.Lerp(EmptyColor, CleaveColor, pulse);
        }
    }

    private void HandleFired() => flash = FlashTime;

    // One segment per hit needed, laid out left to right across a fixed bar width.
    private void Rebuild(int count)
    {
        foreach (Image segment in segments) Destroy(segment.gameObject);
        segments.Clear();
        builtFor = count;

        float width = (BarWidth - SegmentGap * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Segment_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, BarHeight);
            rect.anchoredPosition = new Vector2(-BarWidth * 0.5f + width * 0.5f + i * (width + SegmentGap), BottomOffset);
            segments.Add(image);
        }
    }
}
