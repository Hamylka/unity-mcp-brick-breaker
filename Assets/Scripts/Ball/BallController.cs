using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Arcade ball: constant speed, perfect reflections, no gravity, locked to the floor plane (XZ).
/// The ball's own <see cref="Direction"/> is the source of truth; velocity is rewritten from it every
/// physics step, so friction, damping or solver quirks can never slow it down.
/// Starts stuck to the paddle and launches on tap/release or Space.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
[DisallowMultipleComponent]
public class BallController : MonoBehaviour
{
    private enum State { Waiting, Flying, Lost }

    [Header("Speed")]
    [SerializeField, Min(0.1f)] private float baseSpeed = 10f;
    [Tooltip("Seconds for a 100% speed boost to fade back to the base speed.")]
    [SerializeField, Min(0.05f)] private float boostDecayTime = 1f;
    [Tooltip("Upper limit of the temporary boost (1 = +100% speed).")]
    [SerializeField, Range(0f, 2f)] private float maxBoost = 1f;

    [Header("Combat")]
    [Tooltip("Hit points removed from a brick per hit. Upgrades can raise it.")]
    [SerializeField, Min(1)] private int damage = 1;

    [Header("Safety")]
    [Tooltip("Minimum angle from the horizontal (X) axis. Prevents endless side-to-side bouncing.")]
    [SerializeField, Range(5f, 45f)] private float minAngleFromHorizontal = 12f;

    [Header("Launch")]
    [Tooltip("Paddle the ball rides on before launch.")]
    [SerializeField] private Transform paddle;
    [Tooltip("Ball offset from the paddle center while waiting (Y is ignored; the ball keeps its height).")]
    [SerializeField] private Vector3 restOffset = new(0f, 0f, 0.55f);
    [Tooltip("Random launch spread around straight ahead, in degrees.")]
    [SerializeField, Range(0f, 45f)] private float launchAngleRange = 15f;

    [Header("Out of Bounds")]
    [Tooltip("World Z below which the ball counts as lost.")]
    [SerializeField] private float loseZ = -3f;

    /// <summary>Raised when the ball falls past the paddle. It then returns to the paddle, unless <see cref="AutoReset"/> is off.</summary>
    public event Action<BallController> BallLost;
    /// <summary>Raised after every bounce, once the final direction is set.</summary>
    public event Action<BallController, Collision> Bounced;

    public Vector3 Position => body.position;
    public Vector3 Direction { get; private set; } = Vector3.forward;
    public float CurrentSpeed => baseSpeed * (1f + boost);
    public bool IsFlying => state == State.Flying;

    /// <summary>
    /// When true (default) a lost ball goes back to the paddle. When false it stops where it is and stays lost,
    /// so a game manager can end the run.
    /// </summary>
    public bool AutoReset { get; set; } = true;

    /// <summary>Hit points removed from a brick per hit.</summary>
    public int Damage => Mathf.Max(1, Mathf.RoundToInt(damage * DamageMultiplier));

    /// <summary>Temporary damage factor (wall buffs). 1 = normal.</summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>
    /// While true the ball passes through bricks: each brick it enters takes a normal hit once and the ball
    /// does not bounce. Walls and the paddle still bounce it.
    /// </summary>
    public bool Phasing { get; set; }

    private const float PhaseLookAhead = 0.3f;
    private const float PhaseExitMargin = 0.05f;
    private readonly HashSet<Collider> phaseIgnored = new(); // brick colliders collisions are switched off for
    private readonly HashSet<Collider> phaseInside = new();  // ...that already took their hit for this pass
    private readonly List<Collider> phaseScratch = new();
    private readonly Collider[] phaseHits = new Collider[16];
    private SphereCollider sphere;

    private static readonly List<BallController> active = new();

    /// <summary>Every enabled ball: the main one and any temporary extras (multi-ball).</summary>
    public static IReadOnlyList<BallController> Active => active;

    /// <summary>True for temporary copies made by <see cref="SpawnExtra"/>. Losing one never ends the run.</summary>
    public bool IsExtra { get; private set; }

