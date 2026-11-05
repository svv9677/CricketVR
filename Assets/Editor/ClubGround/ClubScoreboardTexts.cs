using System.Text;
using TMPro;
using UnityEngine;

/// The score hut's two texts, part of ClubGroundBuilder's prefab assembly (kept apart so that
/// file stays readable).
public static partial class ClubGroundBuilder
{
    static partial class ClubPrefab
    {
        /// The score hut's texts: the header (the home side's name, ClubScoreboard.SetHomeName
        /// writes it each match; the default club's name until then) and the labels and figures
        /// ClubScoreboard writes from ClubScoreboardFigures.Compose. The figures text is never
        /// auto-sized: every size, place and line spacing in it is set by its tags in metres
        /// (ClubScoreboardLayout), which only hold at the TMP's own scale. BaselineLeft puts its
        /// first baseline at its rect's middle, which ClubScoreboardBoard centres on the first
        /// band's label baseline.
        static TextMeshPro Scoreboard(ClubParts p, Transform parent, TMP_FontAsset font, int layer, StringBuilder log,
                                      out TextMeshPro header)
        {
            CheckFont(font, log);
            Color cream = ColorUtility.TryParseHtmlString(ClubScoreboardFigures.Cream, out Color c) ? c : Color.white;
            header = Label("Club Scoreboard Header", parent, p.ScoreHeader, VenueRules.ClubDefaultName.ToUpperInvariant(),
                           ClubScoreboardLayout.HeaderSize, TextAlignmentOptions.Center, cream, font, layer);
            header.fontStyle = FontStyles.Bold;
            TextMeshPro figures = Label("Club Scoreboard", parent, p.Figures, ClubScoreboardFigures.Compose(default),
                                        ClubScoreboardLayout.LabelSize, TextAlignmentOptions.BaselineLeft, cream, font, layer);
            figures.enableAutoSizing = false;
            figures.fontSize = ClubScoreboardLayout.LabelSize;
            figures.margin = Vector4.zero;
            figures.lineSpacing = figures.paragraphSpacing = figures.characterSpacing = figures.wordSpacing = 0f;
            log.AppendLine("scoreboard: " + ClubScoreboardLayout.Fields.Length + " figures, digits " +
                           (ClubScoreboardLayout.Rows[0].digitSize * 0.1f * ClubScoreboardLayout.CapRatio).ToString("0.00") + " m and " +
                           (ClubScoreboardLayout.Rows[1].digitSize * 0.1f * ClubScoreboardLayout.CapRatio).ToString("0.00") + " m tall");
            return figures;
        }

        /// The board centres each digit on its tin by LiberationSans SDF's cap height
        /// (ClubScoreboardLayout.CapRatio); another font sits its digits higher or lower on the tins.
        static void CheckFont(TMP_FontAsset font, StringBuilder log)
        {
            if (font == null) { log.AppendLine("WARNING no TMP font: the scoreboard's digits may sit off their tins"); return; }
            float cap = font.faceInfo.capLine / font.faceInfo.pointSize;
            if (Mathf.Abs(cap - ClubScoreboardLayout.CapRatio) > 0.02f)
                log.AppendLine("WARNING font " + font.name + " cap height " + cap.ToString("0.000") + " em, the board is laid out for " +
                               ClubScoreboardLayout.CapRatio.ToString("0.000") + ": digits will sit off centre on their tins");
        }
    }
}
