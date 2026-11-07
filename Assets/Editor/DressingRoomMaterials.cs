using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The dressing room's materials (DressingRoomBuilder), made once under Assets/DressingRoom and
/// reused: URP Simple Lit (cheap on the Quest), two generated textures - floorboards and the
/// panelling's vertical boards - and flat colours for the rest. All own work.
/// </summary>
public sealed class DressingRoomMaterials
{
    private const string Dir = "Assets/DressingRoom";
    private const string Shader = "Universal Render Pipeline/Simple Lit";

    public Material Floor, Rug, Ceiling, Wall, Panel, Trim, Frame, LightPanel, Bench, Board, Gold,
                    ShirtWhite, ShirtTrim, Helmet, Grille, BatWood, Grip, BagNavy, BagMaroon, BagGreen, Peg, Door;

    public static DressingRoomMaterials Load()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets", "DressingRoom");
        Texture2D boards = BoardsTexture("Floorboards", new Color(0.55f, 0.36f, 0.2f), 8, true);
        Texture2D panel = BoardsTexture("Panelling", new Color(0.36f, 0.22f, 0.13f), 10, false);
        var m = new DressingRoomMaterials
        {
            Floor = Mat("Floor", Color.white, boards, new Vector2(4f, 4f)),
            Rug = Mat("Rug", new Color(0.13f, 0.27f, 0.18f)),
            Ceiling = Mat("Ceiling", new Color(0.93f, 0.92f, 0.88f)),
            Wall = Mat("Wall", new Color(0.86f, 0.82f, 0.72f)),
            Panel = Mat("Panelling", Color.white, panel, new Vector2(6f, 1f)),
            Trim = Mat("Trim", new Color(0.3f, 0.18f, 0.1f)),
            Frame = Mat("Window Frame", new Color(0.95f, 0.95f, 0.93f)),
            LightPanel = Emissive("Light Panel", new Color(1f, 0.97f, 0.9f)),
            Bench = Mat("Bench", new Color(0.62f, 0.42f, 0.24f)),
            Board = Mat("Honours Board", new Color(0.2f, 0.11f, 0.06f)),
            Gold = Mat("Gold", new Color(0.83f, 0.66f, 0.27f)),
            ShirtWhite = Mat("Shirt", new Color(0.95f, 0.95f, 0.92f)),
            ShirtTrim = Mat("Shirt Trim", new Color(0.12f, 0.2f, 0.45f)),
            Helmet = Mat("Helmet", new Color(0.1f, 0.15f, 0.35f)),
            Grille = Mat("Grille", new Color(0.7f, 0.7f, 0.72f)),
            BatWood = Mat("Bat Willow", new Color(0.9f, 0.8f, 0.6f)),
            Grip = Mat("Bat Grip", new Color(0.75f, 0.1f, 0.12f)),
            BagNavy = Mat("Bag Navy", new Color(0.1f, 0.13f, 0.28f)),
            BagMaroon = Mat("Bag Maroon", new Color(0.45f, 0.1f, 0.12f)),
            BagGreen = Mat("Bag Green", new Color(0.12f, 0.35f, 0.2f)),
            Peg = Mat("Peg", new Color(0.75f, 0.72f, 0.66f)),
            Door = Mat("Door", new Color(0.42f, 0.27f, 0.15f)),
        };
        AssetDatabase.SaveAssets();
        return m;
    }

    private static Material Mat(string name, Color c, Texture2D tex = null, Vector2? tiling = null)
    {
        string path = $"{Dir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(UnityEngine.Shader.Find(Shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", c);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        mat.SetFloat("_Smoothness", 0.2f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material Emissive(string name, Color c)
    {
        Material mat = Mat(name, c);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 0.8f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return mat;
    }

    /// Boards across the texture (`count` of them), each a slightly different shade with fine
    /// grain and a dark joint; floorboards are staggered with end joints.
    private static Texture2D BoardsTexture(string name, Color baseColour, int count, bool endJoints)
    {
        string path = $"{Dir}/{name}.png";
        if (File.Exists(path))
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        const int Size = 512;
        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
        var rng = new System.Random(name.GetHashCode());
        int boardW = Size / count;
        var shades = new float[count * 2];
        for (int i = 0; i < shades.Length; i++)
            shades[i] = 0.85f + (float)rng.NextDouble() * 0.3f;
        for (int x = 0; x < Size; x++)
        {
            int board = Mathf.Min(x / boardW, count - 1);
            int offset = endJoints ? (board * 197) % Size : 0;
            for (int y = 0; y < Size; y++)
            {
                int yy = (y + offset) % Size;
                int half = endJoints && yy >= Size / 2 ? 1 : 0;
                float shade = shades[board * 2 + half];
                float grain = 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.9f + board * 13f, y * 0.04f);
                bool joint = x % boardW < 2 || (endJoints && (yy == 0 || yy == Size / 2));
                Color c = baseColour * shade * grain * (joint ? 0.45f : 1f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
