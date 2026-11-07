using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The main menu's "Calibrate" grip (2026-11-06: the grip was only in a match's settings). There is
/// no bat in the menu scene, so the request opens the free nets and, once Main and the rig are up,
/// starts the calibration there; Lock or Cancel leaves the player in the nets with the bat in hand.
/// </summary>
public class GripCalibrationOnArrival : MonoBehaviour
{
    /// Seconds after the nets load before it starts: the fade in and the rig placed at the crease.
    private const float Settle = 1.5f, GiveUpAfter = 10f;
    private static bool pending;

    public static void Request()
    {
        pending = true;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        SceneFader.Load(MainMenuScreen.NetsScene);
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pending || scene.name != MainMenuScreen.NetsScene)
            return;
        pending = false;
        SceneManager.sceneLoaded -= OnLoaded;
        var go = new GameObject("Grip Calibration On Arrival", typeof(GripCalibrationOnArrival));
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(Settle);
        float until = Time.time + GiveUpAfter;
        while (Time.time < until && (Main.Instance == null || Main.Instance.gameState != eGameState.InGame_Ready))
            yield return null;
        if (Main.Instance != null)
            Main.Instance.onCalibrateGrip();
        Destroy(gameObject);
    }
}