    /// <summary>
    /// Spawns a copy of this ball (same speed and damage, upgrades included) flying in <paramref name="direction"/>.
    /// The copy ignores collisions with the other balls and is destroyed if it falls past the paddle.
    /// </summary>
    public BallController SpawnExtra(Vector3 direction)
    {
        BallController extra = Instantiate(this, transform.position, Quaternion.identity);
        extra.name = $"{name} (Extra)";
        extra.IsExtra = true;
        extra.AutoReset = false;
        extra.StartFlying(body.position, direction);

        SphereCollider ownCollider = extra.GetComponent<SphereCollider>();
        foreach (BallController other in active)
            if (other != extra) Physics.IgnoreCollision(ownCollider, other.GetComponent<SphereCollider>());
        return extra;
    }

    /// <summary>Multiplies the base speed (upgrades). 1.1 = 10% faster.</summary>
    public void MultiplyBaseSpeed(float factor) => baseSpeed *= Mathf.Max(0.1f, factor);

    /// <summary>Permanently changes the damage per hit (upgrades, perks).</summary>
    public void AddDamage(int amount) => damage = Mathf.Max(1, damage + amount);

    private Rigidbody body;
    private State state = State.Waiting;
    private float boost;
    private float planeY;
    private float lastZSign = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        planeY = transform.position.y;
        ResetToPaddle();
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        if (Time.timeScale == 0f) return; // paused (menus): ignore taps
        if (state == State.Waiting && LaunchRequested())
            Launch();
    }

    private void FixedUpdate()
    {
        if (state == State.Lost) return;

        if (state == State.Waiting)
        {
            FollowPaddle();
            return;
        }

        boost = Mathf.Max(0f, boost - Time.fixedDeltaTime / boostDecayTime);
        UpdatePhasing();

        // Re-apply every step so the speed can never drift.
        Direction = ClampDirection(Direction);
        body.linearVelocity = Direction * CurrentSpeed;

        if (body.position.z < loseZ)
        {
            if (AutoReset)
            {
                BallLost?.Invoke(this);
                ResetToPaddle();
            }
            else
            {
                // Stop for good (set the state first so the event is raised only once).
                state = State.Lost;
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
                BallLost?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Phasing: bricks near the ball get their collision switched off before the ball touches them and take one
    /// normal hit; they are switched back on once the ball has left them (and the phasing is over).
    /// </summary>
    private void UpdatePhasing()
    {
        if (!Phasing && phaseIgnored.Count == 0) return;
        if (sphere == null) sphere = GetComponent<SphereCollider>();

        Vector3 position = body.position;
        float radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

        // Forget bricks the ball has left; give their collision back once phasing is over.
        phaseScratch.Clear();
        phaseScratch.AddRange(phaseIgnored);
        foreach (Collider brick in phaseScratch)
        {
            if (brick == null) { phaseIgnored.Remove(brick); phaseInside.Remove(brick); continue; }
            bool left = Vector3.Distance(brick.ClosestPoint(position), position) > radius + PhaseExitMargin;
            if (!left) continue;
            phaseInside.Remove(brick);
            if (Phasing) continue;
            Physics.IgnoreCollision(sphere, brick, false);
            phaseIgnored.Remove(brick);
        }
        if (!Phasing) return;

        int count = Physics.OverlapSphereNonAlloc(position, radius + PhaseLookAhead, phaseHits);
        for (int i = 0; i < count; i++)
        {
            Collider other = phaseHits[i];
            BrickStack stack = other.GetComponentInParent<BrickStack>();
            if (stack == null) continue;

            if (phaseIgnored.Add(other)) Physics.IgnoreCollision(sphere, other, true);
            if (!phaseInside.Add(other)) continue;

            // Unrounded damage with the fraction carried on the brick, so a halved 1-damage ball still
            // removes an HP every second brick it passes through.
            Brick bottom = stack.Bottom;
            if (bottom == null) continue;
            bottom.DamageCarry += damage * DamageMultiplier;
            int whole = Mathf.FloorToInt(bottom.DamageCarry + 0.001f);
            if (whole <= 0) continue;
            bottom.DamageCarry = Mathf.Max(0f, bottom.DamageCarry - whole);
            stack.TakeDamage(whole);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.Flying) return;

        // 1) Default arcade reflection on the XZ plane.
        ContactPoint contact = collision.GetContact(0);
        Vector3 normal = contact.normal;
        normal.y = 0f;
        if (normal.sqrMagnitude > 1e-4f)
        {
            normal.Normalize();
            // Make the normal face the ball, then only reflect if we are actually moving into the surface
            // (guards against double reflection when two contacts report in the same step).
            if (Vector3.Dot(body.position - contact.point, normal) < 0f) normal = -normal;
            if (Vector3.Dot(Direction, normal) < 0f)
                Direction = Vector3.Reflect(Direction, normal);
        }

        // 2) Let the surface customise the result (paddle angles, wall zones, ...).
        BallSurface surface = collision.collider.GetComponentInParent<BallSurface>();
        if (surface != null) surface.OnBallHit(this, collision);

        // 3) Apply, including the anti-horizontal-loop safeguard.
        Direction = ClampDirection(Direction);
        body.linearVelocity = Direction * CurrentSpeed;
        Bounced?.Invoke(this, collision);
    }

    /// <summary>Overrides the travel direction (flattened to XZ). Intended for surfaces and effects.</summary>
    public void SetDirection(Vector3 direction)
    {
        Direction = ClampDirection(direction);
        if (state == State.Flying) body.linearVelocity = Direction * CurrentSpeed;
    }

    /// <summary>Adds a temporary speed boost (0.5 = +50%) that fades over Boost Decay Time.</summary>
    public void AddSpeedBoost(float amount)
    {
        boost = Mathf.Clamp(Mathf.Max(boost, amount), 0f, maxBoost);
    }

    /// <summary>Launches the ball from the paddle with a small random spread.</summary>
    public void Launch()
    {
        float angle = UnityEngine.Random.Range(-launchAngleRange, launchAngleRange) * Mathf.Deg2Rad;
        Direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        boost = 0f;
        state = State.Flying;
        body.isKinematic = false;
        body.linearVelocity = Direction * CurrentSpeed;
        SoundManager.Play(Sfx.Launch);
    }

    /// <summary>
    /// Turns a temporary extra into a normal ball: it stops fading, regains its size and no longer expires.
    /// Used when the main ball is lost while an extra is still flying.
    /// </summary>
    public void PromoteToMain()
    {
        if (!IsExtra) return;
        IsExtra = false;
        name = name.Replace(" (Extra)", "");
        if (TryGetComponent(out TemporaryBall temporary)) temporary.Cancel();
    }

    // Starts flying right away from a given spot (extras skip the paddle and the launch sound).
    private void StartFlying(Vector3 position, Vector3 direction)
    {
        position.y = planeY;
        transform.position = position;
        body.isKinematic = false;
        body.position = position;
        boost = 0f;
        Direction = ClampDirection(direction);
        state = State.Flying;
        body.linearVelocity = Direction * CurrentSpeed;

        // The copied trail would draw a line from the original ball's position.
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
    }

    /// <summary>Puts the ball back on the paddle, waiting for launch.</summary>
    public void ResetToPaddle()
    {
        state = State.Waiting;
        boost = 0f;
        if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        FollowPaddle();
    }

    private void FollowPaddle()
    {
        if (paddle == null) return;
        Vector3 target = paddle.position + restOffset;
        target.y = planeY;
        body.position = target;
    }

    private static bool LaunchRequested()
    {
        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasReleasedThisFrame) return true;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    // Keeps the direction planar and never too close to horizontal (X axis).
    private Vector3 ClampDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-6f) direction = new Vector3(0f, 0f, lastZSign);
        direction.Normalize();

        if (Mathf.Abs(direction.z) > 0.01f) lastZSign = Mathf.Sign(direction.z);

        float minZ = Mathf.Sin(minAngleFromHorizontal * Mathf.Deg2Rad);
        if (Mathf.Abs(direction.z) < minZ)
        {
            float xSign = direction.x >= 0f ? 1f : -1f;
            direction = new Vector3(xSign * Mathf.Sqrt(1f - minZ * minZ), 0f, lastZSign * minZ);
        }
        return direction;
    }
}
