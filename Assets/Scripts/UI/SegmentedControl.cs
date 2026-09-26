using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// A row of mutually exclusive options (Left | Right, Easy | Medium | Hard, ...), used in place of
/// dropdowns wherever there are only a few choices. Each option is a Toggle in one ToggleGroup
/// whose onValueChanged is wired (saved listener, built by Tools > CricketVR > Build Player UI) to
/// OnOption with its own index; onValueChanged reports the chosen index once per change.
/// </summary>
public class SegmentedControl : MonoBehaviour
{
    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    [SerializeField] private Toggle[] options = new Toggle[0];
    public IntEvent onValueChanged = new IntEvent();

    public int Value { get; private set; } = -1;
    public int Count => options.Length;

    /// Wired to option `index`. The option being switched off also calls in; only the one that
    /// ends up on counts.
    public void OnOption(int index)
    {
        if (index < 0 || index >= options.Length || options[index] == null || !options[index].isOn || index == Value)
            return;
        Value = index;
        onValueChanged.Invoke(index);
    }

    /// Show `index` as chosen without reporting it (the panel mirroring the game's settings).
    public void SetValueWithoutNotify(int index)
    {
        Value = index;
        // The chosen one first: switching the lit option off while no other is on would be
        // refused by the group.
        if (index >= 0 && index < options.Length && options[index] != null)
            options[index].SetIsOnWithoutNotify(true);
        for (int i = 0; i < options.Length; i++)
            if (i != index && options[i] != null)
                options[i].SetIsOnWithoutNotify(false);
    }

#if UNITY_EDITOR
    /// Editor builder only.
    public void SetOptions(Toggle[] toggles) => options = toggles;
#endif
}
