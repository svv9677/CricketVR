/// <summary>
/// The club score hut's board, laid out once in metres and shared by the two things that must
/// agree on it: the builder, which puts the tins (the plates each figure hangs on) into the hut's
/// geometry, and ClubScoreboardFigures, which writes each figure onto its tin as TextMeshPro rich
/// text. Pure numbers, so EditMode tests check the fit without a scene.
///
/// The board reads from the crease, 73 m off: figures on tins in three bands under the home side's
/// name (a TMP of its own, ClubScoreboard's header).
///
///   [                 HOME SIDE NAME                  ]
///      TOTAL           WICKETS          OVERS
///    [1][2][3]         [ ][4]         [1][5].[2]       big tins: digits 0.62 m tall
///        BATTER                 BATTER
///    [ 3] [ 4][ 5]*        [ 5] [ 1][ 2]               batter's number on a light tin
///    LAST WKT          TARGET           NEED
///    [ ][6][7]         [1][5][0]        [ ][2][8]      digits 0.50 m tall
///
/// Board coordinates: x from the plate's left edge as the batter sees it, y down from its top,
/// metres. Font sizes are TMP's (world-space: size 10 is a 1 m em). Every tin is a cell `pitch`
/// wide; the figure's glyph is centred in its cell by a mspace tag of that pitch, placed with a pos
/// tag per digit, so nothing about the font's own spacing (kerning, bold) can walk a digit off its tin.
/// </summary>
public static class ClubScoreboardLayout
{
    /// The dark plate the board is painted on.
    public const float Width = 9f, Height = 4.4f;
    /// The header band (the home side's name), and its largest size (caps about 0.38 m).
    public const float HeaderTop = 0.1f, HeaderHeight = 0.55f, HeaderMargin = 0.2f, HeaderSize = 5.5f;
    /// The painted labels: TMP size, and each letter's cell (monospaced like signwriting, so a
    /// label's width is known and it can be centred over its tins).
    public const float LabelSize = 4f, LabelCell = 0.32f;
    /// LiberationSans SDF's cap height over its point size (capLine 59 / 86): digits stand this
    /// high per unit of em. The builder checks the font it uses against it.
    public const float CapRatio = 59f / 86f;
    /// Between a band's tins and the next band's label, and a label's baseline and its tins.
    const float BandGap = 0.1f, LabelToTins = 0.1f;

    public enum Tin { Dark, Light, None }

    /// One band of tins: the figures' TMP size, the cell pitch, each tin's size, and where it sits.
    public struct Row
    {
        public float digitSize, pitch, tinWidth, tinHeight;
        public float labelBaseline, tinTop, tinBottom, digitBaseline;
    }

    /// Every figure on the board, in Rows order.
    public enum Slot { Total, Wickets, Overs, Balls, BatANo, BatARuns, BatAStar, BatBNo, BatBRuns, BatBStar, LastWicket, Target, Need }

    public struct Field
    {
        public Slot slot;
        public int row, cells;
        /// The left edge of its first cell.
        public float left;
        public Tin tin;
    }

    public struct Label
    {
        public int row;
        public float centre;
        public string text;
    }

    public static readonly Row[] Rows = MakeRows();

    /// Column centres: the three groups of the top and bottom bands, the two batters.
    const float Left = 1.55f, Middle = 4.5f, Right = 7.45f, BatA = 2.25f, BatB = 6.75f;
    /// The batter's number, the gap before his runs, and the cell for the striker's star.
    const float BatterGap = 0.15f, StarCell = 0.4f, DotCell = 0.3f;

    public static readonly Field[] Fields = MakeFields();

    public static readonly Label[] Labels =
    {
        new Label { row = 0, centre = Left, text = "TOTAL" },
        new Label { row = 0, centre = Middle, text = "WICKETS" },
        new Label { row = 0, centre = Right, text = "OVERS" },
        new Label { row = 1, centre = BatterCentre(BatA), text = "BATTER" },
        new Label { row = 1, centre = BatterCentre(BatB), text = "BATTER" },
        new Label { row = 2, centre = Left, text = "LAST WKT" },
        new Label { row = 2, centre = Middle, text = "TARGET" },
        new Label { row = 2, centre = Right, text = "NEED" },
    };

