using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns one wall buff zone at a time on a random side wall. Rules: a zone appears after
/// <see cref="spawnInterval"/> seconds and stays <see cref="zoneLifetime"/> seconds. If a ball takes it, the
/// interval restarts only once the buff has faded; if it expires untouched, the interval restarts right away.
/// The clock only runs while a ball is in play. Buffs are the <see cref="WallBuff"/> assets in
/// <c>Resources/WallBuffs</c>. Builds itself at startup (no scene setup).
/// </summary>
public class WallBuffZoneSpawner : MonoBehaviour
{
    private enum State { Cooldown, ZoneUp, BuffRunning }

    [SerializeField, Min(1f)] private float spawnInterval = 20f;
    [SerializeField, Min(1f)] private float zoneLifetime = 10f;
    [Tooltip("Zone length along the wall, in world units.")]
    [SerializeField, Min(0.5f)] private float zoneLength = 2.5f;
    [Tooltip("Where along the wall (0 = near the paddle, 1 = far end) a zone may appear.")]
    [SerializeField, Range(0f, 1f)] private float minT = 0.2f;
    [SerializeField, Range(0f, 1f)] private float maxT = 0.7f;

    private readonly List<Wall> walls = new();
    private WallBuff[] buffs;
    private State state = State.Cooldown;
    private float timer;
    private WallBuffZone zone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Create();
        // The scene is reloaded by the Restart button: bring the spawner back with it.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Create();
    }

    private static void Create()
    {
        if (FindAnyObjectByType<WallBuffZoneSpawner>() == null)
            new GameObject("WallBuffZoneSpawner").AddComponent<WallBuffZoneSpawner>();
    }

    private void Awake()
    {
        buffs = Resources.LoadAll<WallBuff>("WallBuffs");
        foreach (Wall wall in FindObjectsByType<Wall>())
            if (wall.IsSideWall) walls.Add(wall);

        timer = spawnInterval;
        if (buffs.Length == 0 || walls.Count == 0) enabled = false;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Cooldown:
                if (!BallInPlay()) return;
                timer -= Time.deltaTime;
                if (timer <= 0f) SpawnZone();
                break;

            case State.BuffRunning:
                if (!BallBuffAura.AnyActive) Restart();
                break;
        }
    }

    private static bool BallInPlay()
    {
        foreach (BallController ball in BallController.Active)
            if (ball.IsFlying) return true;
        return false;
    }

    private void SpawnZone()
    {
        Wall wall = walls[Random.Range(0, walls.Count)];
        zone = WallBuffZone.Create(wall, PickBuff(), Random.Range(minT, maxT), zoneLength, zoneLifetime);
        zone.Ended += HandleZoneEnded;
        state = State.ZoneUp;
    }

    private void HandleZoneEnded(WallBuffZone ended, bool tookBuff)
    {
        ended.Ended -= HandleZoneEnded;
        zone = null;
        // The aura is added in the same frame the zone ends, so AnyActive is already true here.
        if (tookBuff) state = State.BuffRunning;
        else Restart();
    }

    private void Restart()
    {
        timer = spawnInterval;
        state = State.Cooldown;
    }

    private WallBuff PickBuff()
    {
        float total = 0f;
        foreach (WallBuff buff in buffs) total += buff.weight;
        if (total <= 0f) return buffs[0];

        float roll = Random.value * total;
        foreach (WallBuff buff in buffs)
        {
            roll -= buff.weight;
            if (roll <= 0f) return buff;
        }
        return buffs[^1];
    }

    [ContextMenu("Debug: Spawn Zone Now")]
    private void DebugSpawn()
    {
        if (state == State.Cooldown) SpawnZone();
    }
}
