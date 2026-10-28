using UnityEngine;

/// <summary>
/// Fixed pseudo-third-person camera for the brick breaker playfield.
/// Sits behind and above the paddle, tilted down. Optionally drifts slightly
/// sideways with the paddle (smoothed) for a subtle sense of depth.
/// Attach to the Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class BrickBreakerCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Paddle transform. Only its X position is used.")]
    [SerializeField] private Transform paddle;

    [Header("Fixed Pose (matches the reference view)")]
    [SerializeField] private Vector3 basePosition = new(0f, 11.5f, -5.5f);
    [SerializeField] private Vector3 baseEulerAngles = new(48f, 0f, 0f);

    [Header("Lens")]
    [Tooltip("Vertical FOV used when 'Lock Horizontal FOV' is off.")]
    [SerializeField, Range(20f, 90f)] private float verticalFov = 50f;
    [Tooltip("Keeps the playfield width constant on any phone aspect ratio (recommended for portrait).")]
    [SerializeField] private bool lockHorizontalFov = true;
    [Tooltip("Horizontal FOV used when locked. 38 deg is roughly 63 deg vertical at 9:16.")]
    [SerializeField, Range(10f, 90f)] private float horizontalFov = 38f;

    [Header("Lag / Smooth Follow")]
    [SerializeField] private bool followPaddle = true;
    [Tooltip("How much of the paddle's X offset the camera copies (0 = fixed, 1 = locked to paddle).")]
    [SerializeField, Range(0f, 1f)] private float followStrength = 0.25f;
    [Tooltip("Approx. time in seconds to catch up. Higher = more lag.")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.25f;
    [Tooltip("Max sideways offset from the base position, in world units.")]
    [SerializeField, Min(0f)] private float maxSideOffset = 1.5f;

    [Header("Shake")]
    [Tooltip("Offset in world units at full shake (trauma 1). The shake grows with trauma squared.")]
    [SerializeField, Min(0f)] private float maxShakeOffset = 0.3f;
    [Tooltip("Roll in degrees at full shake.")]
    [SerializeField, Min(0f)] private float maxShakeRoll = 2f;
    [Tooltip("Trauma lost per second (unscaled, so the camera settles even when the game is stopped).")]
    [SerializeField, Min(0.01f)] private float shakeDecay = 1.4f;
    [SerializeField, Min(0.1f)] private float shakeFrequency = 22f;

    private static float trauma;

    /// <summary>Adds camera shake (0 to 1, piles up and is capped at 1). Anyone can call it.</summary>
    public static void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    private Camera cam;
    private float velocityX;
    private float currentX;
    private float lastAspect;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        trauma = 0f; // static: a restart must not inherit the last run's shake
        currentX = basePosition.x;
        ApplyPose();
        ApplyLens();
    }

    private void LateUpdate()
    {
        // Follow after the paddle has moved this frame to avoid jitter.
        float targetX = basePosition.x;
        if (followPaddle && paddle != null)
        {
            float offset = (paddle.position.x - basePosition.x) * followStrength;
            targetX += Mathf.Clamp(offset, -maxSideOffset, maxSideOffset);
        }

        currentX = Mathf.SmoothDamp(currentX, targetX, ref velocityX, smoothTime);
        ApplyPose();

        // Re-apply lens only if the screen shape changed (rotation, split screen, etc.).
        if (!Mathf.Approximately(cam.aspect, lastAspect))
            ApplyLens();
    }

    private void ApplyPose()
    {
        // The camera stays at the fixed tilt; only the shake moves and rolls it a little.
        Vector3 shakeOffset = Vector3.zero;
        float roll = 0f;
        if (trauma > 0f)
        {
            trauma = Mathf.Max(0f, trauma - shakeDecay * Time.unscaledDeltaTime);
            float strength = trauma * trauma;
            float t = Time.unscaledTime * shakeFrequency;
            shakeOffset = new Vector3(Noise(t, 0f), Noise(t, 10f), Noise(t, 20f) * 0.5f) * (maxShakeOffset * strength);
            roll = Noise(t, 30f) * maxShakeRoll * strength;
        }

        transform.SetPositionAndRotation(
            new Vector3(currentX, basePosition.y, basePosition.z) + shakeOffset,
            Quaternion.Euler(baseEulerAngles + new Vector3(0f, 0f, roll)));
    }

    // Smooth noise in -1..1, one independent channel per seed.
    private static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;

    private void ApplyLens()
    {
        lastAspect = cam.aspect;
        if (lockHorizontalFov)
        {
            // Convert horizontal FOV to vertical FOV for the current aspect ratio. The FOV is locked on the
            // playfield column, not the whole screen: on screens wider than 9:16 (Windows landscape) the framing
            // stays the phone one and the extra width just shows more of the scene on the sides.
            float aspect = Mathf.Min(cam.aspect, ScreenColumn.Aspect);
            float halfH = horizontalFov * 0.5f * Mathf.Deg2Rad;
            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(halfH) / aspect) * Mathf.Rad2Deg;
        }
        else
        {
            cam.fieldOfView = verticalFov;
        }
    }

#if UNITY_EDITOR
    // Live preview of inspector tweaks outside Play mode.
    private void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;
        currentX = basePosition.x;
        ApplyPose();
        ApplyLens();
    }
#endif
}
