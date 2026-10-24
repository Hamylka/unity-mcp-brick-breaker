using UnityEngine;

/// <summary>
/// The barrier raised by <see cref="ShieldEffect"/>. The ball reflects off it like off a wall; the first hit
/// breaks it. Built in code so it needs no prefab.
/// </summary>
public class ShieldBarrier : BallSurface
{
    private const float BreakTime = 0.25f;

    /// <summary>The shield currently up, or null.</summary>
    public static ShieldBarrier Current { get; private set; }

    private Collider barrierCollider;
    private Vector3 startScale;
    private float breakT = -1f;

    public static ShieldBarrier Create(Material material, float z, float width, float thickness)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // comes with a BoxCollider
        cube.name = "Shield";
        cube.transform.position = new Vector3(0f, 0f, z);
        // Tall enough to catch the ball, which rolls close to the floor.
        cube.transform.localScale = new Vector3(width, 1f, thickness);
        if (material != null) cube.GetComponent<Renderer>().sharedMaterial = material;

        var barrier = cube.AddComponent<ShieldBarrier>();
        barrier.barrierCollider = cube.GetComponent<Collider>();
        barrier.startScale = cube.transform.localScale;
        Current = barrier;
        return barrier;
    }

    public override void OnBallHit(BallController ball, Collision collision)
    {
        if (breakT >= 0f) return;
        barrierCollider.enabled = false; // the bounce was already computed, so this is safe
        breakT = 0f;
        SoundManager.Play(Sfx.WallBounce);
    }

    private void Update()
    {
        if (breakT < 0f)
        {
            ActiveBonusTracker.Report("shield", "SHIELD", new Color(0.3f, 0.9f, 1f, 1f), -1f, 0f);
            return;
        }
        breakT += Time.deltaTime;
        float k = Mathf.Clamp01(breakT / BreakTime);
        transform.localScale = new Vector3(startScale.x, startScale.y * (1f - k), startScale.z * (1f + k * 2f));
        if (k >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
