using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// File helpers for the Bat, Ball and Blink builders: a texture or a sound made in code is written
/// to a real .png or .wav file and imported, so it is an asset the prefabs can point at (and that
/// stays when the editor closes). Until 2026-11-15 the game made these in memory as it started.
/// </summary>
public static class VisualAssetFiles
{
    /// Writes `tex` to `path` as a PNG, imports it with the settings given, and returns the asset.
    /// The texture in memory is destroyed.
    public static Texture2D SavePng(Texture2D tex, string path, bool mipmaps, TextureWrapMode wrap, bool alpha)
    {
        SceneObjectAssets.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = mipmaps;
        importer.wrapMode = wrap;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// Writes mono 16-bit PCM `samples` (-1..1) to `path` as a WAV, imports it uncompressed (a loop
    /// must not go through a codec) and returns the clip.
    public static AudioClip SaveWav(float[] samples, int rate, string path)
    {
        SceneObjectAssets.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        using (var stream = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(stream))
        {
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);              // PCM
            w.Write((short)1);              // mono
            w.Write(rate);
            w.Write(rate * 2);              // bytes per second
            w.Write((short)2);              // bytes per sample frame
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float s in samples)
                w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        importer.forceToMono = true;
        importer.loadInBackground = false;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
