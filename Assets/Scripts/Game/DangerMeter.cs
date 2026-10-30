using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the wall's threat readable. Measures how many wall steps are left before the front row reaches the lose line
/// (the step timer included, so the danger rises smoothly between steps) and turns it into <see cref="Level"/> (0-1)
/// and a heartbeat (<see cref="Beat"/>). From that:
/// <list type="bullet">
/// <item>the lose line throbs brighter and thicker with each beat, with a red light pool on the floor under it;</item>
/// <item>a red, pixel-banded tint creeps in from the screen edges (strongest at the bottom), pulsing with the beat;</item>
/// <item>a low heartbeat plays from mid danger and speeds up; when the next step would end the run, an alarm sounds,
///       the line sheds embers and every step shakes the camera;</item>
/// <item><see cref="MusicPlayer"/> reads <see cref="Level"/> to play the loop faster.</item>
/// </list>
/// Creates itself when the game starts, so no scene setup is needed.
/// </summary>
public class DangerMeter : MonoBehaviour
{
    [Header("Danger Level")]
    [Tooltip("Steps left (timer included) at which the danger starts to rise from 0.")]
    [SerializeField, Min(1.5f)] private float warnSteps = 4.5f;
    [Tooltip("Seconds for the level to rise toward the measured danger.")]
    [SerializeField, Min(0.01f)] private float riseTime = 0.35f;
    [Tooltip("Seconds for the level to fall back (after clearing bricks near the line).")]
    [SerializeField, Min(0.01f)] private float fallTime = 1.2f;

    [Header("Heartbeat")]
    [SerializeField, Min(10f)] private float calmBpm = 62f;
    [SerializeField, Min(10f)] private float dangerBpm = 128f;
    [Tooltip("Beats per minute reached right before the lethal step.")]
    [SerializeField, Min(10f)] private float lastStepBpm = 175f;
    [Tooltip("The heartbeat sound starts above this level.")]
    [SerializeField, Range(0f, 1f)] private float soundFrom = 0.35f;

    [Header("Lose Line")]
    [Tooltip("Extra brightness of the line at full danger (x the material color), on top of each beat.")]
    [SerializeField, Min(0f)] private float lineGlow = 0.6f;
    [SerializeField, Min(0f)] private float lineBeatGlow = 2.2f;
    [Tooltip("Extra thickness of the line at full danger on a beat (1 = twice as thick).")]
    [SerializeField, Min(0f)] private float lineBeatThickness = 1.2f;
    [SerializeField, Min(0f)] private float floorGlowIntensity = 1.6f;
    [SerializeField] private Vector2 floorGlowSize = new(8.6f, 1.8f);

    [Header("Edge Tint")]
    [SerializeField, Range(0f, 1f)] private float edgeMaxAlpha = 0.55f;
    [Tooltip("Share of the edge alpha that throbs with the beat.")]
    [SerializeField, Range(0f, 1f)] private float edgeBeatShare = 0.35f;

    [Header("Last Step")]
    [SerializeField, Min(0f)] private float stepShake = 0.12f;
    [SerializeField, Min(0f)] private float lastStepShake = 0.3f;
    [SerializeField, Min(0.01f)] private float emberInterval = 0.07f;

    /// <summary>0 = the wall is far, 1 = the next step ends the run. Smoothed.</summary>
    public static float Level { get; private set; }
    /// <summary>Heartbeat envelope, 0-1 (a "lub-dub" per beat). Already 0 when the danger is.</summary>
    public static float Beat { get; private set; }
    /// <summary>True while the next wall step would push a brick over the lose line.</summary>
    public static bool LastStep { get; private set; }

    private static readonly Color DangerRed = new(1f, 0.1f, 0.2f, 1f);
    private static readonly Color EdgeColor = new(1f, 0.05f, 0.12f, 1f);

