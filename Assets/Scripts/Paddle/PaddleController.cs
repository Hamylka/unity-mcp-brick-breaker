using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Touch/mouse-driven paddle with smooth horizontal movement and a limited depth axis.
/// Drag sideways to move, drag toward the camera to pull the paddle back (slingshot);
/// on release it springs forward, and the forward speed is exposed as <see cref="SlingshotPower"/>
/// so the ball can read it on impact for extra force.
/// Moves a kinematic Rigidbody in FixedUpdate so ball collisions stay reliable.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class PaddleController : MonoBehaviour
{
    [Header("Playfield Bounds (world units, centered on X = 0)")]
    [Tooltip("Half of the playfield's inner width (distance from center to a side wall).")]
    [SerializeField, Min(0.1f)] private float playfieldHalfWidth = 4f;
    [Tooltip("Half of the paddle's width, so its edge stops at the wall instead of its center.")]
    [SerializeField, Min(0.05f)] private float paddleHalfWidth = 0.8f;

    [Header("Depth / Slingshot (relative to the starting Z)")]
    [Tooltip("How far the paddle can be pulled back toward the camera.")]
    [SerializeField, Min(0f)] private float maxPullBack = 1.5f;
    [Tooltip("How far the paddle can advance past its rest position.")]
    [SerializeField, Min(0f)] private float maxPushForward = 0.5f;
    [Tooltip("Return to the rest depth when the finger is lifted (slingshot release).")]
    [SerializeField] private bool springBackOnRelease = true;

    [Header("Drag Sensitivity")]
    [Tooltip("World units travelled sideways when dragging across the full screen width.")]
    [SerializeField, Min(0.1f)] private float horizontalDragRange = 9f;
    [Tooltip("World units travelled in depth when dragging across the full screen height.")]
    [SerializeField, Min(0.1f)] private float depthDragRange = 6f;
    [Tooltip("Editor / keyboard fallback speed in units per second (A/D or arrow keys).")]
    [SerializeField, Min(0f)] private float keyboardSpeed = 10f;

    [Header("Smoothing (seconds, lower = snappier)")]
    [SerializeField, Min(0.001f)] private float horizontalSmoothTime = 0.05f;
    [SerializeField, Min(0.001f)] private float depthSmoothTime = 0.08f;
    [Tooltip("Smoothing used for the forward snap after release. Lower = harder snap = more power.")]
    [SerializeField, Min(0.001f)] private float releaseSmoothTime = 0.06f;

    [Header("Slingshot Power")]
    [Tooltip("Forward speed (units/s) that counts as 100% power.")]
    [SerializeField, Min(0.1f)] private float maxPowerSpeed = 10f;

    /// <summary>Current paddle velocity in world space.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>0..1 forward-snap strength. Read it when the ball hits the paddle.</summary>
    public float SlingshotPower => Mathf.Clamp01(Velocity.z / maxPowerSpeed);

    /// <summary>0..1 how far the paddle is pulled back (for UI / FX).</summary>
    public float PullBackNormalized =>
        maxPullBack > 0f ? Mathf.Clamp01((restZ - targetZ) / maxPullBack) : 0f;

    private Rigidbody body;
    private float restZ;
    private float targetX;
    private float targetZ;
    private float velocityX;
    private float velocityZ;
    private bool dragging;
    private Vector2 lastPointerPosition;

    private float MaxX => Mathf.Max(0f, playfieldHalfWidth - paddleHalfWidth);

    /// <summary>Makes the paddle wider or narrower (upgrades). 1.15 = 15% wider.</summary>
    public void MultiplyWidth(float factor)
    {
        Vector3 scale = transform.localScale;
        scale.x *= factor;
        transform.localScale = scale;
        paddleHalfWidth *= factor;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        restZ = transform.position.z;
        targetX = transform.position.x;
        targetZ = restZ;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
        {
            dragging = false; // paused (menus): ignore the pointer
            return;
        }

        ReadPointer();
        ReadKeyboard();

        targetX = Mathf.Clamp(targetX, -MaxX, MaxX);
        targetZ = Mathf.Clamp(targetZ, restZ - maxPullBack, restZ + maxPushForward);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector3 position = body.position;

        float zSmooth = dragging ? depthSmoothTime : releaseSmoothTime;
        float newX = Mathf.SmoothDamp(position.x, targetX, ref velocityX, horizontalSmoothTime, Mathf.Infinity, dt);
        float newZ = Mathf.SmoothDamp(position.z, targetZ, ref velocityZ, zSmooth, Mathf.Infinity, dt);

        body.MovePosition(new Vector3(newX, position.y, newZ));
        Velocity = new Vector3(velocityX, 0f, velocityZ);
    }

    // Relative dragging: the paddle follows finger movement, so the finger never hides it.
    private void ReadPointer()
    {
        Pointer pointer = Pointer.current;
        bool pressed = pointer != null && pointer.press.isPressed;

        if (pressed)
        {
            Vector2 position = pointer.position.ReadValue();
            if (!dragging)
            {
                dragging = true;
                lastPointerPosition = position;
            }

            Vector2 delta = position - lastPointerPosition;
            lastPointerPosition = position;

            targetX += delta.x / Screen.width * horizontalDragRange;
            targetZ += delta.y / Screen.height * depthDragRange;
        }
        else if (dragging)
        {
            dragging = false;
            if (springBackOnRelease) targetZ = restZ;
        }
    }

    private void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float axis = 0f;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) axis -= 1f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) axis += 1f;
        targetX += axis * keyboardSpeed * Time.deltaTime;
    }

#if UNITY_EDITOR
    // Shows the allowed movement area in the Scene view.
    private void OnDrawGizmosSelected()
    {
        float rz = Application.isPlaying ? restZ : transform.position.z;
        float y = transform.position.y;
        float minZ = rz - maxPullBack;
        float maxZ = rz + maxPushForward;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Vector3 center = new(0f, y, (minZ + maxZ) * 0.5f);
        Vector3 size = new(MaxX * 2f, 0.05f, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, y, rz), new Vector3(playfieldHalfWidth * 2f, 0.05f, 0.02f));
    }

    private void OnValidate()
    {
        paddleHalfWidth = Mathf.Min(paddleHalfWidth, playfieldHalfWidth);
    }
#endif
}
