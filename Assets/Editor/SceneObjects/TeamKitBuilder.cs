using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dresses a "David" body in a team kit for the Partner and Batting Side builders: pre-made shirt,
/// trouser and helmet-shell materials for every kit colour (saved as assets, which PartnerKit picks
/// from at runtime), and the helmet, pads and gloves as plain shapes on his bones. Until 2026-11-15
/// the game made all of it in code when the match scene loaded (PartnerKit, in PartnerBatter.cs).
///
/// Run on a body standing upright at the origin of a preview scene (or placed as it will stand),
/// before anything moves it: the pieces are built in world space and then parented to his bones.
/// </summary>
public static class TeamKitBuilder
{
    public const string Folder = SceneObjectAssets.GeneratedFolder + "/TeamKit";

    // The helmet: the shell's drop below the top of his head and its size; the peak; the grille;
    // the pads; the gloves (the values the game used).
    private const float ShellDrop = 0.075f;
    private static readonly Vector3 ShellSize = new Vector3(0.25f, 0.23f, 0.27f);
    private const float PeakForward = 0.135f, PeakTilt = 8f;
    private static readonly Vector3 PeakSize = new Vector3(0.21f, 0.012f, 0.09f);
    private const float GrilleTop = 0.13f, GrilleGap = 0.04f, GrilleForward = 0.13f, BarThickness = 0.012f, BarLength = 0.22f;
    private const float PadWidth = 0.15f, PadDepth = 0.12f, PadLength = 1.1f, PadForward = 0.04f;
    private const float GloveSize = 0.10f, GloveForward = 0.04f;
    private const float DefaultHeadTop = 0.17f, HeadSearchRadius = 0.2f, TrouserShade = 0.3f;

    private static readonly Color Metal = new Color(0.55f, 0.57f, 0.6f);
    private static readonly Color Whites = new Color(0.93f, 0.93f, 0.9f);
    private static readonly Color Shoes = new Color(0.9f, 0.9f, 0.9f);

    /// The colour of kit index `i`: Appearance.KitColours, then whites (a Test).
    public static Color KitColour(int i) => i < Appearance.KitColours.Length ? Appearance.KitColours[i] : Whites;

    /// Dresses `a` in the kit (shirt, trousers, shoes) and, when `helmeted`, helmet, pads and gloves.
    /// The returned PartnerKit has a material for every kit colour and shows colour 0.
    public static PartnerKit Dress(Animator a, bool helmeted)
    {
        var kit = new PartnerKit();
        Clothes(a, kit);
        if (helmeted)
            Padded(a, kit);
        kit.Apply(0);
        return kit;
    }

    private static void Clothes(Animator a, PartnerKit kit)
    {
        var shirts = new List<Renderer>();
        var trousers = new List<Renderer>();
        int count = Appearance.KitColours.Length + 1;
        kit.shirtMaterials = new Material[count];
        kit.trouserMaterials = new Material[count];
        foreach (SkinnedMeshRenderer skin in a.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string n = skin.name;
            if (n.Contains("Hoody"))
            {
                shirts.Add(skin);
                for (int i = 0; i < count; i++)
                    kit.shirtMaterials[i] = Flat(skin, "Shirt_" + i, KitColour(i));
            }
            else if (n.Contains("Pants"))
            {
                trousers.Add(skin);
                for (int i = 0; i < count; i++)
                    kit.trouserMaterials[i] = Flat(skin, "Trousers_" + i, Color.Lerp(KitColour(i), Color.black, TrouserShade));
            }
            else if (n.Contains("Sneakers"))
                skin.sharedMaterial = Flat(skin, "Shoes", Shoes);
        }
        kit.shirts = shirts.ToArray();
        kit.trousers = trousers.ToArray();
    }

    /// One flat colour (the atlas would show through) on a copy of the skin's own material.
    private static Material Flat(SkinnedMeshRenderer skin, string asset, Color colour)
    {
        var m = new Material(skin.sharedMaterial) { name = "Partner " + asset };
        m.mainTexture = null;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
        if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
        return SceneObjectAssets.SaveMaterial(m, $"{Folder}/{asset}.mat");
    }

