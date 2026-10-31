using UnityEngine;

/// <summary>
/// Runs on a <see cref="BrickKind.Healer"/> brick: every few seconds of play it restores hit points to the damaged
/// bricks right next to it (the four direct neighbours), with a green beam to each. Kill it first.
/// Added by <see cref="BrickGrid"/> when the brick spawns.
/// </summary>
public class BrickHealer : MonoBehaviour
{
    private static readonly Color HealColor = new(0.35f, 1f, 0.5f);
    private static readonly System.Collections.Generic.List<Brick> live = new();

    private Brick brick;
    private BrickGrid grid;
    private float interval;
    private float percent;
    private float timer;

    public void Setup(BrickGrid owner, float healInterval, float healPercent)
    {
        brick = GetComponent<Brick>();
        grid = owner;
        interval = Mathf.Max(0.5f, healInterval);
        percent = Mathf.Clamp01(healPercent);
        timer = Random.Range(0f, interval * 0.5f); // healers on the same wall do not pulse in sync
    }

    private void Update()
    {
        if (brick == null || grid == null || !brick.IsAlive) return;
        if (BallController.Active.Count == 0) return; // like the wall, it waits while no ball is flying

        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;
        Heal();
    }

    private void Heal()
    {
        grid.GetLiveBricks(live);
        float width = grid.BrickWidth, depth = grid.CellDepth;
        Vector3 origin = brick.transform.position;
        int healed = 0;
        foreach (Brick other in live)
        {
            if (other == brick || other.HitPoints >= other.MaxHitPoints) continue;
            Vector3 d = other.transform.position - origin;
            float nx = d.x / width, nz = d.z / depth;
            if (nx * nx + nz * nz > 1.05f * 1.05f) continue; // the four direct neighbours only

            other.Heal(Mathf.Max(1, Mathf.CeilToInt(other.MaxHitPoints * percent)));
            other.Pulse(HealColor * 1.4f, 0.3f);
            ChainBolt.Spawn(origin, other.transform.position, HealColor * 1.5f, 0.015f, 0.2f, 0.18f);
            healed++;
        }
        if (healed == 0) return;

        brick.NotifyHealCast();
        brick.Pulse(HealColor * 1.6f, 0.3f);
        NeonFx.Ring(origin, HealColor, 14, 2.5f, 0.07f, 0.3f);
        SoundManager.Play(Sfx.ZoneSpawn, 1.4f); // placeholder cue: a soft bell
    }
}
