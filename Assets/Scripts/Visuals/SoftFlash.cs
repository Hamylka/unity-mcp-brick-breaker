using UnityEngine;

/// <summary>
/// A one-shot round glow that swells and fades out, facing the camera: <c>SoftFlash.Spawn(position, color, size)</c>.
/// Use it for big flashes (explosions, blasts); <see cref="NeonFx.Flash"/> is a flat square and only reads well at
/// pixel size. Built on <see cref="NeonGlow"/>'s radial texture, runs on scaled time like the particles.
/// </summary>
public class SoftFlash : MonoBehaviour
{
    private MeshRenderer quad;
    private Color color;
    private float size;
    private float lifetime;
    private float age;

    /// <summary>Spawns a glow of <paramref name="size"/> world units (diameter at its widest), HDR color welcome.</summary>
    public static void Spawn(Vector3 position, Color color, float size, float lifetime = 0.3f)
    {
        // The quad faces the tilted camera, so its lower half would sink into the floor and get cut. Pull it toward
        // the camera far enough to clear the floor, and shrink it by the same perspective ratio so the size on
        // screen stays the same.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = cam.transform.position - position;
            float distance = toCamera.magnitude;
            float pull = Mathf.Min(size * 0.5f, distance * 0.8f);
            position += toCamera / distance * pull;
            size *= (distance - pull) / distance;
        }

        var go = new GameObject("SoftFlash");
        go.transform.position = position;
        var flash = go.AddComponent<SoftFlash>();
        flash.quad = NeonGlow.CreateQuad(go.transform, "Glow", 1f);
        flash.color = color;
        flash.size = size;
        flash.lifetime = Mathf.Max(0.01f, lifetime);
        flash.Apply(0f);
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        float k = age / lifetime;
        if (k >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(k);
    }

    private void Apply(float k)
    {
        // Swells fast (ease-out) from 60% to full size while the light fades with a squared falloff.
        float grow = 1f - (1f - k) * (1f - k);
        quad.transform.localScale = Vector3.one * (size * Mathf.Lerp(0.6f, 1f, grow));
        float fade = (1f - k) * (1f - k);
        NeonGlow.SetColor(quad, color * fade);

        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation; // the quad faces -Z, i.e. the camera
    }
}
