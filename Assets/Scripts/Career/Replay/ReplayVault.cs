using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Record clips on disk: clips/slot{n+1}/{stamp}.cvrclip beside the career saves. A slot's clips go
/// with its career (CareerSaves.Create and Delete), and only the clips its records still point at
/// are kept (Prune, as each career match starts).
///
/// Clips are written off the main thread, one at a time on a single chain of tasks (Queue), each to
/// a temporary file that is then moved into place, so a clip on disk is always a whole one. Every
/// path is worked out on the main thread before the work is queued: the saves' folder comes from
/// Application.persistentDataPath, which may only be read there.
/// </summary>
public static class ReplayVault
{
    public const string Extension = ".cvrclip";

    private static string root;
    private static Task chain = Task.CompletedTask;
    private static readonly object gate = new object();
    /// How long DeleteSlot and Prune wait for a clip still being written (ms).
    private const int WaitForWritesMs = 3000;

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
        WaitForWrites(WaitForWritesMs);   // a clip still being written lands before its folder goes
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

    /// Every clip of `slot` but the ones in `keep` deleted (and any temporary file a write left).
    public static void Prune(int slot, IEnumerable<string> keep)
    {
        if (slot < 0)
            return;
        WaitForWrites(WaitForWritesMs);
        var kept = new HashSet<string>();
        if (keep != null)
            foreach (string k in keep)
                if (!string.IsNullOrEmpty(k))
                    kept.Add(k + Extension);
        try
        {
            string d = SlotDir(slot);
            if (!Directory.Exists(d))
                return;
            foreach (string f in Directory.GetFiles(d))
            {
                string name = Path.GetFileName(f);
                bool clip = name.EndsWith(Extension, StringComparison.Ordinal);
                if ((clip && !kept.Contains(name)) || name.EndsWith(".tmp", StringComparison.Ordinal))
                    File.Delete(f);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayVault] could not prune slot {slot + 1} clips: {e.Message}");
        }
    }

    /// A stamp that is safe as a file name (letters, digits, '-' and '_' only).
    public static bool SafeStamp(string stamp)
    {
        if (string.IsNullOrEmpty(stamp) || stamp.Length > 64)
            return false;
        foreach (char c in stamp)
            if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_'))
                return false;
        return true;
    }

    /// `bytes` to `path`, whole or not at all: written beside it as ".tmp", then moved over it.
    /// False when it could not be written (nothing is left half-written). Any thread.
    public static bool Write(string path, byte[] bytes)
    {
        if (string.IsNullOrEmpty(path) || bytes == null || bytes.Length == 0)
            return false;
        string tmp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ReplayVault] could not write {Path.GetFileName(path)}: {e.Message}");
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (Exception) { }
            return false;
        }
    }

    /// The bytes at `path`, or null (missing or unreadable).
    public static byte[] Read(string path)
    {
        try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
        catch (Exception) { return null; }
    }

    /// `work` after everything queued before it, on a worker thread. It must not touch Unity objects
    /// or read a path: work those out first. A failure is logged and the chain goes on.
    public static Task Queue(Action work)
    {
        if (work == null)
            return chain;
        lock (gate)
        {
            chain = chain.ContinueWith(_ =>
            {
                try { work(); }
                catch (Exception e) { Debug.LogWarning($"[ReplayVault] clip work failed: {e.Message}"); }
            }, TaskScheduler.Default);
            return chain;
        }
    }

    /// Waits up to `ms` for the queued work to finish; true when it has.
    public static bool WaitForWrites(int ms)
    {
        Task t;
        lock (gate) t = chain;
        try { return t.Wait(ms); }
        catch (Exception) { return true; }
    }

    /// Tests: keep clips in `dir` (null: beside the career saves again).
    public static void UseDirectoryForTests(string dir) => root = dir;
}
