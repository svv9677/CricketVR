using UnityEngine;

/// <summary>
/// Where the parts of the bat are, in the bat's own frame, so the contact model follows the real
/// blade whatever model or size the bat is.
///
/// Three marker children - Toe, SweetSpot, Shoulder - sit on the bat's long axis, plus the blade's
/// half-width and its face and back planes. They are fitted from the mesh (Fit, or the context
/// menu), and because they are children of the bat they scale with it: making the bat bigger
/// moves the sweet spot with it. Move a marker by hand to override the fit.
///
/// The fit reads the mesh the way you would read a real bat:
///   * toe       the end of the blade (lowest local Z);
///   * shoulder  where the blade narrows into the handle (width drops below 80% of the blade's);
///   * sweet spot the thickest part of the back - the "middle" a bat maker shapes around it,
///               ~0.35 of the blade up from the toe on this bat (0.18 m, as on a real bat);
///   * face / back the flat hitting face (+Y) and the deepest point of the back (-Y).
/// </summary>
public class BatGeometry : MonoBehaviour
{
    public Transform toe;
    public Transform sweetSpot;
    public Transform shoulder;
    [Tooltip("Half the blade's width, local units.")]
    public float halfWidth;
    [Tooltip("Local Y of the flat face (+Y side).")]
    public float faceY;
    [Tooltip("Local Y of the deepest point of the back.")]
    public float backY;

    /// The blade in local units, as the contact model uses it.
    public BatContact.Blade Blade => new BatContact.Blade
    {
        toeZ = toe.localPosition.z,
        shoulderZ = shoulder.localPosition.z,
        sweetZ = sweetSpot.localPosition.z,
        halfWidth = halfWidth,
        faceY = faceY,
        backY = backY,
    };

    public bool IsFitted => toe != null && sweetSpot != null && shoulder != null && halfWidth > 0f;

#if UNITY_EDITOR
    /// Editor only: fitting creates the marker objects, which must exist in the prefab beforehand,
    /// never be made at runtime.
    [ContextMenu("Fit markers to mesh")]
    public void Fit()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;
        BatContact.Blade blade = FitBlade(mf.sharedMesh.vertices);
        halfWidth = blade.halfWidth;
        faceY = blade.faceY;
        backY = blade.backY;
        toe = Marker("Toe", blade.toeZ);
        sweetSpot = Marker("SweetSpot", blade.sweetZ);
        shoulder = Marker("Shoulder", blade.shoulderZ);
    }

    private Transform Marker(string name, float z)
    {
        Transform markers = transform.Find("BatMarkers");
        if (markers == null)
        {
            markers = new GameObject("BatMarkers").transform;
            markers.SetParent(transform, false);
        }
        Transform t = markers.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(markers, false);
        }
        t.localPosition = new Vector3(0f, 0f, z);
        t.localRotation = Quaternion.identity;
        return t;
    }
#endif

    /// Pure: fit the blade from mesh vertices (bat-local). Used by Fit and by the tests.
    public static BatContact.Blade FitBlade(Vector3[] vertices)
    {
        float zMin = float.MaxValue, zMax = float.MinValue;
        foreach (Vector3 v in vertices) { zMin = Mathf.Min(zMin, v.z); zMax = Mathf.Max(zMax, v.z); }

        const int slices = 64;
        float step = (zMax - zMin) / slices;
        var width = new float[slices];
        var top = new float[slices];
        var bottom = new float[slices];
        var any = new bool[slices];
        for (int i = 0; i < slices; i++) { top[i] = float.MinValue; bottom[i] = float.MaxValue; }
        var xMin = new float[slices];
        var xMax = new float[slices];
        for (int i = 0; i < slices; i++) { xMin[i] = float.MaxValue; xMax[i] = float.MinValue; }
        foreach (Vector3 v in vertices)
        {
            int i = Mathf.Clamp((int)((v.z - zMin) / step), 0, slices - 1);
            any[i] = true;
            xMin[i] = Mathf.Min(xMin[i], v.x); xMax[i] = Mathf.Max(xMax[i], v.x);
            top[i] = Mathf.Max(top[i], v.y); bottom[i] = Mathf.Min(bottom[i], v.y);
        }
        float maxWidth = 0f;
        for (int i = 0; i < slices; i++) if (any[i]) { width[i] = xMax[i] - xMin[i]; maxWidth = Mathf.Max(maxWidth, width[i]); }

        // The blade runs from the toe up to where it narrows into the handle.
        int shoulderSlice = slices - 1;
        for (int i = 0; i < slices; i++)
        {
            if (any[i] && width[i] < 0.8f * maxWidth) { shoulderSlice = i; break; }
        }
        float shoulderZ = zMin + shoulderSlice * step;

        float face = float.MinValue, back = float.MaxValue, maxThick = 0f;
        for (int i = 0; i < shoulderSlice; i++)
        {
            if (!any[i]) continue;
            face = Mathf.Max(face, top[i]);
            back = Mathf.Min(back, bottom[i]);
            maxThick = Mathf.Max(maxThick, top[i] - bottom[i]);
        }
        // Sweet spot: centre of the thickest stretch of the back (within 3% of the maximum).
        float sum = 0f; int count = 0;
        for (int i = 0; i < shoulderSlice; i++)
        {
            if (any[i] && top[i] - bottom[i] >= 0.97f * maxThick) { sum += zMin + (i + 0.5f) * step; count++; }
        }
        float sweetZ = count > 0 ? sum / count : zMin + 0.35f * (shoulderZ - zMin);

        return new BatContact.Blade
        {
            toeZ = zMin,
            shoulderZ = shoulderZ,
            sweetZ = sweetZ,
            halfWidth = maxWidth * 0.5f,
            faceY = face,
            backY = back,
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!IsFitted) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        BatContact.Blade b = Blade;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(0f, (b.faceY + b.backY) / 2f, (b.toeZ + b.shoulderZ) / 2f),
                            new Vector3(b.halfWidth * 2f, b.faceY - b.backY, b.shoulderZ - b.toeZ));
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(new Vector3(0f, b.faceY, b.sweetZ), b.halfWidth * 0.25f);
    }
#endif
}
