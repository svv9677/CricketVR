using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Move a menu panel by hand: point the laser at it and hold grip, and it moves with the
/// controller as if held; let go and it stays there, upright. Where it was left is saved
/// (PlayerPrefs), and the panel opens there from then on - Restore, called by its Show.
///
/// Runs after OpenXRMenuInputModule has posed the laser for the frame.
/// </summary>
[DefaultExecutionOrder(100)]
public class GrabbablePanel : MonoBehaviour
{
    [Tooltip("The panel that moves (the menu's canvas).")]
    [SerializeField] private Transform target;
    [Tooltip("PlayerPrefs key the placement is saved under.")]
    [SerializeField] private string saveKey;

    private bool grabbing;
    private Vector3 heldPosition;      // in the aim's frame
    private Quaternion heldRotation;

    private string Key => "panel_pose_" + saveKey;

    /// Put the panel where the player last left it. False if it has never been moved.
    public bool Restore()
    {
        if (target == null || string.IsNullOrEmpty(saveKey))
            return false;
        string saved = PlayerPrefs.GetString(Key, "");
        string[] v = saved.Split(',');
        if (v.Length != 7)
            return false;
        float F(int i) => float.Parse(v[i], CultureInfo.InvariantCulture);
        target.SetPositionAndRotation(new Vector3(F(0), F(1), F(2)), new Quaternion(F(3), F(4), F(5), F(6)).normalized);
        return true;
    }

    private void Save()
    {
        Vector3 p = target.position;
        Quaternion q = target.rotation;
        PlayerPrefs.SetString(Key, string.Join(",", new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w }
            .Select(f => f.ToString("R", CultureInfo.InvariantCulture))));
        PlayerPrefs.Save();
    }

    private void LateUpdate()
    {
        OpenXRMenuInputModule menu = OpenXRMenuInputModule.Instance;
        if (target == null || menu == null || menu.Aim == null || !target.gameObject.activeInHierarchy)
        {
            grabbing = false;
            return;
        }
        Transform aim = menu.Aim;
        if (!grabbing)
        {
            if (menu.GripPressedThisFrame && menu.PointingAt(target))
            {
                grabbing = true;
                heldPosition = aim.InverseTransformPoint(target.position);
                heldRotation = Quaternion.Inverse(aim.rotation) * target.rotation;
            }
            return;
        }
        if (menu.GripHeld)
        {
            target.SetPositionAndRotation(aim.TransformPoint(heldPosition), aim.rotation * heldRotation);
            return;
        }
        // Let go: it stays where it was put, keeping its tilt toward the player but never rolled.
        grabbing = false;
        target.rotation = Quaternion.LookRotation(target.forward, Vector3.up);
        Save();
    }
}