    private static Material Plain(string asset, Color colour)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Partner " + asset };
        m.SetColor("_BaseColor", colour);
        return SceneObjectAssets.SaveMaterial(m, $"{Folder}/{asset}.mat");
    }

    private static void Padded(Animator a, PartnerKit kit)
    {
        int count = Appearance.KitColours.Length + 1;
        kit.shellMaterials = new Material[count];
        for (int i = 0; i < count; i++)
            kit.shellMaterials[i] = Plain("Shell_" + i, KitColour(i));
        Material metal = Plain("Grille", Metal), white = Plain("Pads", Whites);
        kit.shell = Helmet(a, kit.shellMaterials[0], metal);
        Pad(a, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, white);
        Pad(a, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, white);
        Glove(a, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, white);
        Glove(a, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, white);
    }

    /// Built as he stands (upright, facing his root's forward), then carried by his head. Returns the shell.
    private static Renderer Helmet(Animator a, Material shell, Material metal)
    {
        Transform head = a.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return null;
        Vector3 f = a.transform.forward, up = Vector3.up;
        Quaternion facing = Quaternion.LookRotation(f, up);
        float top = TopOfHead(a, head);
        var helmetObject = new GameObject(PartnerBatter.HelmetName);
        SceneManager.MoveGameObjectToScene(helmetObject, head.gameObject.scene);
        Transform helmet = helmetObject.transform;
        helmet.SetPositionAndRotation(head.position, facing);
        helmet.SetParent(head, true);
        Vector3 centre = head.position + up * (top - ShellDrop);
        Renderer shellRenderer = Part(PrimitiveType.Sphere, helmet, shell, "Shell", centre, facing, ShellSize);
        Part(PrimitiveType.Cube, helmet, shell, "Peak", centre + f * PeakForward,
             facing * Quaternion.Euler(PeakTilt, 0f, 0f), PeakSize);
        Quaternion across = facing * Quaternion.Euler(0f, 0f, 90f);     // a cylinder's length is its y
        var bar = new Vector3(BarThickness, BarLength * 0.5f, BarThickness);
        for (int i = 0; i < 3; i++)
            Part(PrimitiveType.Cylinder, helmet, metal, "Grille", head.position + up * (top - GrilleTop - i * GrilleGap) + f * GrilleForward, across, bar);
        return shellRenderer;
    }

    private static void Pad(Animator a, HumanBodyBones shin, HumanBodyBones foot, Material white)
    {
        Transform knee = a.GetBoneTransform(shin), ankle = a.GetBoneTransform(foot);
        if (knee == null || ankle == null)
            return;
        Vector3 along = ankle.position - knee.position;
        if (along.sqrMagnitude < 1e-4f)
            return;
        Quaternion turn = Quaternion.LookRotation(a.transform.forward, along);
        Vector3 at = (knee.position + ankle.position) * 0.5f + a.transform.forward * PadForward;
        // A capsule is 2 long and 1 across: half the length in y.
        Part(PrimitiveType.Capsule, knee, white, "Pad", at, turn, new Vector3(PadWidth, along.magnitude * PadLength * 0.5f, PadDepth));
    }

    private static void Glove(Animator a, HumanBodyBones hand, HumanBodyBones finger, Material white)
    {
        Transform h = a.GetBoneTransform(hand), m = a.GetBoneTransform(finger);
        if (h == null)
            return;
        Vector3 toward = m != null ? (m.position - h.position).normalized : Vector3.zero;
        Part(PrimitiveType.Sphere, h, white, "Glove", h.position + toward * GloveForward, Quaternion.identity, Vector3.one * GloveSize);
    }

    private static Renderer Part(PrimitiveType shape, Transform parent, Material material, string name, Vector3 at, Quaternion turn, Vector3 size)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(part.GetComponent<Collider>());      // the ball passes through him
        part.name = name;
        SceneManager.MoveGameObjectToScene(part, parent.gameObject.scene);
        part.transform.SetPositionAndRotation(at, turn);
        part.transform.localScale = size;
        part.transform.SetParent(parent, true);
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    /// The top of his head (hair included) above the head bone, from the skinned mesh itself.
    private static float TopOfHead(Animator a, Transform head)
    {
        float top = float.NegativeInfinity;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer skin in a.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.name.Contains("Body") && !skin.name.Contains("Hair"))
                continue;
            skin.BakeMesh(baked, true);
            Vector3 at = skin.transform.position;
            Quaternion turn = skin.transform.rotation;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = at + turn * v;
                if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(head.position.x, head.position.z)) < HeadSearchRadius)
                    top = Mathf.Max(top, w.y);
            }
        }
        Object.DestroyImmediate(baked);
        return float.IsNegativeInfinity(top) ? DefaultHeadTop : top - head.position.y;
    }

    /// Sets a private serialized field (the builders fill components that runtime code only reads).
    public static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new System.MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }
}
