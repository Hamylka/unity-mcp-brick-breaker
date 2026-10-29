using System;
using UnityEngine;

/// <summary>
/// Falls toward the paddle at a constant speed. When the paddle touches it, raises <see cref="Collected"/>.
/// It does not apply the ability itself, the <see cref="BonusDropper"/> does.
/// Driven by Rigidbody velocity (no gravity, locked to its start height), with a trigger collider.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BonusItem : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField, Min(0f)] private float fallSpeed = 4f;
    [Tooltip("World Z below which an uncaught item disappears.")]
    [SerializeField] private float despawnZ = -4f;
    [Tooltip("Spin around the vertical axis, degrees per second.")]
    [SerializeField] private float spinSpeed = 180f;
    [SerializeField] private Renderer targetRenderer;

    public BonusDefinition Definition { get; private set; }

    /// <summary>The item, and the paddle that caught it.</summary>
    public event Action<BonusItem, PaddleController> Collected;

    private Rigidbody body;
    private bool collected;
    private PaddleController paddleRef;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY
                         | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void Initialize(BonusDefinition definition)
    {
        Definition = definition;

        // The neon crystal look replaces the placeholder sphere when the prefab has it.
        if (TryGetComponent(out BonusItemVisual visual))
        {
            visual.Build(definition.Color, targetRenderer);
            return;
        }

        if (targetRenderer == null) return;
        var block = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, definition.Color);
        targetRenderer.SetPropertyBlock(block);
    }

    private void FixedUpdate()
    {
        // Constant velocity, re-applied every step so nothing can alter the fall.
        body.linearVelocity = Vector3.back * (fallSpeed * PowerUpStats.BonusFallSpeed);
        body.angularVelocity = Vector3.up * (spinSpeed * Mathf.Deg2Rad);

        if (body.position.z < despawnZ) { Destroy(gameObject); return; }
        if (PowerUpStats.CatchZoneScale > 1.001f) CheckWideCatch();
    }

    // Catch Zone: the paddle also picks the item up inside a wider strip, without touching the paddle's collider.
    private void CheckWideCatch()
    {
        if (collected) return;
        paddleRef ??= FindAnyObjectByType<PaddleController>();
        if (paddleRef == null || !paddleRef.TryGetComponent(out Collider paddleCollider)) return;

        Bounds bounds = paddleCollider.bounds;
        float halfWidth = bounds.extents.x * PowerUpStats.CatchZoneScale;
        Vector3 p = body.position;
        if (Mathf.Abs(p.x - bounds.center.x) > halfWidth) return;
        if (p.z < bounds.min.z - 0.3f || p.z > bounds.max.z + 0.3f) return;
        Collect(paddleRef);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        PaddleController paddle = other.GetComponentInParent<PaddleController>();
        if (paddle != null) Collect(paddle);
    }

    private void Collect(PaddleController paddle)
    {
        collected = true;
        Collected?.Invoke(this, paddle);
        Destroy(gameObject);
    }
}
