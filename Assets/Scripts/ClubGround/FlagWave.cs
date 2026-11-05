using UnityEngine;

/// <summary>
/// The club's flag, flying from the flagpole: a 12 x 8 grid of quads built here (no mesh asset),
/// rippled on the CPU 30 times a second. 117 vertices - cheaper than a cloth component or a vertex
/// shader of its own, and it needs no material but the ground's.
///
/// The object's origin is the top of the hoist (where the flag meets the pole): the flag runs out
/// along local +X and hangs down local -Y, so the builder puts the object at the pole's top and
/// turns it to the wind. The hoist edge stays still; the ripple grows toward the fly.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class FlagWave : MonoBehaviour
{
    [Tooltip("Width (along the wind) and depth (down the pole) of the flag, in metres.")]
    [SerializeField] private Vector2 size = new Vector2(1.8f, 0.9f);

    private const int Cols = 12, Rows = 8;
    private const float Rate = 30f;               // updates a second
    private const float Wavelength = 1.1f;        // metres along the flag
    private const float Speed = 2.6f;             // metres a second the ripple runs out
    private const float Amplitude = 0.12f;        // at the fly, fraction of the width

    private Mesh mesh;
    private Vector3[] rest, moved;
    private Renderer view;
    private float nextUpdate;

    private void Awake()
    {
        mesh = Build(size, out rest);
        moved = (Vector3[])rest.Clone();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        view = GetComponent<Renderer>();
        Wave(0f);
    }

    private void Update()
    {
        if (Time.time < nextUpdate || (view != null && !view.isVisible))
            return;
        nextUpdate = Time.time + 1f / Rate;
        Wave(Time.time);
    }

    /// The ripple at time `t`: out along the flag, larger toward the fly, with a slower flutter
    /// across it so the waves are not all the same.
    private void Wave(float t)
    {
        float k = 2f * Mathf.PI / Wavelength, w = k * Speed;
        for (int i = 0; i < rest.Length; i++)
        {
            Vector3 p = rest[i];
            float along = p.x / Mathf.Max(size.x, 1e-3f);
            float swing = Amplitude * size.x * along;
            float flutter = 0.35f * Mathf.Sin(1.7f * t - p.y * 2.3f);
            moved[i] = new Vector3(p.x * (1f - 0.04f * along), p.y,
                                   swing * (Mathf.Sin(k * p.x - w * t) + flutter));
        }
        mesh.vertices = moved;            // the bounds follow (Mesh.vertices recomputes them)
        mesh.RecalculateNormals();
    }

    private static Mesh Build(Vector2 size, out Vector3[] verts)
    {
        verts = new Vector3[(Cols + 1) * (Rows + 1)];
        var uv = new Vector2[verts.Length];
        for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Cols; x++)
            {
                int i = y * (Cols + 1) + x;
                float u = (float)x / Cols, v = (float)y / Rows;
                verts[i] = new Vector3(u * size.x, -v * size.y, 0f);
                uv[i] = new Vector2(u, 1f - v);
            }
        var tris = new int[Cols * Rows * 6];
        int n = 0;
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
            {
                int a = y * (Cols + 1) + x, b = a + 1, c = a + Cols + 1, d = c + 1;
                tris[n++] = a; tris[n++] = b; tris[n++] = c;
                tris[n++] = b; tris[n++] = d; tris[n++] = c;
            }
        var mesh = new Mesh { name = "Club Flag (waving)" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}
