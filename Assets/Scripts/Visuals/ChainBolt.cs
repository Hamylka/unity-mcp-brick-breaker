using UnityEngine;

/// <summary>
/// A short-lived jagged lightning bolt between two points (Chain Lightning). Spawns itself with
/// <see cref="Spawn"/>, flickers for a fraction of a second and destroys itself. Uses the additive NeonFx material.
/// </summary>
public class ChainBolt : MonoBehaviour
{
    private const int Segments = 7;
    private const float Life = 0.22f;
    private const float Jitter = 0.18f;

    private static Material material;

    private LineRenderer line;
    private Vector3 from;
    private Vector3 to;
    private Color color;
    private float age;

    public static void Spawn(Vector3 from, Vector3 to, Color color)
    {
        if (material == null) material = Resources.Load<Material>("FX/M_NeonFx");

        var go = new GameObject("ChainBolt");
        var bolt = go.AddComponent<ChainBolt>();
        bolt.from = from;
        bolt.to = to;
        bolt.color = color;

        bolt.line = go.AddComponent<LineRenderer>();
        bolt.line.positionCount = Segments + 1;
        bolt.line.useWorldSpace = true;
        bolt.line.sharedMaterial = material;
        bolt.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bolt.line.receiveShadows = false;
        bolt.line.numCornerVertices = 2;
        bolt.Reshape();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Life)
        {
            Destroy(gameObject);
            return;
        }

        float k = 1f - age / Life;
        line.widthMultiplier = Mathf.Lerp(0.04f, 0.2f, k);
        Color c = color * (1f + 3f * k); // HDR-ish push so the bloom picks it up
        c.a = 1f;
        line.startColor = c;
        line.endColor = c;
        Reshape(); // new jagged path every frame: the flicker
    }

    private void Reshape()
    {
        Vector3 along = to - from;
        Vector3 side = Vector3.Cross(along, Vector3.up).normalized;
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (i > 0 && i < Segments)
                p += (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-0.5f, 0.5f)) * Jitter;
            line.SetPosition(i, p);
        }
    }
}
