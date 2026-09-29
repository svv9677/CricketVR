using NUnit.Framework;

/// <summary>
/// MenuStack: which screen is showing and what Back does. Pure logic, no GameObjects, so the
/// navigation rules are provable without a scene.
/// Run: Window > General > Test Runner > EditMode > MenuStackTests.
/// </summary>
public class MenuStackTests
{
    [Test]
    public void Current_IsTheRootAfterReset()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        Assert.AreEqual("main", stack.Current);
    }

    [Test]
    public void Push_MakesTheNewScreenCurrent()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        stack.Push("settings");
        Assert.AreEqual("settings", stack.Current);
    }

    [Test]
    public void Pop_ReturnsToWhoeverPushed()
    {
        var stack = new MenuStack();
        stack.Reset("pause");
        stack.Push("settings");
        stack.Pop();
        Assert.AreEqual("pause", stack.Current,
                        "Settings must return to its opener, not to a fixed screen");
    }

    [Test]
    public void CanGoBack_IsFalseAtTheRoot()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        Assert.IsFalse(stack.CanGoBack, "the root screen shows no Back button");
    }

    [Test]
    public void CanGoBack_IsTrueOnceSomethingIsPushed()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        stack.Push("settings");
        Assert.IsTrue(stack.CanGoBack);
    }

    [Test]
    public void Pop_AtTheRootEmptiesTheStack()
    {
        var stack = new MenuStack();
        stack.Reset("pause");
        stack.Pop();
        Assert.IsNull(stack.Current,
                      "popping the last screen closes the menu, not leaves a blank canvas");
        Assert.IsFalse(stack.IsOpen);
    }

    [Test]
    public void Push_TheSameScreenTwiceDoesNotStack()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        stack.Push("settings");
        stack.Push("settings");
        stack.Pop();
        Assert.AreEqual("main", stack.Current,
                        "a double-click on Settings must not need two Backs");
    }

    [Test]
    public void Reset_DiscardsPreviousHistory()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        stack.Push("settings");
        stack.Reset("pause");
        Assert.AreEqual("pause", stack.Current);
        Assert.IsFalse(stack.CanGoBack,
                       "reopening the menu must not inherit the last session's Back trail");
    }

    [Test]
    public void Clear_ClosesTheMenu()
    {
        var stack = new MenuStack();
        stack.Reset("main");
        stack.Push("settings");
        stack.Clear();
        Assert.IsFalse(stack.IsOpen);
        Assert.IsNull(stack.Current);
    }
}
