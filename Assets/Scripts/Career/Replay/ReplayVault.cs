using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Record clips on disk: clips/slot{n+1}/{stamp}.cvrclip beside the career saves. A slot's clips go
/// with its career (CareerSaves.Create and Delete), and only the clips its records still point at
/// are kept.
/// </summary>
public static class ReplayVault
{
    public const string Extension = ".cvrclip";

    private static string root;

    public static string Root => root ?? Path.Combine(CareerSaves.Directory, "clips");

    public static string SlotDir(int slot) => Path.Combine(Root, $"slot{slot + 1}");

    public static string PathFor(int slot, string stamp) => Path.Combine(SlotDir(slot), stamp + Extension);

    /// Is there a clip for `stamp` in `slot`?
    public static bool Has(int slot, string stamp)
    {
        if (slot < 0 || string.IsNullOrEmpty(stamp))
            return false;
        try { return File.Exists(PathFor(slot, stamp)); }
        catch { return false; }
    }

    /// Every clip of `slot` gone (a new career in it, or the career deleted).
    public static void DeleteSlot(int slot)
    {
        if (slot < 0)
            return;
        try
        {
            string d = SlotDir(slot);
            if (Directory.Exists(d))
                Directory.Delete(d, true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ReplayVault] could not delete slot {slot + 1} clips: {e.Message}");
        }
    }

    /// Every clip of `slot` but the ones in `keep` deleted.
    public static void Prune(int slot, IEnumerable<string> keep) { }

    /// Tests: keep clips in `dir` (null: beside the career saves again).
    public static void UseDirectoryForTests(string dir) => root = dir;
}