    /// The overs' decimal point: its cell, between the overs and the ball of the over.
    public static float DotLeft => Find(Slot.Overs).left + 2 * Rows[0].pitch;
    public static float DotWidth => DotCell;

    public static Field Find(Slot s)
    {
        foreach (Field f in Fields)
            if (f.slot == s) return f;
        throw new System.ArgumentException("no field " + s);
    }

    /// A label's width on the board (its letters' cells).
    public static float LabelWidth(string text) => text.Length * LabelCell;

    /// The baselines of the figures text's six lines, top to bottom: each band's labels, then its digits.
    public static float[] Baselines()
    {
        var b = new float[Rows.Length * 2];
        for (int i = 0; i < Rows.Length; i++)
        {
            b[2 * i] = Rows[i].labelBaseline;
            b[2 * i + 1] = Rows[i].digitBaseline;
        }
        return b;
    }

    static Row[] MakeRows()
    {
        var rows = new Row[3];
        float top = HeaderTop + HeaderHeight;
        for (int i = 0; i < rows.Length; i++)
        {
            Row r = i == 0
                ? new Row { digitSize = 9f, pitch = 0.7f, tinWidth = 0.62f, tinHeight = 0.86f }
                : new Row { digitSize = 7.3f, pitch = 0.57f, tinWidth = 0.5f, tinHeight = 0.68f };
            r.labelBaseline = top + BandGap + LabelSize * 0.1f * CapRatio;
            r.tinTop = r.labelBaseline + LabelToTins;
            r.tinBottom = r.tinTop + r.tinHeight;
            // The digit's cap height centred on the tin.
            r.digitBaseline = r.tinBottom - (r.tinHeight - r.digitSize * 0.1f * CapRatio) * 0.5f;
            rows[i] = r;
            top = r.tinBottom;
        }
        return rows;
    }

    static Field[] MakeFields()
    {
        float pa = 0.7f, pb = 0.57f;
        float oversWidth = 2 * pa + DotCell + pa;
        return new[]
        {
            F(Slot.Total, 0, Left - 1.5f * pa, 3, Tin.Dark),
            F(Slot.Wickets, 0, Middle - pa, 2, Tin.Dark),
            F(Slot.Overs, 0, Right - oversWidth * 0.5f, 2, Tin.Dark),
            F(Slot.Balls, 0, Right - oversWidth * 0.5f + 2 * pa + DotCell, 1, Tin.Dark),
            F(Slot.BatANo, 1, BatterLeft(BatA), 2, Tin.Light),
            F(Slot.BatARuns, 1, BatterLeft(BatA) + 2 * pb + BatterGap, 3, Tin.Dark),
            F(Slot.BatAStar, 1, BatterLeft(BatA) + 5 * pb + BatterGap, 1, Tin.None),
            F(Slot.BatBNo, 1, BatterLeft(BatB), 2, Tin.Light),
            F(Slot.BatBRuns, 1, BatterLeft(BatB) + 2 * pb + BatterGap, 3, Tin.Dark),
            F(Slot.BatBStar, 1, BatterLeft(BatB) + 5 * pb + BatterGap, 1, Tin.None),
            F(Slot.LastWicket, 2, Left - 1.5f * pb, 3, Tin.Dark),
            F(Slot.Target, 2, Middle - 1.5f * pb, 3, Tin.Dark),
            F(Slot.Need, 2, Right - 1.5f * pb, 3, Tin.Dark),
        };
    }

    /// A batter's group: number, gap, runs, star, centred on `centre`.
    static float BatterGroupWidth => 5 * 0.57f + BatterGap + StarCell;
    static float BatterLeft(float centre) => centre - BatterGroupWidth * 0.5f;
    /// The label goes over the number and the runs, not the star.
    static float BatterCentre(float centre) => BatterLeft(centre) + (5 * 0.57f + BatterGap) * 0.5f;

    static Field F(Slot s, int row, float left, int cells, Tin tin) =>
        new Field { slot = s, row = row, left = left, cells = cells, tin = tin };

    /// A field's cell width: its band's pitch, or the star's own narrower cell.
    public static float CellWidth(Field f) => f.tin == Tin.None ? StarCell : Rows[f.row].pitch;
}
