using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using Slot = ClubScoreboardLayout.Slot;

/// <summary>
/// ClubScoreboardLayout and the text ClubScoreboardFigures.Compose writes on it: every tin and label
/// on the plate and clear of its neighbours, the digits 0.5-0.7 m tall and centred on their tins,
/// big enough to read from the crease, and every figure the text places landing on the middle of
/// its own tin at its band's baseline. The last is checked by reading the tags back the way TMP
/// lays them out (pos sets the pen in 0.1 m units, mspace makes a cell the glyph is centred in,
/// line-height steps the baseline), so the text and the builder's tins cannot drift apart unseen.
/// Standalone.
/// </summary>
public class ClubScoreboardLayoutTests
{
    const float W = ClubScoreboardLayout.Width, H = ClubScoreboardLayout.Height;

    struct Glyph { public char ch; public float x, baseline, size; }

    /// Each visible character of `text`: its cell's centre across the board, its line's baseline
    /// (the first line on `firstBaseline`) and its TMP size.
    static List<Glyph> Lay(string text, float firstBaseline)
    {
        var glyphs = new List<Glyph>();
        float baseline = firstBaseline, lineHeight = 0f, x = 0f, mono = 0f, size = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<')
            {
                int end = text.IndexOf('>', i);
                string tag = text.Substring(i + 1, end - i - 1);
                i = end;
                int eq = tag.IndexOf('=');
                if (eq < 0 || tag.StartsWith("color")) continue;
                float v = float.Parse(tag.Substring(eq + 1), CultureInfo.InvariantCulture);
                switch (tag.Substring(0, eq))
                {
                    case "line-height": lineHeight = v * 0.1f; break;
                    case "pos": x = v * 0.1f; break;
                    case "mspace": mono = v * 0.1f; break;
                    case "size": size = v; break;
                }
                continue;
            }
            if (c == '\n') { baseline += lineHeight; x = 0f; continue; }
            Assert.Greater(mono, 0f, "every character is monospaced, so its place is known");
            if (c != ' ') glyphs.Add(new Glyph { ch = c, x = x + mono * 0.5f, baseline = baseline, size = size });
            x += mono;
        }
        return glyphs;
    }

    static IEnumerable<(float x0, float x1, float y0, float y1, ClubScoreboardLayout.Field f)> Tins()
    {
        foreach (ClubScoreboardLayout.Field f in ClubScoreboardLayout.Fields)
        {
            if (f.tin == ClubScoreboardLayout.Tin.None) continue;
            ClubScoreboardLayout.Row r = ClubScoreboardLayout.Rows[f.row];
            for (int i = 0; i < f.cells; i++)
            {
                float c = f.left + (i + 0.5f) * r.pitch;
                yield return (c - r.tinWidth * 0.5f, c + r.tinWidth * 0.5f, r.tinTop, r.tinBottom, f);
            }
        }
    }

    [Test]
    public void TinsAndLabelsSitOnThePlateClearOfEachOther()
    {
        var tins = new List<(float x0, float x1, float y0, float y1, ClubScoreboardLayout.Field f)>(Tins());
        float header = ClubScoreboardLayout.HeaderTop + ClubScoreboardLayout.HeaderHeight;
        foreach (var t in tins)
        {
            Assert.That(t.x0, Is.GreaterThanOrEqualTo(0.2f), t.f.slot.ToString());
            Assert.That(t.x1, Is.LessThanOrEqualTo(W - 0.2f), t.f.slot.ToString());
            Assert.That(t.y0, Is.GreaterThan(header), t.f.slot + " below the header");
            Assert.That(t.y1, Is.LessThanOrEqualTo(H - 0.05f), t.f.slot.ToString());
        }
        for (int a = 0; a < tins.Count; a++)
            for (int b = a + 1; b < tins.Count; b++)
            {
                bool apart = tins[a].x1 <= tins[b].x0 || tins[b].x1 <= tins[a].x0 || tins[a].y1 <= tins[b].y0 || tins[b].y1 <= tins[a].y0;
                Assert.IsTrue(apart, tins[a].f.slot + " and " + tins[b].f.slot + " overlap");
            }
        var labels = ClubScoreboardLayout.Labels;
        foreach (ClubScoreboardLayout.Label l in labels)
        {
            float half = ClubScoreboardLayout.LabelWidth(l.text) * 0.5f;
            Assert.That(l.centre - half, Is.GreaterThanOrEqualTo(0.1f), l.text);
            Assert.That(l.centre + half, Is.LessThanOrEqualTo(W - 0.1f), l.text);
            ClubScoreboardLayout.Row r = ClubScoreboardLayout.Rows[l.row];
            Assert.Less(r.labelBaseline, r.tinTop, l.text + " sits above its tins");
        }
        for (int a = 0; a < labels.Length; a++)
            for (int b = a + 1; b < labels.Length; b++)
                if (labels[a].row == labels[b].row)
                    Assert.Greater(Mathf.Abs(labels[a].centre - labels[b].centre),
                                   (ClubScoreboardLayout.LabelWidth(labels[a].text) + ClubScoreboardLayout.LabelWidth(labels[b].text)) * 0.5f + 0.2f,
                                   labels[a].text + " runs into " + labels[b].text);
        for (int i = 1; i < ClubScoreboardLayout.Rows.Length; i++)
            Assert.Greater(ClubScoreboardLayout.Rows[i].labelBaseline - ClubScoreboardLayout.LabelSize * 0.1f * ClubScoreboardLayout.CapRatio,
                           ClubScoreboardLayout.Rows[i - 1].tinBottom, "band " + i + "'s labels clear the band above");
    }

    [Test]
    public void DigitsAreHalfToTwoThirdsOfAMetreAndCentredOnTheirTins()
    {
        foreach (ClubScoreboardLayout.Row r in ClubScoreboardLayout.Rows)
        {
            float cap = r.digitSize * 0.1f * ClubScoreboardLayout.CapRatio;
            Assert.That(cap, Is.InRange(0.48f, 0.7f), "digit height");
            Assert.AreEqual((r.tinTop + r.tinBottom) * 0.5f, r.digitBaseline - cap * 0.5f, 1e-4f, "centred on the tin");
            Assert.Greater(r.tinHeight, cap + 0.1f, "a margin of tin round the digit");
            // A digit's glyph is about 0.48 em wide (LiberationSans): inside its tin with room.
            Assert.Greater(r.tinWidth, 0.48f * r.digitSize * 0.1f + 0.1f, "tin wide enough");
            Assert.Less(r.tinWidth, r.pitch, "a gap between tins");
        }
        Assert.That(ClubScoreboardLayout.Rows[0].digitSize * 0.1f * ClubScoreboardLayout.CapRatio, Is.InRange(0.58f, 0.7f),
                    "the total, wickets and overs are the big figures");
    }

    /// From the batter's eye to the score hut (73 m), the smallest digit subtends over 0.38 degrees,
    /// about 8 pixels on a Quest 2, twice the old board's 0.24 m figures.
    [Test]
    public void TheSmallestFigureReadsFromTheCrease()
    {
        float d = ClubGroundLayout.FromEye(ClubGroundLayout.XZ(ClubGroundLayout.ScoreHut.pos));
        Assert.That(d, Is.InRange(60f, 80f));
        foreach (ClubScoreboardLayout.Row r in ClubScoreboardLayout.Rows)
        {
            float deg = Mathf.Atan2(r.digitSize * 0.1f * ClubScoreboardLayout.CapRatio, d) * Mathf.Rad2Deg;
            Assert.Greater(deg, 0.38f);
        }
        Assert.Greater(ClubScoreboardLayout.Rows[1].digitSize * 0.1f * ClubScoreboardLayout.CapRatio, 2f * 0.24f);
    }

    [Test]
    public void EveryFigureLandsOnItsTinAtItsBandsBaseline()
    {
        BoardState s = ClubScoreboardFigures.From(123, 4, 15, 2, 150, 2, 60, 4, 7, 98);
        string text = ClubScoreboardFigures.Compose(s);
        Assert.AreEqual(6, text.Split('\n').Length, "a labels line and a figures line per band");
        float[] baselines = ClubScoreboardLayout.Baselines();
        List<Glyph> glyphs = Lay(text, baselines[0]);
        string[] figures = ClubScoreboardFigures.Figures(s);
        int found = 0;
        foreach (ClubScoreboardLayout.Field f in ClubScoreboardLayout.Fields)
        {
            string fig = figures[(int)f.slot];
            ClubScoreboardLayout.Row r = ClubScoreboardLayout.Rows[f.row];
            float cell = ClubScoreboardLayout.CellWidth(f);
            for (int i = 0; i < fig.Length; i++)
            {
                float want = f.left + (f.cells - fig.Length + i + 0.5f) * cell;
                Glyph g = glyphs.Find(q => Mathf.Abs(q.x - want) < 1e-3f && Mathf.Abs(q.baseline - r.digitBaseline) < 1e-3f);
                Assert.AreEqual(fig[i], g.ch, f.slot + " digit " + i + " at " + want);
                Assert.AreEqual(r.digitSize, g.size, 1e-4f, f.slot + " size");
                found++;
            }
        }
        Assert.AreEqual("123" + "4" + "15" + "2" + "3" + "60" + "*" + "5" + "7" + "98" + "150" + "27",
                        string.Concat(figures), "what the board shows for this state");
        Glyph dot = glyphs.Find(q => q.ch == '.');
        Assert.AreEqual(ClubScoreboardLayout.DotLeft + ClubScoreboardLayout.DotWidth * 0.5f, dot.x, 1e-3f, "the overs' point");
        foreach (ClubScoreboardLayout.Label l in ClubScoreboardLayout.Labels)
        {
            float x0 = l.centre - ClubScoreboardLayout.LabelWidth(l.text) * 0.5f + ClubScoreboardLayout.LabelCell * 0.5f;
            Glyph g = glyphs.Find(q => Mathf.Abs(q.x - x0) < 1e-3f && Mathf.Abs(q.baseline - ClubScoreboardLayout.Rows[l.row].labelBaseline) < 1e-3f);
            Assert.AreEqual(l.text[0], g.ch, l.text);
            found += l.text.Replace(" ", "").Length;
        }
        Assert.AreEqual(found + 1, glyphs.Count, "nothing else is drawn but the point");
    }

    [Test]
    public void BlankTinsDrawNothingAndAnEndOfOverHasNoPoint()
    {
        List<Glyph> glyphs = Lay(ClubScoreboardFigures.Compose(ClubScoreboardFigures.From(40, 0, 6, 0, 0, -1, 0, -1, 0)),
                                 ClubScoreboardLayout.Baselines()[0]);
        Assert.IsFalse(glyphs.Exists(g => g.ch == '.'), "no point at the end of an over");
        Assert.IsFalse(glyphs.Exists(g => g.ch == '*'), "no striker");
        int digits = glyphs.FindAll(g => char.IsDigit(g.ch)).Count;
        Assert.AreEqual(2 + 1 + 1, digits, "40, 0 and 6: no batters, no last wicket, no target");
    }

    [Test]
    public void TheLastWicketIsTheTotalWhenTheWicketsWentUp()
    {
        Assert.AreEqual(0, ClubScoreboardFigures.LastWicket(0, 0, 0, 25), "none down yet");
        Assert.AreEqual(25, ClubScoreboardFigures.LastWicket(0, 0, 1, 25), "the first falls at 25");
        Assert.AreEqual(25, ClubScoreboardFigures.LastWicket(1, 25, 1, 60), "kept while no more fall");
        Assert.AreEqual(61, ClubScoreboardFigures.LastWicket(1, 25, 3, 61), "two in one poll: the total now");
        Assert.AreEqual(0, ClubScoreboardFigures.LastWicket(3, 61, 0, 0), "a new innings");
        BoardState down = ClubScoreboardFigures.From(60, 1, 10, 0, 0, 2, 10, 3, 20, 25);
        Assert.AreEqual("25", ClubScoreboardFigures.Figures(down)[(int)Slot.LastWicket]);
        Assert.AreEqual("", ClubScoreboardFigures.Figures(ClubScoreboardFigures.From(60, 0, 10, 0, 0, 2, 10, 3, 20, 0))[(int)Slot.LastWicket]);
    }

    [Test]
    public void FiguresTooLongForTheirTinsShowTheLargestThatFits()
    {
        string[] f = ClubScoreboardFigures.Figures(ClubScoreboardFigures.From(1234, 2, 120, 1, 0, 0, 1005, 1, 3));
        Assert.AreEqual("999", f[(int)Slot.Total]);
        Assert.AreEqual("99", f[(int)Slot.Overs]);
        Assert.AreEqual("999", f[(int)Slot.BatARuns]);
        Assert.AreEqual("0", ClubScoreboardFigures.Figures(ClubScoreboardFigures.From(160, 2, 18, 0, 150, 0, 1, 1, 3))[(int)Slot.Need],
                        "the target passed: nothing needed");
    }
}
