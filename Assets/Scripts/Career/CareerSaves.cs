using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The three career save slots. Each is a JSON file in Application.persistentDataPath/career
/// ("slot1.json"), written atomically (to a temp file, then moved over) with the previous good
/// file kept as "slot1.bak" - so a crash, a dead battery or a headset taken off mid-write never
/// loses a career. A file that will not read falls back to its backup.
///
/// Files, not PlayerPrefs: a career grows to a few hundred measured balls, and the EditMode tests
/// clear PlayerPrefs (GameSettingsTests) - tests point Directory elsewhere (UseDirectoryForTests).
///
/// Active is the slot being played this session (a static, so it survives the scene loads into the
/// nets and matches and back); Current is its profile, loaded once and saved after every ball.
/// </summary>
public static class CareerSaves
{
    public const int Slots = 3;

    private static string directory;
    private static CareerProfile current;
    private static int active = -1;

    public static string Directory => directory ??= Path.Combine(Application.persistentDataPath, "career");

    /// The slot being played (0..Slots-1), or -1.
    public static int Active => active;
    /// The active slot's profile (null when none is active).
    public static CareerProfile Current => current;

    /// Raised after the active profile is saved (the hub refreshes its card).
    public static event Action Saved;

    private static string PathFor(int slot) => Path.Combine(Directory, $"slot{slot + 1}.json");
    private static string BackupFor(int slot) => Path.Combine(Directory, $"slot{slot + 1}.bak");

    public static bool Exists(int slot) => Valid(slot) && (File.Exists(PathFor(slot)) || File.Exists(BackupFor(slot)));

    /// The profile in a slot, or null when the slot is empty (or unreadable, with no backup).
    public static CareerProfile Load(int slot)
    {
        if (!Valid(slot)) return null;
        return Read(PathFor(slot)) ?? Read(BackupFor(slot));
    }

    /// Make `slot` the active one, loading its profile. False when it is empty.
    public static bool Activate(int slot)
    {
        CareerProfile p = Load(slot);
        if (p == null) return false;
        active = slot;
        current = p;
        return true;
    }

    /// Start a new career in `slot` (overwriting whatever was there) and make it active.
    public static void Create(int slot, CareerProfile p)
    {
        if (!Valid(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        if (string.IsNullOrEmpty(p.created))
            p.created = DateTime.Now.ToString("yyyy-MM-dd");
        Write(slot, p);
        active = slot;
        current = p;
    }

    /// Save the active profile (after every ball, and at every change).
    public static void SaveCurrent()
    {
        if (current == null || active < 0) return;
        Write(active, current);
        Saved?.Invoke();
    }

    public static void Delete(int slot)
    {
        if (!Valid(slot)) return;
        TryDelete(PathFor(slot));
        TryDelete(BackupFor(slot));
        TryDelete(PathFor(slot) + ".tmp");
        if (slot == active) Deactivate();
    }

    public static void Deactivate()
    {
        active = -1;
        current = null;
    }

    /// Tests: keep saves in `dir` (null restores the real place) and forget the active slot.
    public static void UseDirectoryForTests(string dir)
    {
        directory = dir;
        Deactivate();
        Saved = null;
    }

    private static bool Valid(int slot) => slot >= 0 && slot < Slots;

    private static void Write(int slot, CareerProfile p)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = PathFor(slot), tmp = path + ".tmp", bak = BackupFor(slot);
            File.WriteAllText(tmp, JsonUtility.ToJson(p));
            if (File.Exists(path))
            {
                // Keep the last good file as the backup, then move the new one into place.
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Career] could not save slot {slot + 1}: {e.Message}");
        }
    }

    private static CareerProfile Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            CareerProfile p = JsonUtility.FromJson<CareerProfile>(json);
            if (p == null || string.IsNullOrEmpty(p.name)) return null;
            return Migrate(p);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Career] could not read {Path.GetFileName(path)}: {e.Message}");
            return null;
        }
    }

    /// Bring an older save up to CareerProfile.CurrentVersion. Version 1 had no career yet: it gets
    /// the empty record book and inbox, and its first season is made when the hub first opens
    /// (CareerProgress.EnsureSeason).
    private static CareerProfile Migrate(CareerProfile p)
    {
        p.innings ??= new System.Collections.Generic.List<InningsRecord>();
        p.stats ??= new CareerStats();
        p.inbox ??= new System.Collections.Generic.List<CareerMessage>();
        p.scoutReports ??= new System.Collections.Generic.List<float>();
        p.unlocked ??= new System.Collections.Generic.List<string>();
        p.pending ??= new PendingMatch();
        p.drills ??= new DrillBook();
        p.drills.records ??= new System.Collections.Generic.List<DrillRecord>();
        if (p.battingSlot < 1 || p.battingSlot > Ladder.LowestSlot(p.rung)) p.battingSlot = Ladder.LowestSlot(p.rung);
        // JsonUtility gives an empty Season (not null) for a missing field: a season with no teams
        // is no season.
        if (p.season != null && (p.season.teams == null || p.season.teams.Count == 0))
            p.season = null;
        p.look ??= new CareerAppearance();
        p.ratings ??= new RatingSet();
        p.trialRatings ??= new RatingSet();
        p.balls ??= new System.Collections.Generic.List<BallRecord>();
        p.totals ??= new CareerTotals();
        p.version = CareerProfile.CurrentVersion;
        return p;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) { Debug.LogWarning($"[Career] could not delete {Path.GetFileName(path)}: {e.Message}"); }
    }
}
