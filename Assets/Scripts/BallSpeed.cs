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

        float km = Main.Instance.theBallRigidBody.velocity.magnitude * 60f * 60f / 1000f;
        float miles = km * 0.62f;

        km = Mathf.Round(km * 10f) / 10f;
        miles = Mathf.Round(miles * 10f) / 10f;

        //setText(km + " kph\n" + miles + " mph");
        setText("Release: " + km + " kph");
    }

    public void updateBatAndFinalSpeed(float batSpeed, float finalSpeed)
    {
        float km = batSpeed * 60f * 60f / 1000f;
        km = Mathf.Round(km * 10f) / 10f;
        float km2 = finalSpeed * 60f * 60f / 1000f;
        km2 = Mathf.Round(km2 * 10f) / 10f;
        setText(myText.text + "\nBat: " + km.ToString() + " kph\nBounce: " + km2 + " kph");
    }

    public void setText(string text)
    {
        myText.text = text;
    }
}
