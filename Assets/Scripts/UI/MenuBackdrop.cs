using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The graded stadium plate behind the menu. Enabled in Splash, where there is nothing else to
/// look at; disabled in CricketVR and Nets, where the real ground is right there and a backdrop
/// would only hide it.
///
/// The plates are baked by Tools > CricketVR > Bake Menu Backdrops into
/// Assets/Resources/UI/Backdrops. Each screen can name the one it wants, so the set reads as one
/// ground seen from three places rather than one repeated image.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    [Tooltip("The Image that shows the plate. Sits behind the menu card in the same canvas.")]
    [SerializeField] private Image image;

    [Tooltip("Plate shown when the menu opens, by name under Resources/UI/Backdrops.")]
    [SerializeField] private string defaultPlate = "Backdrop-BowlersEnd";

    public void Show()
    {
        gameObject.SetActive(true);
        SetPlate(defaultPlate);
    }

    public void Hide() => gameObject.SetActive(false);

    /// Swap the plate, so each screen can have its own vantage point. A missing plate leaves the
    /// current one up rather than flashing to blank.
    public void SetPlate(string plateName)
    {
        if (image == null || string.IsNullOrEmpty(plateName))
            return;
        var sprite = Resources.Load<Sprite>($"UI/Backdrops/{plateName}");
        if (sprite != null)
            image.sprite = sprite;
    }
}
