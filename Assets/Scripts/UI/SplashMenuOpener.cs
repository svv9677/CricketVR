using UnityEngine;

/// <summary>
/// Opens the menu as soon as Splash loads. In the match scenes MenuRoot is opened with B; in
/// Splash the menu IS the scene, so something has to open it, and a one-line component keeps
/// that out of MenuRoot itself.
///
/// Start, not Awake: MenuRoot.Awake switches its screens off, and OpenXRMenuInputModule needs
/// its own Awake to have run before a panel can register with it.
/// </summary>
[RequireComponent(typeof(MenuRoot))]
public class SplashMenuOpener : MonoBehaviour
{
    private void Start() => GetComponent<MenuRoot>().Open();
}
