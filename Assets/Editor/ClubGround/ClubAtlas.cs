using System.Collections.Generic;
using UnityEngine;

/// The sixteen cells of club_atlas.png, row-major from the top left (Tools/clubground/build_textures.py
/// paints them in this order; the numbers are the cell index).
public enum ClubCell
{
    Weatherboard = 0, Brick = 1, RoofTile = 2, Window = 3,
    VerandaBoards = 4, GreenPaint = 5, BenchSlats = 6, ScorePlate = 7,
    RollerIron = 8, WhitePaint = 9, FoliageA = 10, FoliageB = 11,
    FoliageC = 12, FoliageD = 13, Bark = 14, Deckchair = 15,
}

/// How a cell is laid on a surface: repeated every Tile metres, stretched once over the whole
/// face (one window, one plate, one cluster of leaves), or sampled at its middle (flat paint).
public enum ClubCellMode { Tiled, Stretch, Solid }

/// <summary>
/// UV rectangles of the club atlas, and of the stadium crowd atlas the spectators reuse.
///
/// club_atlas.png is 2048 px square, a 4 x 4 grid of 512 px cells with an 8 px gutter inside each
/// one (the painter extends the edge colours into it), so a cell's usable rectangle is 496 px and
/// mip levels down to 16 px never sample a neighbour.
/// </summary>
public static class ClubAtlas
{
    public const int Size = 2048, Cell = 512, Gutter = 8, Columns = 4;

    /// Usable UV rectangle of a cell (Unity UVs: v = 0 at the bottom of the image).
    public static Rect UV(ClubCell c)
    {
        int i = (int)c, col = i % Columns, row = i / Columns;
        float u0 = (col * Cell + Gutter) / (float)Size, u1 = ((col + 1) * Cell - Gutter) / (float)Size;
        float vTop = 1f - (row * Cell + Gutter) / (float)Size;
        float vBottom = 1f - ((row + 1) * Cell - Gutter) / (float)Size;
        return UnityEngine.Rect.MinMaxRect(u0, vBottom, u1, vTop);
    }

    public static ClubCellMode ModeOf(ClubCell c)
    {
        switch (c)
        {
            case ClubCell.Weatherboard: case ClubCell.Brick: case ClubCell.RoofTile:
            case ClubCell.VerandaBoards: case ClubCell.BenchSlats: case ClubCell.Bark:
                return ClubCellMode.Tiled;
            case ClubCell.GreenPaint: case ClubCell.RollerIron: case ClubCell.WhitePaint:
                return ClubCellMode.Solid;
            default:
                return ClubCellMode.Stretch;   // Window, ScorePlate, foliage, Deckchair
        }
    }

    /// Metres one cell covers when tiled (x across, y up the surface), square: the scale
    /// build_textures.py paints each cell at (its CELL_METRES), so boards, courses and tiles come
    /// out their real size.
    public static Vector2 Tile(ClubCell c)
    {
        float m;
        switch (c)
        {
            case ClubCell.Weatherboard: m = 1.95f; break;
            case ClubCell.Brick: case ClubCell.RoofTile: case ClubCell.VerandaBoards: m = 1.2f; break;
            case ClubCell.BenchSlats: m = 0.5f; break;
            case ClubCell.Bark: m = 1f; break;
            default: m = 2f; break;
        }
        return new Vector2(m, m);
    }

    public static bool IsFoliage(ClubCell c) => c >= ClubCell.FoliageA && c <= ClubCell.FoliageD;

    // ---- The stadium crowd atlas (CrowdProcedural.png), reused for the club's spectators --------

    /// CrowdStandBuilder's layout: 50 people across, 14 rows, seated figures on a flat brown.
    public const int PeopleColumns = 50, PeopleRows = 14;
    /// The brown the CrowdStand shader keys out (atlas RGB 54/47/43, its _KeyColor in sRGB).
    public static readonly Color32 PeopleKey = new Color32(54, 47, 43, 255);

    /// UV of one person (row 0 = the bottom row of the image), pulled in a pixel each side so the
    /// neighbouring figure never bleeds into the quad.
    public static Rect Person(Vector2Int cell, int width = 2048, int height = 1024)
    {
        float du = 1f / width, dv = 1f / height;
        float u0 = cell.x / (float)PeopleColumns + du, u1 = (cell.x + 1) / (float)PeopleColumns - du;
        float v0 = cell.y / (float)PeopleRows + dv, v1 = (cell.y + 1) / (float)PeopleRows - dv;
        return UnityEngine.Rect.MinMaxRect(u0, v0, u1, v1);
    }

    /// The crowd atlas has empty seats (all background). A spectator quad on one of those would
    /// be an invisible person on a bench, so keep only cells where at least `minFill` of the pixels
    /// are not the key brown. `px` is bottom-up (Texture2D.GetPixels32 order).
    public static List<Vector2Int> OccupiedPeople(Color32[] px, int width, int height, float minFill = 0.3f)
    {
        var keep = new List<Vector2Int>();
        for (int row = 0; row < PeopleRows; row++)
            for (int col = 0; col < PeopleColumns; col++)
            {
                int x0 = col * width / PeopleColumns, x1 = (col + 1) * width / PeopleColumns;
                int y0 = row * height / PeopleRows, y1 = (row + 1) * height / PeopleRows;
                int on = 0, all = 0;
                for (int y = y0; y < y1; y += 2)
                    for (int x = x0; x < x1; x += 2)
                    {
                        Color32 c = px[y * width + x];
                        int d = Mathf.Abs(c.r - PeopleKey.r) + Mathf.Abs(c.g - PeopleKey.g) + Mathf.Abs(c.b - PeopleKey.b);
                        all++;
                        if (d > 30) on++;
                    }
                if (all > 0 && on >= minFill * all) keep.Add(new Vector2Int(col, row));
            }
        return keep;
    }

    /// Every cell, for when the crowd atlas cannot be read (then some seats may look empty).
    public static List<Vector2Int> AllPeople()
    {
        var all = new List<Vector2Int>();
        for (int row = 0; row < PeopleRows; row++)
            for (int col = 0; col < PeopleColumns; col++)
                all.Add(new Vector2Int(col, row));
        return all;
    }
}
