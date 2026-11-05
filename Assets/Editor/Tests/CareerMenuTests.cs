using NUnit.Framework;

/// <summary>
/// The career menus' rules that need no scene: the name keyboard (OnScreenKeyboard - capitals for
/// you, the allowed characters, the length cap, Backspace), the Random names, the Create Look
/// steppers' wrap, and the slot line. Standalone; nothing here reads or writes a save.
/// Run: Window > General > Test Runner > EditMode > CareerMenuTests.
/// </summary>
public class CareerMenuTests
{
    // ---- The name keyboard ----------------------------------------------------------------------

    [Test]
    public void EveryWordStartsWithACapitalAndTheRestAreLowerCase()
    {
        Assert.AreEqual("Asha Patel-Rao", OnScreenKeyboard.Retype("asha patel-rao"));
        Assert.AreEqual("Asha Patel-Rao", OnScreenKeyboard.Retype("ASHA PATEL-RAO"));
        Assert.AreEqual("As", OnScreenKeyboard.Type("A", "S"), "the keys are capitals; mid-word they type lower case");
    }

    [Test]
    public void AOneLetterStartBeforeAnApostropheStartsTheWordAgain()
    {
        Assert.AreEqual("O'Neill", OnScreenKeyboard.Retype("o'neill"));
        Assert.AreEqual("Liam D'Souza", OnScreenKeyboard.Retype("liam d'souza"));
        Assert.AreEqual("Don't", OnScreenKeyboard.Retype("DON'T"), "after a longer word it is just an apostrophe");
    }

    [Test]
    public void OnlyLettersSpacesHyphensAndApostrophesAreTyped()
    {
        Assert.AreEqual("Ab", OnScreenKeyboard.Type("Ab", "7"));
        Assert.AreEqual("Ab", OnScreenKeyboard.Type("Ab", "<"));
        Assert.AreEqual("Ab", OnScreenKeyboard.Type("Ab", "é"), "Latin-1 accents are not on the keyboard");
        Assert.AreEqual("Ab", OnScreenKeyboard.Type("Ab", "CD"), "one character per key");
        Assert.AreEqual("Ab", OnScreenKeyboard.Type("Ab", null));
        Assert.AreEqual("Ab", OnScreenKeyboard.Type("Ab", ""));
        Assert.AreEqual("A", OnScreenKeyboard.Type(null, "a"));
    }

    [Test]
    public void ASeparatorIsNeverFirstNorTwoInARow()
    {
        Assert.AreEqual("", OnScreenKeyboard.Type("", OnScreenKeyboard.Space));
        Assert.AreEqual("", OnScreenKeyboard.Type("", OnScreenKeyboard.Hyphen));
        Assert.AreEqual("", OnScreenKeyboard.Type("", OnScreenKeyboard.Apostrophe));
        Assert.AreEqual("Ab ", OnScreenKeyboard.Type("Ab ", OnScreenKeyboard.Space));
        Assert.AreEqual("Ab-", OnScreenKeyboard.Type("Ab-", OnScreenKeyboard.Apostrophe));
        Assert.AreEqual("Ab-", OnScreenKeyboard.Type("Ab", OnScreenKeyboard.Hyphen));
    }

    [Test]
    public void ANameStopsAtSixteenCharacters()
    {
        string text = "";
        for (int i = 0; i < 30; i++)
            text = OnScreenKeyboard.Type(text, "M");
        Assert.AreEqual(OnScreenKeyboard.MaxLength, text.Length);
        Assert.AreEqual(16, OnScreenKeyboard.MaxLength);
        Assert.AreEqual(text, OnScreenKeyboard.Type(text, OnScreenKeyboard.Space));
    }

    [Test]
    public void BackspaceRemovesTheLastCharacter()
    {
        Assert.AreEqual("Ash", OnScreenKeyboard.Backspace("Asha"));
        Assert.AreEqual("Asha", OnScreenKeyboard.Backspace("Asha "));
        Assert.AreEqual("", OnScreenKeyboard.Backspace("A"));
        Assert.AreEqual("", OnScreenKeyboard.Backspace(""));
        Assert.AreEqual("", OnScreenKeyboard.Backspace(null));
        // After a Backspace the next letter is cased for where it now lands.
        Assert.AreEqual("Asha R", OnScreenKeyboard.Type(OnScreenKeyboard.Backspace("Asha X"), "r"));
    }

