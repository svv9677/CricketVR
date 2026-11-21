using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts the career player's locker kit on in a career scene (phase 7): the bat's finish on the game
/// bat, the gloves on both hand models, and the replay helmet's shell, trim and head (through
/// ReplayBatterLook). On the Systems object of CricketVR and Nets; nothing happens outside a career
/// match or career nets (CosmeticLook.ForScene is null there).
///
/// Order matters, so the kit goes on in Start at execution order 900:
///   * after every sceneLoaded handler, so PartnerBatter.BuildBat (which copies the bat's materials
///     as the scene loads) always gives the partner the Classic bat, whichever handler runs first;
///   * before ShotRecorder.Start (order 1000), which builds the replay actors from the live bat, so
///     the replay bat wears the finish too. Keep 900 below ShotRecorder's order.
/// Its material copies are destroyed with the scene (OnDestroy). Looks only - nothing changes how he bats.
/// </summary>
[DefaultExecutionOrder(900)]
public class CareerCosmetics : MonoBehaviour
{
    public const string ObjectName = "Career Cosmetics";

    private CosmeticLook look;
    private readonly List<Material> made = new List<Material>();

    /// The look for this scene (CricketVR's and Nets' Systems > Career Cosmetics): the replay's
    /// dressing set now (before any Start), the kit itself put on in Start. Nothing outside a career
    /// match or career nets (CosmeticLook.ForScene is null there).
    private void Awake()
    {
        ReplayBatterLook.Clear();
        look = CosmeticLook.ForScene(gameObject.scene.name);
        if (look == null)
            return;
        ReplayBatterLook.Shell = look.side;
        ReplayBatterLook.Trim = look.TrimColour;
        ReplayBatterLook.Skin = look.skin;
    }

    private void Start()
    {
        if (look == null)
            return;
        Main main = FindFirstObjectByType<Main>();   // Main.Instance is set in its own Start: not yet
        MeshRenderer bat = main != null && main.theBat != null ? main.theBat.GetComponent<MeshRenderer>() : null;
        made.AddRange(look.ApplyBat(bat));
        var hands = new List<Renderer>();
        foreach (XRHandVisual hand in FindObjectsByType<XRHandVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hands.AddRange(hand.GetComponentsInChildren<Renderer>(true));
        made.AddRange(look.ApplyGloves(hands));
    }

    private void OnDestroy()
    {
        CosmeticLook.Destroy(made);
        ReplayBatterLook.Clear();
    }
}
