using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BallSpeed : MonoBehaviour
{
    public static BallSpeed Instance;

    private TMP_Text myText;

    // Start is called before the first frame update
    void Start()
    {
        if (BallSpeed.Instance == null)
            BallSpeed.Instance = this;

        myText = GetComponent<TMP_Text>();
    }

    public IEnumerator updateBallSpeed()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // What it was as well as how fast, so a player learns the bowler's variations.
        BowlingParams cfg = Main.Instance.currentBowlingConfig;
        string what = cfg != null ? BowlerRepertoire.Name(cfg.delivery, cfg.kind) + "\n" : "";
        setText(what + Kmh(Main.Instance.theBallRigidBody.linearVelocity.magnitude) + " km/h");
    }

    /// The stadium display: delivery speed, then bat and exit speed once the ball is hit.
    public void updateBatAndFinalSpeed(float batSpeed, float finalSpeed)
    {
        setText(myText.text + "\nBat " + Kmh(batSpeed) + " km/h\nExit " + Kmh(finalSpeed) + " km/h");
    }

    private static string Kmh(float metresPerSecond) => Mathf.RoundToInt(metresPerSecond * UIFormat.KmhPerMps).ToString();

    public void setText(string text)
    {
        myText.text = text;
    }
}