    [Test]
    public void ANameNeedsTwoLettersAndLosesATrailingSeparator()
    {
        Assert.IsFalse(OnScreenKeyboard.IsValid(""));
        Assert.IsFalse(OnScreenKeyboard.IsValid("A"));
        Assert.IsFalse(OnScreenKeyboard.IsValid("A-"));
        Assert.IsTrue(OnScreenKeyboard.IsValid("Al"));
        Assert.IsTrue(OnScreenKeyboard.IsValid("A B"));
        Assert.AreEqual("Asha", OnScreenKeyboard.Finish("Asha "));
        Assert.AreEqual("Asha Rao", OnScreenKeyboard.Finish("Asha Rao-"));
        Assert.AreEqual("", OnScreenKeyboard.Finish(null));
    }

    [Test]
    public void RandomNamesAreOnesTheKeyboardCouldHaveTyped()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            string name = CareerDraft.RandomName(new System.Random(seed));
            Assert.LessOrEqual(name.Length, OnScreenKeyboard.MaxLength, name);
            Assert.AreEqual(name, OnScreenKeyboard.Retype(name), name);
            Assert.IsTrue(OnScreenKeyboard.IsValid(name), name);
            Assert.AreEqual(name, OnScreenKeyboard.Finish(name), name);
        }
    }

    [Test]
    public void TheKeyboardRowsAreTheTwentySixLettersOnce()
    {
        string all = string.Concat(OnScreenKeyboard.Rows);
        Assert.AreEqual(26, all.Length);
        for (char c = 'A'; c <= 'Z'; c++)
            Assert.AreEqual(1, all.Split(c).Length - 1, c.ToString());
    }

    // ---- Steppers ---------------------------------------------------------------------------------

    [Test]
    public void SteppersWrapRoundAtBothEnds()
    {
        Assert.AreEqual(5, CreateLookScreen.Wrap(0, -1, 0, 5));
        Assert.AreEqual(0, CreateLookScreen.Wrap(5, 1, 0, 5));
        Assert.AreEqual(3, CreateLookScreen.Wrap(2, 1, 0, 5));
        Assert.AreEqual(1, CreateLookScreen.Wrap(CreateLookScreen.MaxNumber, 1, CreateLookScreen.MinNumber, CreateLookScreen.MaxNumber));
        Assert.AreEqual(99, CreateLookScreen.Wrap(1, -1, CreateLookScreen.MinNumber, CreateLookScreen.MaxNumber));
        Assert.AreEqual(19, CreateLookScreen.Wrap(18, 1, 1, 99));
    }

    [Test]
    public void AStepperBroughtAnOutOfRangeValueBackInside()
    {
        int n = CreateLookScreen.Wrap(150, 0, 1, 99);
        Assert.GreaterOrEqual(n, 1);
        Assert.LessOrEqual(n, 99);
        Assert.AreEqual(0, CreateLookScreen.Wrap(-7, 0, 0, 0));
    }

    [Test]
    public void TheNationStepperVisitsEveryNationAndComesBack()
    {
        int i = Nations.IndexOf("IND");
        for (int step = 0; step < Nations.All.Length; step++)
            i = CreateLookScreen.Wrap(i, 1, 0, Nations.All.Length - 1);
        Assert.AreEqual(Nations.IndexOf("IND"), i);
        Assert.AreEqual(Nations.All.Length - 1, CreateLookScreen.Wrap(0, -1, 0, Nations.All.Length - 1));
    }

    // ---- Slots and the draft ----------------------------------------------------------------------

    [Test]
    public void TheSlotLineShowsTheNationHandAndOverall()
    {
        var p = new CareerProfile { name = "Asha Rao", nation = "IND", hand = eBattingStyle.LeftHanded };
        Assert.AreEqual("India · Left-hand bat · Overall --", CareerScreen.Detail(p));
        p.ratings.timing = 50f;
        p.ratings.power = 58f;
        Assert.AreEqual("India · Left-hand bat · Overall 54", CareerScreen.Detail(p), "the mean of the known ratings, rounded");
    }

    [Test]
    public void ADraftIsFreshForItsSlotUntilCleared()
    {
        CareerDraft.Begin(1);
        Assert.IsTrue(CareerDraft.Active);
        Assert.AreEqual(1, CareerDraft.Slot);
        Assert.AreEqual("", CareerDraft.Profile.name);
        Assert.IsFalse(CareerDraft.Profile.trialDone);
        CareerDraft.Clear();
        Assert.IsFalse(CareerDraft.Active);
        Assert.IsNull(CareerDraft.Profile);
        CareerDraft.Begin(CareerSaves.Slots);
        Assert.IsFalse(CareerDraft.Active, "no such slot");
        CareerDraft.Clear();
    }
}
