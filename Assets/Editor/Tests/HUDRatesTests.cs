using NUnit.Framework;

/// <summary>The rates bar under the floating scorecard (HUDRates.Line).</summary>
public class HUDRatesTests
{
    [Test]
    public void AChaseShowsTheRatesAndTheEquation()
    {
        string line = HUDRates.Line(54, 45, 20, 160);
        StringAssert.Contains("RRR</color> 8.48", line);
        StringAssert.Contains("CRR</color> 7.20", line);
        StringAssert.Contains("106 needed off 75 balls", line);
    }

    [Test]
    public void SettingATotalProjectsAtTheRateTwoQuickerAndTwoSlower()
    {
        string line = HUDRates.Line(54, 45, 20, 0);
        StringAssert.Contains("CRR</color> 7.20", line);
        StringAssert.Contains("at 7.2:</color> 144", line);
        StringAssert.Contains("at 9.2:</color> 169", line);
        StringAssert.Contains("at 5.2:</color> 119", line);
        StringAssert.Contains("after the first over", HUDRates.Line(4, 3, 20, 0));
    }

    [Test]
    public void NoBarInATest()
    {
        Assert.IsNull(HUDRates.Line(120, 200, MatchFormat.TestInningsOvers, 0));
        Assert.IsNull(HUDRates.Line(0, 0, 0, 0));
    }
}
