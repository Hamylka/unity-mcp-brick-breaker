using UnityEngine;

/// <summary>
/// Runtime part of <see cref="LaserEffect"/>, added to the paddle. Fires an instant bolt along +Z from the
/// paddle's middle every interval, only while a ball is in play, and removes itself when the time is up.
/// </summary>
[DisallowMultipleComponent]
public class LaserCannon : MonoBehaviour
{
    private const float BoltFlashTime = 0.15f;
    private const float MaxRange = 40f;

    private float remaining;
    private float interval;
    private float cooldown;
    private Color color;
    private LineRenderer bolt;
    private float flash;

    /// <summary>Starts (or refreshes) the cannon.</summary>
    public void Activate(float duration, float fireInterval, Material material, Color boltColor, float width)
    {
        remaining = duration;
        interval = fireInterval;
        color = boltColor;
        if (cooldown <= 0f || cooldown > interval) cooldown = Mathf.Min(0.3f, interval); // first shot comes quickly

        if (bolt == null)
        {
            var holder = new GameObject("LaserBolt");
            holder.transform.SetParent(transform, false);
            bolt = holder.AddComponent<LineRenderer>();
            bolt.positionCount = 2;
            bolt.useWorldSpace = true;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.receiveShadows = false;
            bolt.enabled = false;
        }
        bolt.sharedMaterial = material;
        bolt.widthMultiplier = width;
    }

    private void Update()
    {
        float dt = Time.deltaTime; // scaled: the cannon pauses with the game
        remaining -= dt;
        if (remaining <= 0f)
        {
            if (bolt != null) Destroy(bolt.gameObject);
            Destroy(this);
            return;
        }

        UpdateBoltFlash(dt);

        cooldown -= dt;
        if (cooldown > 0f || !AnyBallFlying()) return;
        cooldown = interval;
        Fire();
    }

    private void Fire()
    {
        Vector3 origin = transform.position;
        Vector3 end = origin + Vector3.forward * MaxRange;

        // The nearest solid thing in the way, ignoring balls and the paddle itself.
        float nearest = MaxRange;
        BrickStack target = null;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.forward, MaxRange, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= nearest) continue;
            if (hit.collider.GetComponentInParent<BallController>() != null) continue;
            if (hit.collider.GetComponentInParent<PaddleController>() != null) continue;
            nearest = hit.distance;
            target = hit.collider.GetComponentInParent<BrickStack>();
        }
        end = origin + Vector3.forward * nearest;
        if (target != null) target.TakeDamage(CurrentDamage());

        bolt.SetPosition(0, origin);
        bolt.SetPosition(1, end);
        bolt.enabled = true;
        flash = BoltFlashTime;
        SetBoltAlpha(1f);
        SoundManager.Play(Sfx.OrbLaunch);
    }

    private void UpdateBoltFlash(float dt)
    {
        if (flash <= 0f) return;
        flash -= dt;
        if (flash <= 0f)
        {
            bolt.enabled = false;
            return;
        }
        SetBoltAlpha(flash / BoltFlashTime);
    }

    private void SetBoltAlpha(float alpha)
    {
        Color tint = color;
        tint.a *= alpha;
        bolt.startColor = tint;
        bolt.endColor = tint;
    }

    // The strongest ball sets the damage, so upgrades from the cards also boost the laser.
    private static int CurrentDamage()
    {
        int damage = 1;
        foreach (BallController ball in BallController.Active)
            if (!ball.IsExtra) damage = Mathf.Max(damage, ball.Damage);
        return damage;
    }

    private static bool AnyBallFlying()
    {
        foreach (BallController ball in BallController.Active)
            if (ball.IsFlying) return true;
        return false;
    }
}
