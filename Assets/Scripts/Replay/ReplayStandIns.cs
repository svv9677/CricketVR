using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the replay's stand-ins (ReplayActors): a copy of an actor's transforms carrying copies
/// of its mesh renderers - same meshes, materials and bones - and nothing else. No scripts,
/// animator, rigidbodies or colliders, so a stand-in costs only the transform writes that pose it
/// and, while the replay camera films it, its skinning and draw calls.
/// </summary>
public static class ReplayStandIns
{
    /// Copies `source` and everything under it, except the subtrees below it that are in `skip`
    /// (another actor: the ball in a hand), under `parent`, recording live -> copy in `map`. The
    /// copy of `source` itself takes its world pose and scale, for a parent at the origin.
    public static Transform CopyTree(Transform source, Transform parent, HashSet<Transform> skip,
                                     Dictionary<Transform, Transform> map)
    {
        Transform copy = CopyBranch(source, parent, skip, map);
        copy.SetPositionAndRotation(source.position, source.rotation);
        copy.localScale = source.lossyScale;
        return copy;
    }

    private static Transform CopyBranch(Transform source, Transform parent, HashSet<Transform> skip,
                                        Dictionary<Transform, Transform> map)
    {
        Transform copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
        copy.localScale = source.localScale;
        map[source] = copy;
        foreach (Transform child in source)
            if (!skip.Contains(child))
                CopyBranch(child, copy, skip, map);
        return copy;
    }

    /// Copies every live, drawing renderer under `source` onto its copy in `map`. `live` collects
    /// every renderer under it, copied or not (a trail, a line), for hiding from the replay
    /// camera; `copies` collects the copies.
    public static void CopyRenderers(Transform source, Dictionary<Transform, Transform> map,
                                     List<Renderer> live, List<Renderer> copies)
    {
        foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
        {
            if (!map.TryGetValue(renderer.transform, out Transform target))
                continue;   // inside another actor
            live.Add(renderer);
            if (!renderer.enabled)
                continue;
            Renderer copy = renderer is SkinnedMeshRenderer skin ? CopySkin(skin, target, map)
                          : renderer is MeshRenderer ? CopyMesh(renderer, target)
                          : null;
            if (copy != null)
                copies.Add(copy);
        }
    }

    private static Renderer CopySkin(SkinnedMeshRenderer source, Transform target, Dictionary<Transform, Transform> map)
    {
        if (source.sharedMesh == null)
            return null;
        var copy = target.gameObject.AddComponent<SkinnedMeshRenderer>();
        copy.sharedMesh = source.sharedMesh;
        Transform[] bones = source.bones;
        var mapped = new Transform[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            mapped[i] = bones[i] != null && map.TryGetValue(bones[i], out Transform bone) ? bone : null;
        copy.bones = mapped;
        copy.rootBone = source.rootBone != null && map.TryGetValue(source.rootBone, out Transform root) ? root : null;
        copy.localBounds = source.localBounds;
        copy.quality = source.quality;
        copy.skinnedMotionVectors = false;
        copy.updateWhenOffscreen = false;   // ReplayActors turns it on for the frames it is filmed
        for (int i = 0; i < source.sharedMesh.blendShapeCount; i++)
            copy.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
        CopyLook(source, copy);
        return copy;
    }

    private static Renderer CopyMesh(Renderer source, Transform target)
    {
        var filter = source.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return null;
        target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var copy = target.gameObject.AddComponent<MeshRenderer>();
        CopyLook(source, copy);
        return copy;
    }

    /// The same materials and probes; no shadows (the replay camera draws none) or motion vectors.
    /// Hidden until the replay camera's render shows it (ReplayActors.SetReplayView).
    private static void CopyLook(Renderer source, Renderer copy)
    {
        copy.sharedMaterials = source.sharedMaterials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = source.receiveShadows;
        copy.lightProbeUsage = source.lightProbeUsage;
        copy.reflectionProbeUsage = source.reflectionProbeUsage;
        copy.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        copy.allowOcclusionWhenDynamic = false;
        copy.renderingLayerMask = source.renderingLayerMask;
        if (source.HasPropertyBlock())
        {
            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            copy.SetPropertyBlock(block);
        }
        copy.forceRenderingOff = true;
    }
}
