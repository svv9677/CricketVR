using TMPro;
using UnityEngine;

/// <summary>
/// The dressing room's furniture and kit (DressingRoomBuilder): the benches down each side, shirts
/// on pegs over them, a shelf of helmets, bats leaning on the benches, kit bags on the floor, and
/// on the back wall the door and an honours board. The names on the board are made up.
/// </summary>
public static class DressingRoomProps
{
    private const float BenchX = DressingRoomBuilder.HalfWidth - 0.24f, BenchFrom = -2.0f, BenchTo = 2.5f;
    private const float PegY = 1.62f, ShelfY = 2.12f;
    private static readonly float[] PegZ = { -1.7f, -0.9f, -0.1f, 0.7f, 1.5f, 2.25f };

    private static GameObject Box(Transform p, string n, Vector3 at, Vector3 s, Material m, Quaternion? r = null) =>
        DressingRoomBuilder.Box(p, n, at, s, m, r);

    public static void Benches(Transform root, DressingRoomMaterials m)
    {
        float len = BenchTo - BenchFrom, cz = (BenchTo + BenchFrom) * 0.5f;
        foreach (float side in new[] { -1f, 1f })
        {
            var bench = new GameObject(side < 0 ? "Bench Left" : "Bench Right").transform;
            bench.SetParent(root, false);
            Box(bench, "Seat", new Vector3(side * BenchX, 0.44f, cz), new Vector3(0.44f, 0.05f, len), m.Bench);
            Box(bench, "Back Rail", new Vector3(side * (DressingRoomBuilder.HalfWidth - 0.03f), 0.7f, cz), new Vector3(0.04f, 0.14f, len), m.Bench);
            for (float z = BenchFrom + 0.15f; z <= BenchTo - 0.1f; z += 1.1f)
                Box(bench, "Leg", new Vector3(side * BenchX, 0.21f, z), new Vector3(0.36f, 0.42f, 0.05f), m.Trim);
            Box(bench, "Shelf", new Vector3(side * (DressingRoomBuilder.HalfWidth - 0.15f), ShelfY, cz), new Vector3(0.3f, 0.03f, len), m.Bench);
        }
    }

    public static void Kit(Transform root, DressingRoomMaterials m)
    {
        var kit = new GameObject("Kit").transform;
        kit.SetParent(root, false);
        foreach (float side in new[] { -1f, 1f })
        {
            float wallX = side * DressingRoomBuilder.HalfWidth;
            for (int i = 0; i < PegZ.Length; i++)
            {
                float z = PegZ[i];
                Box(kit, "Peg", new Vector3(wallX - side * 0.05f, PegY, z), new Vector3(0.1f, 0.025f, 0.025f), m.Peg);
                if ((i + (side > 0 ? 1 : 0)) % 3 != 2)
                    Shirt(kit, m, new Vector3(wallX - side * 0.05f, PegY - 0.36f, z));
                if (i % 2 == (side > 0 ? 0 : 1))
                    Helmet(kit, m, new Vector3(wallX - side * 0.17f, ShelfY + 0.13f, z + 0.15f), side);
            }
            Bat(kit, m, new Vector3(side * (BenchX - 0.3f), 0f, -1.35f), side, 14f);
            Bat(kit, m, new Vector3(side * (BenchX - 0.3f), 0f, 1.05f + side * 0.1f), side, 11f);
            Box(kit, "Kit Bag", new Vector3(side * (BenchX - 0.05f), 0.17f, 2.85f), new Vector3(0.36f, 0.34f, 0.85f),
                side < 0 ? m.BagNavy : m.BagMaroon);
            Box(kit, "Kit Bag", new Vector3(side * (BenchX - 0.55f), 0.16f, -2.25f), new Vector3(0.82f, 0.32f, 0.34f), m.BagGreen,
                Quaternion.Euler(0f, side * 12f, 0f));
        }
    }

