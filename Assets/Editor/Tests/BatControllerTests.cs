using NUnit.Framework;
using UnityEngine;

/// Which controller holds the bat (2026-11-07): the stance's until one is chosen, then the chosen
/// one whatever the stance. And a Test's ball and trail are made to be seen.
public class BatControllerTests
{
    private const string Key = "bat_controller";
    private bool had;
    private int saved;

    [SetUp]
    public void Keep()
    {
        had = PlayerPrefs.HasKey(Key);
        saved = PlayerPrefs.GetInt(Key, GameSettings.FollowStance);
        PlayerPrefs.DeleteKey(Key);
        GameSettings.ReloadForTests();
    }

    [TearDown]
    public void Restore()
    {
        if (had) PlayerPrefs.SetInt(Key, saved); else PlayerPrefs.DeleteKey(Key);
        GameSettings.ReloadForTests();
    }

    [Test]
    public void NeverChosenTheBatFollowsTheStance()
    {
        Assert.AreEqual(GameSettings.FollowStance, GameSettings.BatController);
        Assert.IsTrue(GameSettings.BatInLeftHand(eBattingStyle.LeftHanded));
        Assert.IsFalse(GameSettings.BatInLeftHand(eBattingStyle.RightHanded));
    }

    [Test]
    public void AChosenControllerHoldsTheBatWhateverTheStance()
    {
        GameSettings.BatController = GameSettings.LeftController;
        Assert.IsTrue(GameSettings.BatInLeftHand(eBattingStyle.RightHanded), "a right-hander batting with the left controller");
        Assert.IsTrue(GameSettings.BatInLeftHand(eBattingStyle.LeftHanded));
        GameSettings.BatController = GameSettings.RightController;
        Assert.IsFalse(GameSettings.BatInLeftHand(eBattingStyle.LeftHanded), "a left-hander batting with the right controller");
        GameSettings.ReloadForTests();
        Assert.AreEqual(GameSettings.RightController, GameSettings.BatController, "kept between sessions");
    }

    [Test]
    public void ATestBallAndTrailAreMadeToBeSeen()
    {
        Assert.Greater(TestLook.RedLeather.r, 0.7f, "a bright red, not a dark leather");
        Assert.Greater(TestLook.BallGlow, 0f, "lit from within");
        Assert.Greater(TestLook.Trail.r, 0.9f, "a bright trail");
        Assert.GreaterOrEqual(TestLook.Trail.a, 0.85f, "hardly see-through at the ball");
        Assert.Greater(TestLook.UmpireShirt.r, 0.9f, "the umpire in a white shirt");
    }
}
