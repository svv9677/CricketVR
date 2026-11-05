using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// The Nets scene as NetsSceneBuilder makes it: the match scene's OpenXR rig and nothing of the old
/// Oculus one, one camera for the headset, no crowd, no fielders, no keeper, and the practice nets
/// with their backstop.
public class NetsSceneTests
{
    private const string NetsPath = "Assets/Scenes/Nets.unity";
    private Scene scene;
    private bool opened;

    [SetUp]
    public void Open()
    {
        scene = SceneManager.GetSceneByPath(NetsPath);
        opened = !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(NetsPath, OpenSceneMode.Additive);
    }

    [TearDown]
    public void Close()
    {
        if (opened && scene.isLoaded)
            EditorSceneManager.CloseScene(scene, true);
    }

    private T[] All<T>() where T : Component =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    [Test]
    public void ItHasTheMatchScenesXRRig_AndNoneOfTheOculusOne()
    {
        Assert.AreEqual(1, All<XRRigSetup>().Length, "one XRRigSetup");
        Assert.IsEmpty(All<MonoBehaviour>().Where(m => m != null && (m.GetType().Name == "OVRPlayerController" ||
                                                                      m.GetType().Name == "OVRCameraRig" ||
                                                                      m.GetType().Name == "OVRManager")));
    }

    [Test]
    public void OnlyTheHeadsetAndTheReplayCameraRender()
    {
        // The eye anchors (centre and the two disabled per-eye cameras) and the big screen's camera.
        var cameras = All<Camera>().Where(c => c.enabled && c.gameObject.activeInHierarchy).Select(c => c.name).ToArray();
        CollectionAssert.IsSubsetOf(cameras, new[] { "CenterEyeAnchor", "LeftEyeAnchor", "RightEyeAnchor", "Replay Cam" },
                                    string.Join(", ", cameras));
        Assert.AreEqual(1, All<AudioListener>().Count(l => l.enabled && l.gameObject.activeInHierarchy));
    }

    [Test]
    public void NoCrowd_NoFielders_NoKeeper()
    {
        Assert.IsFalse(scene.GetRootGameObjects().Any(g => g.name == "Crowd"));
        Main main = All<Main>().Single();
        Assert.IsNotNull(main.theKeeper, "Main.theKeeper stays assigned (HUD moves it for each bowler)");
        Assert.IsFalse(main.theKeeper.activeInHierarchy, "the keeper is off");
        Transform fielders = main.transform.Find("Fielders");
        Assert.IsEmpty(fielders.Cast<Transform>().Where(f => f.gameObject.activeSelf).Select(f => f.name));
    }

    [Test]
    public void ThePracticeNets_AreUp_WithTheirBackstop()
    {
        GameObject nets = scene.GetRootGameObjects().Single(g => g.name == "Practice Nets");
        Assert.IsNotNull(nets.GetComponent<NetsBackstop>());
        var walls = nets.GetComponentsInChildren<BoxCollider>(true);
        Assert.AreEqual(3, walls.Length, "two sides and a back");
        foreach (BoxCollider w in walls)
        {
            Assert.IsFalse(w.isTrigger, w.name);
            Assert.IsNotNull(w.GetComponent<MeshRenderer>().sharedMaterial, w.name);
            Assert.IsNotNull(w.GetComponent<MeshFilter>().sharedMesh, w.name);
        }
        // The batter's stumps are inside the tunnel, with room to swing either side of them.
        Bounds b = new Bounds(walls[0].bounds.center, Vector3.zero);
        foreach (BoxCollider w in walls) b.Encapsulate(w.bounds);
        Assert.Less(b.min.x, BallDelivery.BatsmanStumpsX);
        Assert.Greater(b.max.x, BallDelivery.BatsmanStumpsX + NetsBackstop.MissedPastStumps);
        Assert.GreaterOrEqual(b.extents.z, 2.9f, "about 6 m wide");
    }

    [Test]
    public void TheNetsSoundIsThere() => Assert.IsTrue(GameSfx.Has("net"));
}
