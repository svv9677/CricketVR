using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShotDistance : MonoBehaviour
{
    public static ShotDistance Instance;

    private TMP_Text myText;

    // Start is called before the first frame update
    void Start()
    {
        if (ShotDistance.Instance == null)
            ShotDistance.Instance = this;

        myText = GetComponent<TMP_Text>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Main inst = Main.Instance;
        if (inst.gameState == eGameState.InGame_BallHitLoop ||
            inst.gameState == eGameState.InGame_BallPastBoundaryLoop ||
            inst.gameState == eGameState.InGame_ResetToReadyLoop ||
            inst.gameState == eGameState.InGame_Ready)
        {
            Vector3 ballPos = inst.theBall.transform.position;
            Vector3 batPos = inst.theBat.transform.position;
            ballPos.y = 0f;
            batPos.y = 0f;

            string text = (Mathf.Round(Vector3.Distance(ballPos, batPos) * 10f) / 10f) + " m";
            setText(text);
        }
        if (inst.gameState == eGameState.InGame_SelectDeliveryLoop)
        {
            setText("0.0 m");
        }
    }

    public void calculateDistance(Vector3 startPos, Vector3 vel)
    {
        ////yield return new WaitForSeconds(1f);
        ////float y = 0f - Main.Instance.theBall.transform.position.y; // the -1f is to make sure there is no error if this calculation is done right when the ball hits the ground.
        ////float v = Main.Instance.theBallRigidBody.velocity.y;
        //float y = 0f - startPos.y;
        //float v = vel.y;
        //float t1 = (-v + Mathf.Sqrt(Mathf.Pow(v, 2f) - (19.6f * y))) / -9.81f;
        //float t2 = (-v - Mathf.Sqrt(Mathf.Pow(v, 2f) - (19.6f * y))) / -9.81f;
        //float time = Mathf.Max(t1, t2); // this is the time it takes for the ball to hit the ground.
        ////float posx = (Main.Instance.theBallRigidBody.velocity.x * time) + Main.Instance.theBall.transform.position.x;
        ////float posz = (Main.Instance.theBallRigidBody.velocity.z * time) + Main.Instance.theBall.transform.position.z;
        //float posx = (vel.x * time);
        //float posz = (vel.z * time);
        ////float distance = Vector2.Distance(new Vector2(posx, posz), new Vector2(startPos.x, startPos.z));
        //float distance = new Vector2(posx, posz).magnitude;
        ////distance += Vector2.Distance(new Vector2(Main.Instance.theBat.transform.position.x, Main.Instance.theBat.transform.position.z), new Vector2(Main.Instance.theBall.transform.position.x, Main.Instance.theBall.transform.position.z));
        //string text = (Mathf.Round(distance * 10f) / 10f) + " m";
        //if (text != "NaN m")
        //    setText(text);
    }

    public void setText(string text)
    {
        myText.text = text;
    }
}
