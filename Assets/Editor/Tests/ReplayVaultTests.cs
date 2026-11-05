using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Record clips on disk (ReplayVault): a prune keeps only the clips named, a write is whole or not
/// there at all, queued work runs one at a time in order, a career's clips go with it, and the
/// theatre only opens on a readable clip whose header carries the record's stamp, never during a
/// career match. Every test works in its own temp directory, never the real saves.
/// </summary>
public class ReplayVaultTests
{
    private string dir;

    [SetUp]
    public void UseTempDirectory()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_vault_tests_" + Guid.NewGuid().ToString("N"));
        ReplayVault.UseDirectoryForTests(dir);
    }

    [TearDown]
    public void RestoreDirectory()
    {
        ReplayVault.WaitForWrites(5000);
        ReplayVault.UseDirectoryForTests(null);
        CareerSaves.UseDirectoryForTests(null);
        CareerMatchSession.ResetForTests();
        ReplayTheatre.ResetForTests();
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    private static void Put(int slot, string name, string text = "clip")
    {
        Directory.CreateDirectory(ReplayVault.SlotDir(slot));
        File.WriteAllText(Path.Combine(ReplayVault.SlotDir(slot), name), text);
    }

    private static string[] Names(int slot)
    {
        var names = new List<string>();
        if (Directory.Exists(ReplayVault.SlotDir(slot)))
            foreach (string f in Directory.GetFiles(ReplayVault.SlotDir(slot)))
                names.Add(Path.GetFileName(f));
        names.Sort(StringComparer.Ordinal);
        return names.ToArray();
    }

    [Test]
    public void Prune_KeepsOnlyTheClipsNamed()
    {
        Put(0, "a.cvrclip");
        Put(0, "b.cvrclip");
        Put(0, "c.cvrclip.tmp");
        Put(0, "notes.txt");
        Put(1, "a.cvrclip");
        ReplayVault.Prune(0, new[] { "b", null, "" });
        CollectionAssert.AreEqual(new[] { "b.cvrclip", "notes.txt" }, Names(0), "a clip not named and a left-over temp go");
        CollectionAssert.AreEqual(new[] { "a.cvrclip" }, Names(1), "another slot is left alone");

        ReplayVault.Prune(1, null);
        CollectionAssert.IsEmpty(Names(1), "nothing named: no clip kept");
        Assert.DoesNotThrow(() => ReplayVault.Prune(2, new[] { "x" }), "no folder: nothing to do");
        Assert.DoesNotThrow(() => ReplayVault.Prune(-1, null));
    }

    [Test]
    public void AWrite_IsWholeOrNotThereAtAll()
    {
        string path = ReplayVault.PathFor(0, "s1");
        Assert.IsTrue(ReplayVault.Write(path, new byte[] { 1, 2, 3 }), "the slot's folder is made");
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, ReplayVault.Read(path));
        Assert.IsFalse(File.Exists(path + ".tmp"), "written beside it, then moved into place");

        Assert.IsTrue(ReplayVault.Write(path, new byte[] { 9 }));
        CollectionAssert.AreEqual(new byte[] { 9 }, ReplayVault.Read(path), "a second write replaces it");

        Assert.IsFalse(ReplayVault.Write(path, null));
        Assert.IsFalse(ReplayVault.Write(path, new byte[0]));
        Assert.IsFalse(ReplayVault.Write("", new byte[] { 1 }));
        CollectionAssert.AreEqual(new byte[] { 9 }, ReplayVault.Read(path), "a write that cannot happen leaves the old clip");
        Assert.IsNull(ReplayVault.Read(ReplayVault.PathFor(0, "missing")));
        Assert.IsTrue(ReplayVault.Has(0, "s1"));
    }

    [Test]
    public void QueuedWork_RunsOneAtATime_InOrder()
    {
        var order = new List<int>();
        int running = 0, most = 0;
        for (int i = 0; i < 20; i++)
        {
            int n = i;
            ReplayVault.Queue(() =>
            {
                int now = System.Threading.Interlocked.Increment(ref running);
                lock (order) { most = Math.Max(most, now); order.Add(n); }
                System.Threading.Thread.Sleep(1);
                System.Threading.Interlocked.Decrement(ref running);
            });
        }
        Assert.IsTrue(ReplayVault.WaitForWrites(5000));
        Assert.AreEqual(20, order.Count);
        for (int i = 0; i < 20; i++)
            Assert.AreEqual(i, order[i]);
        Assert.AreEqual(1, most, "never two at once");
    }

    [Test]
    public void QueuedWork_ThatFails_DoesNotStopTheChain()
    {
        bool after = false;
        ReplayVault.Queue(() => throw new IOException("disk full"));
        ReplayVault.Queue(() => after = true);
        Assert.IsTrue(ReplayVault.WaitForWrites(5000));
        Assert.IsTrue(after);
    }

    [Test]
    public void Stamps_AreSafeFileNamesOnly()
    {
        Assert.IsTrue(ReplayVault.SafeStamp("5eed0001-b14"));
        Assert.IsTrue(ReplayVault.SafeStamp("A_b-9"));
        foreach (string bad in new[] { null, "", "../x", "a/b", "a\\b", "a b", "x.cvrclip", new string('a', 65) })
            Assert.IsFalse(ReplayVault.SafeStamp(bad), bad ?? "null");
    }

    [Test]
    public void ACareerStartedOrDeleted_ClearsItsSlotFolder()
    {
        CareerSaves.UseDirectoryForTests(dir);
        ReplayVault.UseDirectoryForTests(null);     // beside the saves: clips/slot{n}
        CareerSaves.Create(0, CareerTestPlay.Career(3));
        Assert.IsTrue(ReplayVault.Write(ReplayVault.PathFor(0, "a"), new byte[] { 1 }));
        Assert.IsTrue(ReplayVault.Write(ReplayVault.PathFor(1, "b"), new byte[] { 1 }));
        Assert.AreEqual(Path.Combine(dir, "clips", "slot1"), ReplayVault.SlotDir(0));
        CareerSaves.Delete(0);
        Assert.IsFalse(Directory.Exists(ReplayVault.SlotDir(0)));
        Assert.IsTrue(ReplayVault.Has(1, "b"));

        Assert.IsTrue(ReplayVault.Write(ReplayVault.PathFor(0, "c"), new byte[] { 1 }));
        CareerSaves.Create(0, CareerTestPlay.Career(4));
        Assert.IsFalse(Directory.Exists(ReplayVault.SlotDir(0)), "a new career in the slot starts with none");
    }

    /// The active career in slot 0 with its longest six's clip saved under `fileStamp`, its book
    /// pointing at `bookStamp`.
    private void CareerWithClip(string bookStamp, string fileStamp)
    {
        CareerSaves.UseDirectoryForTests(dir);
        ReplayVault.UseDirectoryForTests(null);
        CareerProfile p = CareerTestPlay.Career(5);
        p.recordBook.longestSix.set = true;
        p.recordBook.longestSix.value = 104.5f;
        p.recordBook.longestSix.clipStamp = bookStamp;
        CareerSaves.Create(0, p);
        byte[] clip = ClipCodec.Encode(ClipCodecTests.Recording(4f, 2f, 2.5f), ClipCodecTests.RotKeys,
                                       ClipCodecTests.PosKeys, ClipCodecTests.Header(bookStamp));
        Assert.IsTrue(ReplayVault.Write(ReplayVault.PathFor(0, fileStamp), clip));
    }

    [Test]
    public void TheTheatre_OpensOnlyOnTheRecordsOwnReadableClip()
    {
        CareerSaves.UseDirectoryForTests(dir);      // no career active
        Assert.IsFalse(ReplayTheatre.Begin(0, eRecord.LongestSix), "no career");
        Assert.IsFalse(ReplayTheatre.Active);

        CareerWithClip("good-1", "good-1");
        Assert.IsTrue(ReplayTheatre.Ready(0, eRecord.LongestSix, out ClipHeader h, out byte[] data));
        Assert.AreEqual("good-1", h.stamp);
        Assert.AreEqual(eVenue.ClubGround, h.venue);
        Assert.IsNotNull(ClipCodec.Unpack(data, h));
        Assert.IsFalse(ReplayTheatre.Ready(1, eRecord.LongestSix, out _, out _), "not the active slot");
        Assert.IsFalse(ReplayTheatre.Ready(0, eRecord.HighScore, out _, out _), "no clip for that record");
        Assert.IsFalse(ReplayTheatre.Active, "Ready only looks");

        CareerMatchSession.SetPlanForTests(new MatchPlan());
        Assert.IsFalse(ReplayTheatre.Ready(0, eRecord.LongestSix, out _, out _), "never during a career match");
        Assert.IsFalse(ReplayTheatre.Begin(0, eRecord.LongestSix));
        Assert.IsFalse(ReplayTheatre.Active);
    }

    [Test]
    public void TheTheatre_RefusesAClipThatIsNotTheRecords()
    {
        CareerWithClip("book-1", "book-1");
        File.WriteAllBytes(ReplayVault.PathFor(0, "book-1"), new byte[] { 1, 2, 3 });
        Assert.IsFalse(ReplayTheatre.Ready(0, eRecord.LongestSix, out _, out _), "unreadable");

        CareerWithClip("book-2", "book-2");
        Assert.IsTrue(ReplayTheatre.Ready(0, eRecord.LongestSix, out _, out _));
        byte[] other = ClipCodec.Encode(ClipCodecTests.Recording(4f, 2f, 2.5f), ClipCodecTests.RotKeys,
                                        ClipCodecTests.PosKeys, ClipCodecTests.Header("someone-else"));
        Assert.IsTrue(ReplayVault.Write(ReplayVault.PathFor(0, "book-2"), other));
        Assert.IsFalse(ReplayTheatre.Ready(0, eRecord.LongestSix, out _, out _), "its header names another clip");
        CareerSaves.Current.recordBook.longestSix.clipStamp = "../book-2";
        Assert.IsFalse(ReplayTheatre.Ready(0, eRecord.LongestSix, out _, out _), "not a safe file name");
    }

    [Test]
    public void TheCaption_IsShortLatin1_AndSaysHowToLeave()
    {
        foreach (ClipHeader h in new[] { ClipCodecTests.Header(), new ClipHeader(), null })
        {
            string heading = ReplayTheatre.Heading(h), body = ReplayTheatre.Body(h);
            Assert.LessOrEqual(heading.Length, 31, heading);
            StringAssert.Contains("Main Menu, to leave", body);
            foreach (char c in heading + body)
                Assert.LessOrEqual((int)c, 0xFF, $"'{c}' is not Latin-1");
        }
        ClipHeader six = ClipCodecTests.Header();
        six.value = 103.6f;
        Assert.AreEqual("Longest six: 104 m", ReplayTheatre.Heading(six));
        StringAssert.Contains("vs Northgate", ReplayTheatre.Body(ClipCodecTests.Header()));
    }
}
