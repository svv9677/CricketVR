using NUnit.Framework;
using UnityEngine;

/// The bat's whoosh follows the swing (2026-11-08: "at different speeds, it should sound louder or
/// softer, like how it would actually sound"), and every line TeamTalk can say is there in every voice.
public class TeamTalkTests
{
    [Test]
    public void ASlowBatIsSilentAndAFullSwingIsFullVolume()
    {
        Assert.AreEqual(0f, BatWhoosh.Loudness(0f));
        Assert.AreEqual(0f, BatWhoosh.Loudness(BatWhoosh.SilentBelow));
        Assert.AreEqual(1f, BatWhoosh.Loudness(BatWhoosh.FullAt), 1e-4f);
        Assert.AreEqual(1f, BatWhoosh.Loudness(BatWhoosh.FullAt + 15f), 1e-4f, "never past full");
    }

    [Test]
    public void LoudnessRisesSteeplyWithSpeedNotInSteps()
    {
        float last = 0f;
        for (float s = 0f; s <= 35f; s += 0.5f)
        {
            float l = BatWhoosh.Loudness(s);
            Assert.GreaterOrEqual(l, last, $"louder at {s} m/s than below it");
            Assert.Less(l - last, 0.12f, $"no jump at {s} m/s");
            last = l;
        }
        // Twice the speed is 2^2.5 = 5.7 times the amplitude (about 15 dB), as air noise grows.
        Assert.AreEqual(Mathf.Pow(2f, BatWhoosh.Exponent), BatWhoosh.Loudness(24f) / BatWhoosh.Loudness(12f), 0.01f);
    }

    [Test]
    public void EveryLineIsThereInEveryVoice()
    {
        AudioClip[] all = Resources.LoadAll<AudioClip>("Sounds/Voices");
        string[] groups = { "goodball", "beaten", "console", "wicket", "catch", "stop", "chatter", "shot", "six" };
        foreach (string g in groups)
            for (int v = 0; v < TeamTalk.Voices; v++)
                Assert.IsTrue(System.Array.Exists(all, c => c.name.StartsWith($"{g}_v{v}_")), $"{g} in voice {v}");
        Assert.IsTrue(System.Array.Exists(Resources.LoadAll<AudioClip>("Sounds/Sfx"), c => c.name == "clap_1"), "a single clap");
    }

    [Test]
    public void ABowlersArmGoesByTheSidesFirstName()
    {
        // Why a career's roster must decide the arm again (HUDCareer.ApplyCareerRoster): the same
        // bowler in the same slot can bowl the other arm once the side's real names are in.
        bool flips = false;
        foreach (string first in new[] { "Bumrah", "Rana", "Kumar", "Patel", "Singh", "Khan" })
            flips |= BowlerArm.IsLeftArm(2, first) != BowlerArm.IsLeftArm(2, "Bumrah");
        Assert.IsTrue(flips);
    }
}
