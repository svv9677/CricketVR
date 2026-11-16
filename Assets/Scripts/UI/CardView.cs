using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The parts of a reading card that code fills in: its heading, its body and its one button. The
/// cards (Resources/Prefabs/UI: ResultCard, NextOverCard, NetsTrialCard, CareerMatchCard) are
/// prefabs built by Scene Objects > Build Card Prefabs, each saved inactive with its text set up
/// for CardText and its button wired to OnButton here. Until 2026-11-15 each card was a copy of
/// NextBallMenu's panel that code cut down by child name as it was shown.
///
/// Create instantiates one under a parent (the card is shown from a scene's own system, so it goes
/// with the scene); the owner then sets the words (CardText.Set) and listens to Clicked.
/// </summary>
public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text body;
    [SerializeField] private TMP_Text buttonLabel;

    /// The button was clicked (the laser; A is read by whoever owns the card).
    public event Action Clicked;

    public TMP_Text Heading => heading;
    public TMP_Text Body => body;
    public TMP_Text ButtonLabel => buttonLabel;

    /// The prefab at `resource` (under Resources), inactive, under `parent`; null with an error in
    /// the log when it is not there (run Scene Objects > Build Card Prefabs).
    public static CardView Create(string resource, Transform parent)
    {
        GameObject prefab = Resources.Load<GameObject>(resource);
        if (prefab == null)
        {
            Debug.LogError($"[CardView] No card prefab at Resources/{resource}: run CricketVR > Scene Objects > Build Card Prefabs.");
            return null;
        }
        GameObject go = Instantiate(prefab, parent);
        go.name = prefab.name;
        go.SetActive(false);
        return go.GetComponent<CardView>();
    }

    /// Wired to the button's onClick in the prefab.
    public void OnButton() => Clicked?.Invoke();

    public void SetButton(string text)
    {
        if (buttonLabel != null)
            buttonLabel.text = text;
    }
}
