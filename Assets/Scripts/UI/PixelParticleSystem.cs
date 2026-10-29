using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pooled UI sparkles: little squares that move on a 4px grid, shrink and fade, for a retro look.
/// Plain class (no MonoBehaviour): the owner creates it with a container and calls <see cref="Tick"/> every frame.
/// </summary>
public class PixelParticleSystem
{
    private sealed class Particle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public float size;
        public float drag;
        public float gravity;
        public Color color;
    }

    private readonly List<Particle> pool = new();
    private int cursor;

    public PixelParticleSystem(RectTransform container, int capacity)
    {
        for (int i = 0; i < capacity; i++)
        {
            var go = new GameObject("P", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(container, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = go.AddComponent<Image>(); // no sprite = a plain square
            image.raycastTarget = false;
            image.enabled = false;
            pool.Add(new Particle { rect = rect, image = image });
        }
    }

    public void Emit(Vector2 position, Vector2 velocity, Color color, float size, float life, float drag, float gravity)
    {
        for (int n = 0; n < pool.Count; n++)
        {
            cursor = (cursor + 1) % pool.Count;
            Particle p = pool[cursor];
            if (p.life > 0f) continue;

            p.pos = position;
            p.vel = velocity;
            p.color = color;
            p.size = size;
            p.life = p.maxLife = life;
            p.drag = drag;
            p.gravity = gravity;
            p.image.enabled = true;
            return;
        }
    }

    /// <summary>An outward explosion of squares in random directions.</summary>
    public void Burst(Vector2 center, int count, IReadOnlyList<Color> colors, float minSpeed, float maxSpeed,
        float minSize, float maxSize, float minLife, float maxLife, float drag, float gravity)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 velocity = direction * Random.Range(minSpeed, maxSpeed);
            Color color = colors[Random.Range(0, colors.Count)];
            Emit(center + direction * Random.Range(0f, 10f), velocity, color,
                Random.Range(minSize, maxSize), Random.Range(minLife, maxLife), drag, gravity);
        }
    }

    /// <summary>Removes every live particle at once.</summary>
    public void Clear()
    {
        foreach (Particle p in pool)
        {
            p.life = 0f;
            p.image.enabled = false;
        }
    }

    public void Tick(float dt)
    {
        foreach (Particle p in pool)
        {
            if (p.life <= 0f) continue;

            p.life -= dt;
            if (p.life <= 0f)
            {
                p.image.enabled = false;
                continue;
            }

            p.vel *= 1f - Mathf.Min(1f, p.drag * dt);
            p.vel.y -= p.gravity * dt;
            p.pos += p.vel * dt;

            float k = p.life / p.maxLife;
            p.rect.anchoredPosition = new Vector2(Mathf.Round(p.pos.x / 4f) * 4f, Mathf.Round(p.pos.y / 4f) * 4f);
            float size = Mathf.Max(4f, Mathf.Round(Mathf.Lerp(0.35f, 1f, k) * p.size / 2f) * 2f);
            p.rect.sizeDelta = new Vector2(size, size);
            Color c = p.color;
            c.a = Mathf.Clamp01(k * 1.8f);
            p.image.color = c;
        }
    }
}
