using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// What the coach's goals say (phase 7): the hub button, each goal's value and bar, the result
/// screen's line, the Goals screen's empty states - all Latin-1 (the menu font) and inside
/// RatingCell's line (labels CoachGoalsText.LabelMax, values ValueMax, rich text aside).
/// </summary>
public class CoachGoalsTextTests
{
    private static void AssertLatin1(string s)
    {
        foreach (char c in s ?? "")
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
        StringAssert.DoesNotContain("—", s ?? "", "no em dash");
    }

    private static string Plain(string s) => Regex.Replace(s ?? "", "<[^>]*>", "");

    private static Season ThreeGoals(int done)
    {
        var s = new Season();
        s.goals.Add(new SeasonGoal { id = "runs", kind = eGoalKind.Runs, target = 60f, text = "Score 60 runs this season" });
        s.goals.Add(new SeasonGoal { id = "rating", kind = eGoalKind.Rating, target = 50f, text = "Get Judgement to 50" });
        s.goals.Add(new SeasonGoal { id = "drills", kind = eGoalKind.Drills, target = 3f, text = "Complete 3 drills" });
        for (int i = 0; i < done; i++)
            s.goals[i].done = true;
        return s;
    }

    [Test]
    public void TheHubButtonCountsTheGoals()
    {
        Assert.AreEqual("Goals", CoachGoalsText.HubButton(null));
        Assert.AreEqual("Goals", CoachGoalsText.HubButton(new Season()));
        Assert.AreEqual("Goals 1/3", CoachGoalsText.HubButton(ThreeGoals(1)));
        Assert.AreEqual("Goals 3/3", CoachGoalsText.HubButton(ThreeGoals(3)));
    }

    [Test]
    public void ValuesReadAsProgressOverTarget()
    {
        Assert.AreEqual("46 / 60", CoachGoalsText.Value(new SeasonGoal { kind = eGoalKind.Runs, progress = 46f, target = 60f }));
        Assert.AreEqual("SR 104 / 110", CoachGoalsText.Value(new SeasonGoal { kind = eGoalKind.StrikeRate, progress = 104.4f, target = 110f }));
        Assert.AreEqual("43 / 50", CoachGoalsText.Value(new SeasonGoal { kind = eGoalKind.Rating, progress = 43.2f, target = 50f }));
        Assert.AreEqual("-- / 50", CoachGoalsText.Value(new SeasonGoal { kind = eGoalKind.Rating, progress = RatingSet.Unknown, target = 50f }));
        Assert.AreEqual("Done", Plain(CoachGoalsText.Value(new SeasonGoal { kind = eGoalKind.Runs, done = true })));
        StringAssert.Contains(CoachGoalsText.Good, CoachGoalsText.Value(new SeasonGoal { done = true }));
        Assert.AreEqual("", CoachGoalsText.Value(null));
    }

    [Test]
    public void TheBarFillsTowardTheTarget()
    {
        Assert.AreEqual(0.5f, CoachGoalsText.Fraction(new SeasonGoal { kind = eGoalKind.Runs, progress = 30f, target = 60f }), 1e-4f);
        Assert.AreEqual(1f, CoachGoalsText.Fraction(new SeasonGoal { done = true }));
        Assert.AreEqual(0f, CoachGoalsText.Fraction(null));
        Assert.AreEqual(0f, CoachGoalsText.Fraction(new SeasonGoal { kind = eGoalKind.Runs, target = 0f }));
        // A rating fills from where it stood when the goal was set.
        Assert.AreEqual(0.5f, CoachGoalsText.Fraction(new SeasonGoal { kind = eGoalKind.Rating, baseline = 40f, progress = 45f, target = 50f }), 1e-4f);
        Assert.AreEqual(0f, CoachGoalsText.Fraction(new SeasonGoal { kind = eGoalKind.Rating, baseline = 40f, progress = 35f, target = 50f }));
        Assert.AreEqual(0f, CoachGoalsText.Fraction(new SeasonGoal { kind = eGoalKind.Rating, baseline = 40f, progress = RatingSet.Unknown, target = 50f }));
        // Over the target and not yet done (a strike rate over too few balls) is not a full bar.
        Assert.Less(CoachGoalsText.Fraction(new SeasonGoal { kind = eGoalKind.StrikeRate, progress = 150f, target = 110f }), 1f);
    }

