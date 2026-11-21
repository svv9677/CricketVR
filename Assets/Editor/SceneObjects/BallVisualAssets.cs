using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The seamed ball's mesh and texture, made the way BallVisual made them in code as the game
/// started (until 2026-11-15). BallVisualBuilder saves what these return.
/// </summary>
public static class BallVisualAssets
{
    /// The seam band's middle and how fast the texture runs across it per radian of latitude.
    private const float SeamU = 0.29f, UPerRadian = 1.0f, UMin = 0.03f, UMax = 0.56f;
    /// Times the photographed stretch of stitching repeats round the ball.
    private const float StitchRepeats = 5f;
    /// The photographed seam band's edges across the texture; outside them, plain leather.
    private const float BandFrom = 0.105f, BandTo = 0.47f, Feather = 0.035f;

    /// `source`'s texture with the photo's seam band kept and the rest of it (shading and creases
    /// from the photograph, which sampled at a fixed column made stripes radiating from the poles)
    /// replaced with even leather of the same colour. Null if the source has no texture.
    public static Texture2D SeamedTexture(Texture src)
    {
        if (src == null)
            return null;
        int w = src.width, h = src.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Seamed Ball", wrapMode = TextureWrapMode.Repeat };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels();
        Color leather = Leather(px, w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float inBand = Mathf.Min(Mathf.InverseLerp(BandFrom - Feather, BandFrom, u), 1f - Mathf.InverseLerp(BandTo, BandTo + Feather, u));
                px[y * w + x] = Color.Lerp(leather, px[y * w + x], Mathf.Clamp01(inBand));
            }
        tex.SetPixels(px);
        tex.Apply(true, false);
        return tex;
    }

    /// The leather: the brightest half of the pixels just outside the band, averaged.
    private static Color Leather(Color[] px, int w, int h)
    {
        var samples = new List<Color>();
        for (int y = 0; y < h; y++)
            for (int x = Mathf.RoundToInt((BandTo + 0.01f) * w); x < Mathf.RoundToInt((BandTo + 0.07f) * w); x++)
                samples.Add(px[y * w + x]);
        samples.Sort((a, b) => b.grayscale.CompareTo(a.grayscale));
        Color leather = Color.black;
        int n = Mathf.Max(1, samples.Count / 2);
        for (int i = 0; i < n; i++) leather += samples[i];
        leather /= n;
        leather.a = 1f;
        return leather;
    }

    /// A unit sphere, poles on local X (the seam's axis), latitude rings packed tight at the seam.
    public static Mesh SeamedMesh()
    {
        const int HalfRings = 24, Segments = 64;
        int rings = 2 * HalfRings + 1;
        var verts = new Vector3[rings * (Segments + 1)];
        var uvs = new Vector2[verts.Length];
        int k = 0;
        for (int i = 0; i < rings; i++)
        {
            // Latitude from the seam, -pi/2..pi/2, finer near 0 where the stitching is.
            float s = (i - HalfRings) / (float)HalfRings;
            float lat = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 1.7f) * Mathf.PI * 0.5f;
            float u = Mathf.Clamp(SeamU + UPerRadian * lat, UMin, UMax);
            for (int j = 0; j <= Segments; j++, k++)
            {
                float lon = j / (float)Segments * Mathf.PI * 2f;
                verts[k] = new Vector3(Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Cos(lat) * Mathf.Sin(lon));
                uvs[k] = new Vector2(u, j / (float)Segments * StitchRepeats);
            }
        }
        var tris = new int[(rings - 1) * Segments * 6];
        int t = 0;
        for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < Segments; j++)
            {
                int a = i * (Segments + 1) + j, b = a + Segments + 1;
                tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                tris[t++] = a + 1; tris[t++] = b + 1; tris[t++] = b;
            }
        FaceOutwards(verts, tris, HalfRings * Segments * 6);
        var mesh = new Mesh { name = "Seamed Ball" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.normals = System.Array.ConvertAll(verts, v => v.normalized);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// Checks the winding against the outward direction on the triangle at `m` (at the seam; the
    /// ones at the poles are degenerate) and flips every triangle if it faces inward.
    private static void FaceOutwards(Vector3[] verts, int[] tris, int m)
    {
        Vector3 n = Vector3.Cross(verts[tris[m + 1]] - verts[tris[m]], verts[tris[m + 2]] - verts[tris[m]]);
        if (Vector3.Dot(n, verts[tris[m]] + verts[tris[m + 1]] + verts[tris[m + 2]]) >= 0f)
            return;
        for (int q = 0; q < tris.Length; q += 3)
        {
            int tmp = tris[q + 1];
            tris[q + 1] = tris[q + 2];
            tris[q + 2] = tmp;
        }
    }
}
