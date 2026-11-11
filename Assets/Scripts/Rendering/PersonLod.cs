using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// People further away are drawn with lighter meshes (PersonLodTable): beyond MidBeyond about
/// two fifths of the triangles, beyond FarBeyond under a fifth, swapped back as they come close.
///
/// Measured 2026-11-11 in a match, from the batter's eyes (CricketVR.unity, packed stadium): facing
/// the bowler 787k triangles were in view and 219k more drawn into the shadow map, facing the
/// keeper 272k and 146k. Almost all of it is people, ~48k triangles each and skinned every frame,
/// and facing the bowler there are about fourteen of them against three or four behind ("only
/// when I look forward, at the bowler, it becomes very laggy"). The crowd stands are ~7k.
///
/// Distance is from the nearest camera drawing the scene; a zoomed one (a replay's broadcast
/// camera) counts as nearer by its zoom. Each step back to a heavier mesh waits until Return metres
/// nearer than the step out, so someone standing at the line does not flick between the two.
/// Only the mesh changes: bones, materials and animation are the renderer's own.
/// </summary>
public class PersonLod : MonoBehaviour
{
    /// Beyond these (effective metres) the mid and the far mesh.
    public const float MidBeyond = 12f, FarBeyond = 25f;
    private const float Return = 1.5f;
    /// How often the scene is searched for people (new ones appear: fielder looks, replay stand-ins).
    private const float RescanSeconds = 1f;
    /// The field of view a camera counts as unzoomed at (the headset's is about this).
    private const float PlainFov = 90f;

    private struct Person
    {
        public SkinnedMeshRenderer skin;
        public Mesh full, mid, far;
    }

    private static PersonLod instance;
    private PersonLodTable table;
    private readonly List<Person> people = new List<Person>();
    private readonly HashSet<SkinnedMeshRenderer> known = new HashSet<SkinnedMeshRenderer>();
    private Camera[] cameras = new Camera[8];
    private float rescanAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;
        var table = Resources.Load<PersonLodTable>(PersonLodTable.ResourceName);
        if (table == null)
            return;
        var go = new GameObject("Person LOD");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PersonLod>();
        instance.table = table;
        SceneManager.sceneLoaded += (s, m) => instance.rescanAt = 0f;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= rescanAt)
        {
            rescanAt = Time.unscaledTime + RescanSeconds;
            Rescan();
        }
        int count = Camera.allCamerasCount;
        if (cameras.Length < count)
            cameras = new Camera[count * 2];
        count = Camera.GetAllCameras(cameras);
        for (int i = people.Count - 1; i >= 0; i--)
        {
            Person p = people[i];
            if (p.skin == null)
            {
                known.Remove(p.skin);
                people.RemoveAt(i);
                continue;
            }
            if (!p.skin.isVisible && p.skin.sharedMesh == p.far)
                continue;       // out of every view: nothing to gain from deciding now
            Mesh want = Choose(p, ViewDistance(p.skin.bounds.center, count));
            if (want != p.skin.sharedMesh)
                p.skin.sharedMesh = want;
        }
    }

    /// The mesh for someone this far away, given the one they have now.
    private static Mesh Choose(Person p, float d)
    {
        Mesh now = p.skin.sharedMesh;
        int level = Level(now == p.far ? 2 : now == p.mid ? 1 : 0, d);
        return level == 2 ? p.far : level == 1 ? p.mid : p.full;
    }

    /// 0 full, 1 mid, 2 far: the level for someone `d` effective metres away who is at `now`.
    public static int Level(int now, float d)
    {
        int wanted = d > FarBeyond ? 2 : d > MidBeyond ? 1 : 0;
        if (wanted >= now)
            return wanted;
        // Coming nearer: only once clearly inside the line.
        if (now == 2 && d > FarBeyond - Return)
            return 2;
        return d > MidBeyond - Return ? 1 : 0;
    }

    /// Metres to the nearest camera drawing the scene, shortened by that camera's zoom.
    private float ViewDistance(Vector3 at, int count)
    {
        float best = float.PositiveInfinity;
        for (int c = 0; c < count; c++)
        {
            Camera cam = cameras[c];
            if (cam == null || !cam.isActiveAndEnabled)
                continue;
            float d = Vector3.Distance(cam.transform.position, at);
            if (!cam.stereoEnabled && cam.fieldOfView < PlainFov)
                d *= Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(PlainFov * 0.5f * Mathf.Deg2Rad);
            best = Mathf.Min(best, d);
        }
        return best;
    }

    private void Rescan()
    {
        known.RemoveWhere(s => s == null);
        foreach (SkinnedMeshRenderer skin in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            if (known.Contains(skin))
                continue;
            int at = table.Find(skin.sharedMesh);
            if (at < 0)
                continue;
            known.Add(skin);
            people.Add(new Person { skin = skin, full = table.full[at], mid = table.mid[at], far = table.far[at] });
        }
    }
}