    [Test]
    public void ReachedNamesOnlyTheGoalsNewlyDone()
    {
        Season s = ThreeGoals(2);
        Assert.AreEqual("", CoachGoalsText.Reached(s, null), "nothing remembered: nothing said");
        Assert.AreEqual("", CoachGoalsText.Reached(s, new List<string> { "runs", "rating" }));
        Assert.AreEqual("Coach's goal reached: Get Judgement to 50", CoachGoalsText.Reached(s, new List<string> { "runs" }));
        Assert.AreEqual("Coach's goals reached: 2 of 3", CoachGoalsText.Reached(s, new List<string>()));
        Assert.AreEqual("", CoachGoalsText.Reached(null, new List<string>()));
    }

    [Test]
    public void TheGoalsScreenSaysWhyThereAreNone()
    {
        Assert.AreEqual(CoachGoalsText.NoCareer, CoachGoalsText.Empty(null));
        var p = new CareerProfile { name = "Sam Carter", season = ThreeGoals(0) };
        Assert.AreEqual(CoachGoalsText.NoTrial, CoachGoalsText.Empty(p));
        p.trialDone = true;
        Assert.AreEqual("", CoachGoalsText.Empty(p));
        p.season.goals.Clear();
        Assert.AreEqual(CoachGoalsText.SeasonOver, CoachGoalsText.Empty(p));
        p.season.number = 2;
        p.season.rung = eRung.State;
        Assert.AreEqual("Season 2  ·  State cricket", CoachGoalsText.Subtitle(p));
        p.goalsDone = 4;
        StringAssert.StartsWith("Goals reached in your career: 4.", CoachGoalsText.CareerLine(p));
    }

    [Test]
    public void EveryGoalFitsItsLineInLatin1()
    {
        foreach (eRung rung in Enum.GetValues(typeof(eRung)))
            for (int weak = 0; weak < RatingSet.Count; weak++)
                for (int seed = 0; seed < 20; seed++)
                {
                    CareerProfile p = CareerTestPlay.Career(seed, rung, 70f);
                    p.ratings[weak] = 11f + weak;
                    p.AddInnings(new InningsRecord { season = 0, rung = rung, batted = true, runs = 9999 });
                    foreach (SeasonGoal g in CoachGoals.Make(p, p.season, new Random(seed)))
                        CheckGoal(g);
                }
    }

    private static void CheckGoal(SeasonGoal g)
    {
        AssertLatin1(g.text);
        Assert.LessOrEqual(g.text.Length, CoachGoalsText.LabelMax, g.text);
        // The most each can read: a strike rate tops out at 600 (a six every ball).
        float most = g.kind == eGoalKind.StrikeRate ? 600f : g.kind == eGoalKind.Rating ? 99f : 9999f;
        foreach (float progress in new[] { 0f, most, RatingSet.Unknown })
        {
            g.progress = progress;
            string v = CoachGoalsText.Value(g);
            AssertLatin1(v);
            Assert.LessOrEqual(Plain(v).Length, CoachGoalsText.ValueMax, v);
        }
        AssertLatin1(CoachGoals.ReachedBody(g));
    }

    [Test]
    public void TheOtherLinesAreLatin1()
    {
        AssertLatin1(CoachGoalsText.NoTrial);
        AssertLatin1(CoachGoalsText.SeasonOver);
        AssertLatin1(CoachGoalsText.CareerLine(new CareerProfile { goalsDone = 12 }));
        AssertLatin1(CoachGoalsText.Subtitle(new CareerProfile { season = new Season() }));
        AssertLatin1(CoachGoalsText.Reached(ThreeGoals(3), new List<string>()));
        AssertLatin1(CoachGoals.ReviewLine(ThreeGoals(1)));
        AssertLatin1(CoachGoalsText.HubButton(ThreeGoals(1)));
    }
}
