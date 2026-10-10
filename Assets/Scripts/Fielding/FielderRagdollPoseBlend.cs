using UnityEngine;

/// <summary>
/// Out of one animator controller into another without a pop. Swapping runtimeAnimatorController
/// (the bowler going from his bowling controller to the fielders' and back) leaves the skeleton in
/// its bind pose until the new controller has run: a T-pose for a frame, the arms out 1.36 m and
/// the feet 0.4 m from where they were (measured, every swap). BlendFromCurrentPose takes the pose
/// as it is, and LateUpdate blends from it into whatever the new controller plays over a moment -
/// in the bones' own (local) frames, so it does not matter that he is moving or turning meanwhile.
/// </summary>
public partial class FielderRagdoll
{
    private Quaternion[] swapPose;
    private Vector3 swapHips;
    private float swapFrom = -1f, swapTime;

    /// Hold the pose he has now, and blend from it into the animation over `seconds`.
    public void BlendFromCurrentPose(float seconds)
    {
        if (bones == null || hips == null)
            return;
        if (swapPose == null || swapPose.Length != bones.Length)
            swapPose = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            swapPose[i] = bones[i].localRotation;
        swapHips = hips.localPosition;
        swapFrom = Time.time;
        swapTime = Mathf.Max(0.01f, seconds);
    }

    private void ApplyPoseBlend()
    {
        if (swapFrom < 0f || state != State.Animated)
            return;
        float w = (Time.time - swapFrom) / swapTime;
        if (w >= 1f)
        {
            swapFrom = -1f;
            return;
        }
        float s = w * w * (3f - 2f * w);
        for (int i = 0; i < bones.Length; i++)
            bones[i].localRotation = Quaternion.Slerp(swapPose[i], bones[i].localRotation, s);
        hips.localPosition = Vector3.Lerp(swapHips, hips.localPosition, s);
    }
}
