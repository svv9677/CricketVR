using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The ball-by-ball card of a partner's simulated balls (CareerMatchBallByBall, CareerMatchBallLines).
/// </summary>
public class CareerMatchBallByBallTests
{
    [Test]
    public void BallsAreNumberedOverDotBall()
    {
        Assert.AreEqual("0.1", CareerMatchLogic.BallNumber(1, true));
        Assert.AreEqual("0.6", CareerMatchLogic.BallNumber(6, true));
        Assert.AreEqual("1.1", CareerMatchLogic.BallNumber(7, true));
        Assert.AreEqual("6.3", CareerMatchLogic.BallNumber(39, true));
        Assert.AreEqual("6.4", CareerMatchLogic.BallNumber(39, false), "a wide carries the next ball's number");
        Assert.IsFalse(CareerMatchLogic.IsLegal("wd"));
        Assert.IsFalse(CareerMatchLogic.IsLegal("3wd"));
        Assert.IsTrue(CareerMatchLogic.IsLegal("2b"));
    }

    [Test]
    public void EachBallSaysWhatItWas()
    {
        Assert.AreEqual("dot", CareerMatchLogic.BallResult("*", null));
        Assert.AreEqual("1 run", CareerMatchLogic.BallResult("1", null));
        Assert.AreEqual("3 runs", CareerMatchLogic.BallResult("3", null));
        Assert.AreEqual("FOUR", CareerMatchLogic.BallResult("4", null));
        Assert.AreEqual("SIX", CareerMatchLogic.BallResult("6", null));
        Assert.AreEqual("OUT - c Khan b Lee", CareerMatchLogic.BallResult("W", "c Khan b Lee"));
        Assert.AreEqual("wide", CareerMatchLogic.BallResult("wd", null));
        Assert.AreEqual("3 wides", CareerMatchLogic.BallResult("3wd", null));
        Assert.AreEqual("2 byes", CareerMatchLogic.BallResult("2b", null));
        StringAssert.Contains("6.3", CareerMatchLogic.BallLine("6.3", "Ravi", "1", null));
        StringAssert.Contains("Ravi", CareerMatchLogic.BallLine("6.3", "Ravi", "1", null));
    }

    [Test]
    public void TheCardShowsTheNewestBallsOverTheScore()
    {
        var balls = new List<string>();
        for (int i = 1; i <= 9; i++)
            balls.Add("ball " + i);
        string body = CareerMatchLogic.BallByBallBody(balls, new[] { "Ravi 12 (9) not out" }, 45, 2, 39);
        StringAssert.DoesNotContain("ball 3\n", body);
        StringAssert.Contains("ball 4", body);
        StringAssert.Contains("ball 9", body);
        StringAssert.Contains("...", body, "more went before");
        StringAssert.Contains("Ravi 12 (9) not out", body);
        StringAssert.Contains("45/2 after 6.3 overs", body);
        Assert.IsTrue(body.IndexOf("ball 9") < body.IndexOf("45/2"), "the score last");
    }
}
