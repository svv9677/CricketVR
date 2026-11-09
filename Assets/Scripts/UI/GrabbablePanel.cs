using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Move a menu panel by hand: point the laser at it and hold grip, and it moves with the
/// controller as if held; let go and it stays there, upright. Where it was left is saved
/// (PlayerPrefs), and the panel opens there from then on - Restore, called by its Show.
///
/// The pose is saved as if the player were at the striker's crease (EndPose.ToCrease) and carried
/// to wherever he stands when it is restored (FromCrease): a panel left beside him at the crease
/// opens beside him at the non-striker's end too (PlayerEnd). At the crease both are the identity,
/// so poses saved before the ends existed still read the same.
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

    /// Splash's panels keep their own poses: a menu moved by the crease in a match must not reopen
    /// there in Splash, ten metres from the player (it read as an empty menu screen).
    private string Key => "panel_pose_" + (gameObject.scene.name == NetsSession.SplashScene ? "splash_" : "") + saveKey;

    /// The main menu's panels stay where the dressing room puts them (2026-11-06: "in the main
    /// menu scene, can you make it so i cant drag the screens"): no grab there, and no pose from
    /// an old drag.
    private bool Fixed => gameObject.scene.name == NetsSession.SplashScene;

    /// Put the panel where the player last left it. False if it has never been moved.
    public bool Restore()
    {
        if (target == null || string.IsNullOrEmpty(saveKey) || Fixed)
            return false;
        if (!Decode(PlayerPrefs.GetString(Key, ""), PlayerEnd.NonStriker, out Vector3 p, out Quaternion q))
            return false;
        target.SetPositionAndRotation(p, q);
        return true;
    }

    private void Save()
    {
        PlayerPrefs.SetString(Key, Encode(target.position, target.rotation, PlayerEnd.NonStriker));
        PlayerPrefs.Save();
    }

    /// "x,y,z,qx,qy,qz,qw": a pose at the player's end, written as it would be at the crease.
    public static string Encode(Vector3 position, Quaternion rotation, bool nonStriker)
    {
        Vector3 p = EndPose.ToCrease(position, nonStriker);
        Quaternion q = EndPose.ToCrease(rotation, nonStriker);
        return string.Join(",", new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w }
            .Select(f => f.ToString("R", CultureInfo.InvariantCulture)));
    }

    /// The saved crease pose carried to the player's end. False for nothing saved (or not a pose).
    public static bool Decode(string saved, bool nonStriker, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        string[] v = (saved ?? "").Split(',');
        if (v.Length != 7)
            return false;
        var f = new float[7];
        for (int i = 0; i < 7; i++)
            if (!float.TryParse(v[i], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i]))
                return false;
        position = EndPose.FromCrease(new Vector3(f[0], f[1], f[2]), nonStriker);
        rotation = EndPose.FromCrease(new Quaternion(f[3], f[4], f[5], f[6]).normalized, nonStriker);
        return true;
    }

    private void LateUpdate()
    {
        OpenXRMenuInputModule menu = OpenXRMenuInputModule.Instance;
        if (target == null || menu == null || menu.Aim == null || !target.gameObject.activeInHierarchy || Fixed)
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
