using UnityEngine;

/// <summary>
/// Plays the music loop. Fades in at the start, gets muffled (low-pass + quieter) whenever the game is slowed or
/// paused (level-up cards, game over), and on game over fades out with a slight "tape slowing down" pitch drop.
/// </summary>
[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField, Min(0f)] private float fadeInTime = 2f;

    [Header("Pause / Slow Motion")]
    [Tooltip("Low-pass cutoff (Hz) when the game is fully paused. The music opens up again as time speeds back up.")]
    [SerializeField, Min(100f)] private float muffledCutoff = 900f;
    [Tooltip("Volume multiplier when the game is fully paused.")]
    [SerializeField, Range(0f, 1f)] private float muffledVolume = 0.6f;

    [Header("Danger")]
    [Tooltip("Extra playback speed at full danger (DangerMeter.Level = 1). The pitch rises with it, arcade style: 0.08 is about a semitone.")]
    [SerializeField, Range(0f, 0.3f)] private float dangerTempo = 0.08f;
    [Tooltip("Seconds for the tempo to follow the danger.")]
    [SerializeField, Min(0.01f)] private float dangerTempoEase = 1.5f;

    [Header("Game Over")]
    [SerializeField] private GameManager gameManager;
    [SerializeField, Min(0.1f)] private float gameOverFadeTime = 1.5f;
    [SerializeField, Range(0.3f, 1f)] private float gameOverPitch = 0.75f;

    private const float OpenCutoff = 22000f;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private float fade;
    private bool ending;

    public float Volume
    {
        get => volume;
        set => volume = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        lowPass = GetComponent<AudioLowPassFilter>();

        source.clip = music;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;
        lowPass.cutoffFrequency = OpenCutoff;
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.GameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.GameOver -= HandleGameOver;
    }

    private void Start()
    {
        if (music != null) source.Play();
    }

    private void HandleGameOver(GameManager.LoseReason reason) => ending = true;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        fade = Mathf.MoveTowards(fade, ending ? 0f : 1f, dt / (ending ? gameOverFadeTime : Mathf.Max(0.01f, fadeInTime)));

        // 1 = normal speed, 0 = paused. Squared so the muffle kicks in clearly during slow motion.
        float speed = Mathf.Clamp01(Time.timeScale);
        float targetCutoff = Mathf.Lerp(muffledCutoff, OpenCutoff, speed * speed);
        lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, targetCutoff, 1f - Mathf.Exp(-dt * 8f));

        source.volume = volume * fade * Mathf.Lerp(muffledVolume, 1f, speed);

        // The wall nearing the lose line speeds the loop up; the game over drop takes over from whatever speed it had.
        float targetPitch = ending ? gameOverPitch : 1f + dangerTempo * DangerMeter.Level;
        float pitchEase = ending ? 0.5f : dangerTempoEase;
        source.pitch = Mathf.Lerp(source.pitch, targetPitch, 1f - Mathf.Exp(-dt / pitchEase));
    }
}
