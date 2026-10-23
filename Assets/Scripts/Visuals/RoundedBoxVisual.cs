using UnityEngine;

/// <summary>
/// Replaces this object's cube mesh with a rounded neon box of the same world size, and rebuilds it when the
/// object is rescaled (e.g. the Wide Paddle upgrade) so the corners stay round. Visual only: colliders are untouched.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class RoundedBoxVisual : MonoBehaviour
{
    [SerializeField, Min(0f)] private float cornerRadius = 0.12f;
    [Tooltip("Width of the glowing outline band, in world units.")]
    [SerializeField, Min(0.005f)] private float outlineWidth = 0.05f;

    private MeshFilter filter;
    private Mesh mesh;
    private Vector3 builtScale;

    private void OnEnable()
    {
        filter = GetComponent<MeshFilter>();
        Rebuild();
    }

    private void OnDisable()
    {
        DestroyMesh();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled) builtScale = Vector3.zero; // rebuild on the next LateUpdate
    }

    private void LateUpdate()
    {
        if (transform.lossyScale != builtScale) Rebuild();
    }

    private void Rebuild()
    {
        if (filter == null) return;
        Vector3 scale = transform.lossyScale;
        if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f) return;

        DestroyMesh();
        mesh = RoundedBoxMesh.Build(scale, cornerRadius, outlineWidth, scale); // a unit cube scaled = this world size
        mesh.hideFlags = HideFlags.DontSave;
        filter.sharedMesh = mesh;
        builtScale = scale;
    }

    private void DestroyMesh()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
        mesh = null;
    }
}
