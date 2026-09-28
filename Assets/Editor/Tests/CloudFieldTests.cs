using NUnit.Framework;
using UnityEngine;

/// <summary>
/// CloudField / CloudShading: the rules that decide where clouds sit and what colour they are.
/// Pure maths, no GameObjects, so the sky's behaviour is provable without entering play mode.
/// Run: Window > General > Test Runner > EditMode > CloudFieldTests.
/// </summary>
public class CloudFieldTests
{
    private static CloudFieldSettings Settings => CloudFieldSettings.Default;

    [Test]
    public void Generate_IsRepeatableForTheSameSeed()
    {
        var a = CloudField.Generate(Settings, 10, new System.Random(1234));
        var b = CloudField.Generate(Settings, 10, new System.Random(1234));

        Assert.AreEqual(a.Length, b.Length, "the same seed must produce the same number of clouds");
        for (int i = 0; i < a.Length; i++)
        {
            Assert.AreEqual(a[i].AzimuthDeg, b[i].AzimuthDeg, 1e-4f);
            Assert.AreEqual(a[i].ElevationDeg, b[i].ElevationDeg, 1e-4f);
            Assert.AreEqual(a[i].PlateIndex, b[i].PlateIndex);
        }
    }

    [Test]
    public void Generate_DiffersBetweenSeeds()
    {
        var a = CloudField.Generate(Settings, 10, new System.Random(1));
        var b = CloudField.Generate(Settings, 10, new System.Random(2));
        Assert.AreNotEqual(a[0].AzimuthDeg, b[0].AzimuthDeg,
                           "a fresh seed is what makes the sky different each run");
    }

    [Test]
    public void Generate_KeepsCloudsInsideTheCumulusBand()
    {
        var clouds = CloudField.Generate(Settings, 10, new System.Random(7));
        foreach (var c in clouds)
        {
            Assert.GreaterOrEqual(c.ElevationDeg, Settings.ElevationRange.x);
            Assert.LessOrEqual(c.ElevationDeg, Settings.ElevationRange.y,
                               "nothing at the zenith: a cloud overhead reads as a ceiling");
        }
    }

    [Test]
    public void Generate_OnlyPicksPlatesThatExist()
    {
        const int plateCount = 3;
        var clouds = CloudField.Generate(Settings, plateCount, new System.Random(99));
        foreach (var c in clouds)
        {
            Assert.GreaterOrEqual(c.PlateIndex, 0);
            Assert.Less(c.PlateIndex, plateCount);
        }
    }

    [Test]
    public void Generate_WithNoPlatesProducesNoClouds()
    {
        var clouds = CloudField.Generate(Settings, 0, new System.Random(1));
        Assert.AreEqual(0, clouds.Length, "no plates means an empty sky, not a crash");
    }

    [Test]
    public void Generate_SpreadsCloudsAroundTheCompass()
    {
        var clouds = CloudField.Generate(Settings, 10, new System.Random(42));
        // Stratified azimuth: every cloud sits in its own equal slice of the compass, so a sky
        // can never bunch every cloud into one quarter.
        float slice = 360f / clouds.Length;
        for (int i = 0; i < clouds.Length; i++)
        {
            Assert.GreaterOrEqual(clouds[i].AzimuthDeg, i * slice);
            Assert.LessOrEqual(clouds[i].AzimuthDeg, (i + 1) * slice);
        }
    }

    [Test]
    public void Generate_GivesEachCloudItsOwnDriftSpeed()
    {
        var clouds = CloudField.Generate(Settings, 10, new System.Random(5));
        float baseSpeed = Settings.DriftDegPerSecond;
        float tolerance = baseSpeed * Settings.DriftVariance + 1e-4f;

        bool anyDifference = false;
        foreach (var c in clouds)
        {
            Assert.AreEqual(baseSpeed, c.DriftDegPerSecond, tolerance,
                            "drift may vary, but not enough to look like wind shear");
            if (!Mathf.Approximately(c.DriftDegPerSecond, baseSpeed)) anyDifference = true;
        }
        Assert.IsTrue(anyDifference, "a sky that turns as one rigid shell reads as a rotating dome");
    }

    [Test]
    public void PointOnDome_PutsTheHorizonFlatAndTheZenithUp()
    {
        Vector3 horizon = CloudField.PointOnDome(0f, 0f, 100f);
        Assert.AreEqual(0f, horizon.y, 1e-3f);
        Assert.AreEqual(100f, horizon.magnitude, 1e-2f);

        Vector3 zenith = CloudField.PointOnDome(0f, 90f, 100f);
        Assert.AreEqual(100f, zenith.y, 1e-2f);
    }

    [Test]
    public void SunFacing_IsOneTowardTheSunAndZeroAwayFromIt()
    {
        // A sun travelling straight down lights whatever is directly above.
        Vector3 travel = Vector3.down;
        Assert.AreEqual(1f, CloudShading.SunFacing(Vector3.up, travel), 1e-3f);
        Assert.AreEqual(0f, CloudShading.SunFacing(Vector3.down, travel), 1e-3f);
        Assert.AreEqual(0.5f, CloudShading.SunFacing(Vector3.forward, travel), 1e-3f);
    }

    [Test]
    public void BodyTint_BrightensTowardTheSunAndShadesAwayFromIt()
    {
        Color lit = CloudShading.BodyTint(Color.white, 1f, 0.2f, 0.4f);
        Color shaded = CloudShading.BodyTint(Color.white, 0f, 0.2f, 0.4f);

        Assert.Greater(lit.r, shaded.r, "the sunward side of the sky must be the brighter one");
        Assert.AreEqual(1.2f, lit.r, 1e-3f);
        Assert.AreEqual(0.6f, shaded.r, 1e-3f);
        Assert.AreEqual(1f, lit.a, 1e-3f, "tinting must not change how solid a cloud is");
    }

    [Test]
    public void HazeAmount_WashesOutLowCloudsAndLeavesHighOnesClear()
    {
        Assert.AreEqual(1f, CloudShading.HazeAmount(5f, 10f, 40f), 1e-3f);
        Assert.AreEqual(1f, CloudShading.HazeAmount(10f, 10f, 40f), 1e-3f);
        Assert.AreEqual(0.5f, CloudShading.HazeAmount(25f, 10f, 40f), 1e-3f);
        Assert.AreEqual(0f, CloudShading.HazeAmount(40f, 10f, 40f), 1e-3f);
        Assert.AreEqual(0f, CloudShading.HazeAmount(80f, 10f, 40f), 1e-3f);
    }
}
