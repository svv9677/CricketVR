using UnityEditor;
using UnityEngine;

/// <summary>
/// Places the fielders' look on the bowler and the keeper in Main.prefab (called by
/// PlayerSetupBuilder). Each is drawn as the fielders' "David" (Resources/Models/David): a copy of
/// that model beside the body, named "&lt;body&gt; (fielder look)", as tall as the original, always
/// animated, with the original's own meshes switched off. FielderLook then poses the copy from the
/// original every frame. Until 2026-11-15 the game made the copy and hid the meshes in code as the
/// match scene loaded (FielderLook), so the editor showed the original bodies.
///
/// The copy is a sibling, not a child: a human pose's body position is in the space of the root's
/// parent, so the copy sits at its parent's origin and the pose lands where the original stands.
/// </summary>
public static class FielderLookBuilder
{
    private const string ModelPath = "Assets/Resources/Models/David.fbx";

    /// Places the look for the body `who` (the object with the Animator) as a child of `parent`.
    public static void Place(Transform who, Transform parent)
    {
        Animator animator = who.GetComponent<Animator>();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (animator == null || model == null)
        {
            Debug.LogError($"[FielderLookBuilder] No Animator on {who.name}, or no model at {ModelPath}.");
            return;
        }
        Transform copy = MakeCopy(model, who.name + " (fielder look)", parent);
        Animator lookAnimator = copy.GetComponent<Animator>();
        if (lookAnimator == null)
        {
            Debug.LogError($"[FielderLookBuilder] {ModelPath} has no Animator on its root.");
            return;
        }
        lookAnimator.runtimeAnimatorController = null;      // posed from the original, not animated
        lookAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Body positions are in units of each avatar's human scale: match them, as tall as the original.
        float lookScale = lookAnimator.humanScale, ownScale = animator.humanScale;
        if (lookScale <= 0.01f || ownScale <= 0.01f)
            Debug.LogError($"[FielderLookBuilder] Human scale read as {ownScale} / {lookScale} for {who.name}: the copy's size is not set.");
        else
            copy.localScale = Vector3.one * (ownScale * who.localScale.x / Mathf.Max(0.1f, lookScale));
        PlayerSetupBuilder.Touch(lookAnimator);
        // With its meshes hidden the original counts as unseen: his Animator (culled when unseen)
        // would stop posing him, and the copy with him.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PlayerSetupBuilder.Touch(animator);
        SkinnedMeshRenderer[] skins = who.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer skin in skins)
        {
            skin.enabled = false;
            PlayerSetupBuilder.Touch(skin);
        }
        Wire(who.gameObject, copy, skins);
    }

    /// The copy under `parent` (made if it is not there), at the parent's origin.
    private static Transform MakeCopy(GameObject model, string copyName, Transform parent)
    {
        Transform copy = parent.Find(copyName);
        if (copy == null)
        {
            copy = ((GameObject)PrefabUtility.InstantiatePrefab(model, parent)).transform;
            copy.name = copyName;
        }
        copy.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return copy;
    }

    private static void Wire(GameObject body, Transform copy, SkinnedMeshRenderer[] skins)
    {
        FielderLook look = PlayerSetupBuilder.Ensure<FielderLook>(body);
        var so = new SerializedObject(look);
        so.FindProperty("look").objectReferenceValue = copy;
        SerializedProperty list = so.FindProperty("bodySkins");
        list.arraySize = skins.Length;
        for (int i = 0; i < skins.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
