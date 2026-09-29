using NUnit.Framework;
using UnityEngine;

/// <summary>
/// GameSettings: the PlayerPrefs-backed settings model the menus edit and Main applies. No
/// scene, no Main. The defaults asserted here are the ones Main used before this class existed,
/// so a fresh install is unchanged.
/// Run: Window > General > Test Runner > EditMode > GameSettingsTests.
/// </summary>
public class GameSettingsTests
{
    [SetUp]
    public void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
        GameSettings.ReloadForTests();
    }

    [TearDown]
    public void ForgetListeners()
    {
        // Changed is static: a listener added by one test would otherwise still be counting
        // during the next one.
        GameSettings.ReloadForTests();
    }

    // ---- Defaults, matching Main's pre-existing behaviour ---------------------------------------

    [Test]
    public void Overs_DefaultsToFive()
    {
        Assert.AreEqual(5, GameSettings.Overs, "a quick match is five overs unless changed");
    }

    [Test]
    public void BatPower_DefaultsToRealistic()
    {
        Assert.AreEqual(Constants.BatPowerRealistic, GameSettings.BatPower, 1e-4f);
    }

    [Test]
    public void Difficulty_DefaultsToEasy()
    {
        Assert.AreEqual(eDifficulty.Easy, GameSettings.Difficulty,
                        "Main used GetInt(PP_Difficulty, 1), which is Easy");
    }

    [Test]
    public void BattingStyle_DefaultsToRightHanded()
    {
        Assert.AreEqual(eBattingStyle.RightHanded, GameSettings.BattingStyle);
    }

    [Test]
    public void FielderSpeed_DefaultsToOnePointFive()
    {
        Assert.AreEqual(1.5f, GameSettings.FielderSpeed, 1e-4f,
                        "Main's _fielderSpeed seed was 1.5f");
    }

    // ---- Persistence ------------------------------------------------------------------------------

    [Test]
    public void Overs_RoundTripsThroughPlayerPrefs()
    {
        GameSettings.Overs = 10;
        GameSettings.ReloadForTests();          // as if the next scene had just loaded
        Assert.AreEqual(10, GameSettings.Overs);
    }

    [Test]
    public void Difficulty_RoundTripsThroughPlayerPrefs()
    {
        GameSettings.Difficulty = eDifficulty.Hard;
        GameSettings.ReloadForTests();
        Assert.AreEqual(eDifficulty.Hard, GameSettings.Difficulty);
    }

    [Test]
    public void Overs_ClampsToSupportedRange()
    {
        GameSettings.Overs = 0;
        Assert.AreEqual(GameSettings.MinOvers, GameSettings.Overs, "0 overs is not a match");
        GameSettings.Overs = 999;
        Assert.AreEqual(GameSettings.MaxOvers, GameSettings.Overs);
    }

    // ---- Changed ----------------------------------------------------------------------------------

    [Test]
    public void Changed_FiresOnceWhenAValueReallyChanges()
    {
        int fired = 0;
        System.Action count = () => fired++;
        GameSettings.Changed += count;
        GameSettings.Overs = 8;
        GameSettings.Changed -= count;
        Assert.AreEqual(1, fired);
    }

    [Test]
    public void Changed_DoesNotFireWhenSetToTheSameValue()
    {
        GameSettings.Overs = 8;
        int fired = 0;
        System.Action count = () => fired++;
        GameSettings.Changed += count;
        GameSettings.Overs = 8;
        GameSettings.Changed -= count;
        Assert.AreEqual(0, fired,
                        "a no-op set must not make Main re-apply everything mid-over");
    }

    [Test]
    public void Changed_DoesNotFireWhenAClampedSetLandsOnTheCurrentValue()
    {
        GameSettings.Overs = GameSettings.MaxOvers;
        int fired = 0;
        System.Action count = () => fired++;
        GameSettings.Changed += count;
        GameSettings.Overs = 999;               // clamps back to MaxOvers
        GameSettings.Changed -= count;
        Assert.AreEqual(0, fired, "holding the > stepper at the limit must not churn");
    }
}