    private BrickGrid grid;
    private GameManager game;
    private Renderer line;
    private Color lineBaseColor;
    private Vector3 lineBaseScale;
    private MeshRenderer floorGlow;
    private Image edgeTint;
    private float phase;
    private float emberTimer;
    private bool ending;
    private bool wasLastStep;
    private int alarmBeats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Create();
        // The scene is reloaded by the Restart button: bring the meter back with it.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Create();
    }

    private static void Create()
    {
        if (FindAnyObjectByType<DangerMeter>() == null)
            new GameObject("DangerMeter").AddComponent<DangerMeter>();
    }

    private void Awake()
    {
        Level = 0f;
        Beat = 0f;
        LastStep = false;
        BuildEdgeTint();
    }

    private void Start()
    {
        grid = FindAnyObjectByType<BrickGrid>();
        game = FindAnyObjectByType<GameManager>();
        if (grid != null)
        {
            grid.StepStarted += HandleStep;
            if (grid.DangerLine != null) SetUpLine(grid.DangerLine);
        }
        if (game != null) game.GameOver += HandleGameOver;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.StepStarted -= HandleStep;
        if (game != null) game.GameOver -= HandleGameOver;
        Level = 0f;
        Beat = 0f;
        LastStep = false;
    }

    private void HandleGameOver(GameManager.LoseReason reason) => ending = true;

    private void HandleStep()
    {
        float shake = LastStep ? lastStepShake : stepShake * Level * Level;
        if (shake > 0.001f) BrickBreakerCamera.Shake(shake);
    }

    private void Update()
    {
        // Scaled: the beat freezes with the game (pause, card screen). After a loss the danger fades on real time,
        // since the Game Over sequence slows the game to a stop.
        float dt = ending ? Time.unscaledDeltaTime : Time.deltaTime;

        // Danger: steps left before the front row crosses the line, minus how far the step timer already ran.
        float target = 0f;
        bool lastStep = false;
        if (grid != null && !ending && !grid.IsGameOver)
        {
            // Resting position: a slide in progress counts as done, so the level does not jump around mid-step.
            float front = grid.RestingFrontZ;
            if (!float.IsInfinity(front))
            {
                float stepsLeft = (front - grid.DangerZ) / Mathf.Max(0.01f, grid.StepDistance);
                int lethalStep = Mathf.Max(1, Mathf.CeilToInt(stepsLeft - 0.001f)) - grid.QueuedSteps;
                float timeLeft = lethalStep - grid.StepProgress; // in steps; 0 = the run ends now
                lastStep = lethalStep <= 1;
                target = Mathf.InverseLerp(warnSteps, 1f, timeLeft);
            }
        }
        LastStep = lastStep;

        float ease = target > Level ? riseTime : fallTime;
        Level = Mathf.Lerp(Level, target, 1f - Mathf.Exp(-dt / ease));
        if (Level < 0.001f && target <= 0f) Level = 0f;

        // Heartbeat: faster with the danger, faster still as the lethal step's timer runs out.
        float bpm = Mathf.Lerp(calmBpm, dangerBpm, Level);
        if (LastStep && grid != null) bpm = Mathf.Lerp(dangerBpm, lastStepBpm, grid.StepProgress);
        phase += dt * bpm / 60f;
        if (phase >= 1f)
        {
            phase -= Mathf.Floor(phase);
            OnBeat();
        }

        // Entering the last step: the alarm sounds at once (then on every other beat).
        if (LastStep && !wasLastStep) SoundManager.Play(Sfx.DangerAlarm);
        wasLastStep = LastStep;

        // "Lub" at the start of the cycle, a softer "dub" a fifth of a cycle later.
        float beat = Mathf.Max(Pulse(phase, 0f), 0.6f * Pulse(phase, 0.2f));
        Beat = beat * Mathf.Clamp01(Level * 1.5f);

        UpdateLine(dt);
        UpdateEdgeTint();
    }

    private void OnBeat()
    {
        if (Level < soundFrom) return;
        float loud = Mathf.InverseLerp(soundFrom, 1f, Level);
        SoundManager.Play(Sfx.DangerHeartbeat, 1f + 0.08f * loud, 0.45f + 0.55f * loud);
        if (LastStep && ++alarmBeats % 2 == 0) SoundManager.Play(Sfx.DangerAlarm, 1f, 0.7f);
    }

    // A sharp attack and a quick decay starting at 'at' (cycle fraction).
    private static float Pulse(float p, float at)
    {
        float t = p - at;
        if (t < 0f) return 0f;
        return t < 0.03f ? t / 0.03f : Mathf.Exp(-(t - 0.03f) * 14f);
    }

    private void SetUpLine(Transform lineTransform)
    {
        line = lineTransform.GetComponent<Renderer>();
        lineBaseScale = lineTransform.localScale;
        if (line != null)
        {
            Material material = line.sharedMaterial;
            lineBaseColor = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : NeonGlow.Hdr(DangerRed, 3f);
        }

        // Red light pool on the floor around the line (a glow quad turned to face up).
        floorGlow = NeonGlow.CreateQuad(transform, "DangerFloorGlow", 1f);
        floorGlow.transform.SetPositionAndRotation(new Vector3(lineTransform.position.x, 0.02f, lineTransform.position.z), Quaternion.Euler(90f, 0f, 0f));
        floorGlow.transform.localScale = new Vector3(floorGlowSize.x, floorGlowSize.y, 1f);
        floorGlow.enabled = false;
    }

    private void UpdateLine(float dt)
    {
        if (line == null) return;

        float glow = 1f + lineGlow * Level + lineBeatGlow * Beat;
        NeonGlow.SetColor(line, NeonGlow.Hdr(lineBaseColor, glow));
        Vector3 scale = lineBaseScale;
        scale.z *= 1f + lineBeatThickness * Beat;
        line.transform.localScale = scale;

        if (floorGlow != null)
        {
            float intensity = floorGlowIntensity * (0.35f * Level + 0.65f * Beat);
            floorGlow.enabled = intensity > 0.005f;
            if (floorGlow.enabled) NeonGlow.SetColor(floorGlow, NeonGlow.Hdr(DangerRed, intensity));
        }

        // Last step: the line sheds embers along its length.
        if (!LastStep || dt <= 0f) return;
        emberTimer -= dt;
        if (emberTimer > 0f) return;
        emberTimer = emberInterval;
        Vector3 p = line.transform.position;
        p.x += Random.Range(-0.5f, 0.5f) * lineBaseScale.x;
        p.y = 0.1f;
        NeonFx.Embers(p, NeonGlow.Hdr(DangerRed, 2f), 2, 0.07f, 0.6f);
    }

    private void UpdateEdgeTint()
    {
        if (edgeTint == null) return;
        float alpha = edgeMaxAlpha * Level * Level * (1f - edgeBeatShare + edgeBeatShare * Mathf.Clamp01(Beat * 1.5f));
        edgeTint.enabled = alpha > 0.003f;
        Color c = EdgeColor;
        c.a = alpha;
        edgeTint.color = c;
    }

    private void BuildEdgeTint()
    {
        var canvasObject = new GameObject("DangerCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1; // over the 3D scene, under the brick labels and every HUD
        ScreenColumn.ConfigureScaler(canvasObject.AddComponent<CanvasScaler>());

        RectTransform column = ScreenColumn.Create(canvasObject.transform, "Column");
        var go = new GameObject("EdgeTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(column, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        edgeTint = go.GetComponent<Image>();
        edgeTint.sprite = BuildEdgeSprite();
        edgeTint.raycastTarget = false;
        edgeTint.enabled = false;
    }

    // A low-res 9:16 alpha mask, point-filtered so the falloff shows as chunky pixel bands (the retro look).
    // Thickest at the bottom, where the wall is coming from the player's point of view.
    private static Sprite BuildEdgeSprite()
    {
        const int width = 45, height = 80, bands = 6;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "DangerEdge"
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float side = 1f - Mathf.Min(x, width - 1 - x) / (width * 0.16f);
            float bottom = 1f - y / (height * 0.22f);
            float top = 0.6f * (1f - (height - 1 - y) / (height * 0.08f));
            float edge = Mathf.Clamp01(Mathf.Max(side, Mathf.Max(bottom, top)));
            edge = Mathf.Ceil(edge * edge * bands) / bands; // squared falloff, then quantized into bands
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