    /// A cricket shirt on its peg: body, sleeves, and the coloured collar and cuffs.
    private static void Shirt(Transform p, DressingRoomMaterials m, Vector3 at)
    {
        var s = new GameObject("Shirt").transform;
        s.SetParent(p, false);
        s.localPosition = at;
        Box(s, "Body", Vector3.zero, new Vector3(0.05f, 0.62f, 0.46f), m.ShirtWhite);
        foreach (float z in new[] { -1f, 1f })
        {
            Box(s, "Sleeve", new Vector3(0f, 0.17f, z * 0.3f), new Vector3(0.045f, 0.3f, 0.14f), m.ShirtWhite, Quaternion.Euler(z * 28f, 0f, 0f));
            Box(s, "Cuff", new Vector3(0f, 0.04f, z * 0.37f), new Vector3(0.05f, 0.04f, 0.13f), m.ShirtTrim, Quaternion.Euler(z * 28f, 0f, 0f));
        }
        Box(s, "Collar", new Vector3(0f, 0.31f, 0f), new Vector3(0.06f, 0.05f, 0.2f), m.ShirtTrim);
    }

    /// A helmet on the shelf, its grille toward the room.
    private static void Helmet(Transform p, DressingRoomMaterials m, Vector3 at, float side)
    {
        var h = new GameObject("Helmet").transform;
        h.SetParent(p, false);
        h.localPosition = at;
        DressingRoomBuilder.Prim(PrimitiveType.Sphere, h, "Shell", Vector3.zero, new Vector3(0.24f, 0.22f, 0.25f), m.Helmet);
        Box(h, "Peak", new Vector3(-side * 0.11f, 0.02f, 0f), new Vector3(0.08f, 0.015f, 0.2f), m.Helmet);
        for (int i = 0; i < 3; i++)
            Box(h, "Grille", new Vector3(-side * 0.135f, -0.03f - i * 0.035f, 0f), new Vector3(0.012f, 0.01f, 0.2f), m.Grille);
    }

    /// A bat stood on its toe against the bench, leaning back `lean` degrees toward the wall.
    private static void Bat(Transform p, DressingRoomMaterials m, Vector3 toe, float side, float lean)
    {
        var b = new GameObject("Bat").transform;
        b.SetParent(p, false);
        b.localPosition = toe;
        b.localRotation = Quaternion.Euler(0f, 0f, -side * lean);
        Box(b, "Blade", new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.56f, 0.108f), m.BatWood);
        DressingRoomBuilder.Prim(PrimitiveType.Cylinder, b, "Handle", new Vector3(0f, 0.7f, 0f), new Vector3(0.034f, 0.14f, 0.034f), m.Grip);
    }

    public static void BackWall(Transform root, DressingRoomMaterials m)
    {
        var wall = new GameObject("Back Wall").transform;
        wall.SetParent(root, false);
        float z = DressingRoomBuilder.Back;
        Box(wall, "Door", new Vector3(2.1f, 1.05f, z + 0.03f), new Vector3(0.95f, 2.1f, 0.05f), m.Door);
        Box(wall, "Door Frame", new Vector3(2.1f, 2.13f, z + 0.04f), new Vector3(1.1f, 0.07f, 0.06f), m.Trim);
        DressingRoomBuilder.Prim(PrimitiveType.Sphere, wall, "Door Knob", new Vector3(1.75f, 1.0f, z + 0.08f), Vector3.one * 0.06f, m.Gold);
        Box(wall, "Honours Board", new Vector3(-0.7f, 1.85f, z + 0.03f), new Vector3(2.3f, 1.35f, 0.04f), m.Board);
        Box(wall, "Board Edge", new Vector3(-0.7f, 2.54f, z + 0.05f), new Vector3(2.4f, 0.05f, 0.05f), m.Gold);
        Box(wall, "Board Edge", new Vector3(-0.7f, 1.16f, z + 0.05f), new Vector3(2.4f, 0.05f, 0.05f), m.Gold);
        BoardText(wall, new Vector3(-0.7f, 1.85f, z + 0.06f));
    }

    private static readonly string[] Centuries =
    {
        "R. Iyengar  ......  134*", "T. Okafor  ......  121", "M. Hale  ......  117", "S. Kapadia  ......  108",
        "J. Whitlock  ......  104*", "A. Rahman  ......  101",
    };

    /// Gold lettering on the board, facing into the room.
    private static void BoardText(Transform p, Vector3 at)
    {
        var go = new GameObject("Honours Text");
        go.transform.SetParent(p, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = new Vector2(2.1f, 1.2f);
        t.text = "<b>CENTURIES</b>\n<size=60%>" + string.Join("\n", Centuries) + "</size>";
        t.fontSize = 1.6f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.9f, 0.75f, 0.35f);
        t.lineSpacing = -10f;
    }
}
