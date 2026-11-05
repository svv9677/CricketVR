using System.Collections;
using UnityEngine;

public class Stump : MonoBehaviour
{
    [SerializeField]
    protected AudioClip audioClip;
    private Rigidbody ballRigidBody;

    private Vector3 prevPos;
    private Quaternion prevRot;
    private Vector3 homePos;
    private Quaternion homeRot;

    /// True once the stump has been knocked off its mark (it goes back on Stumps.Reset).
    public bool MovedSinceReset =>
        Vector3.Distance(homePos, transform.position) > 0.01f || Quaternion.Angle(homeRot, transform.rotation) > 2f;

    /// One of the striker's stumps (x > 0): the bowler's end is at -x, in the match and in the nets
    /// (BallLedger reads a bowled ball's end the same way). From where it stands, not Main's
    /// theStumps: Main.Instance may not be set yet when this Start runs.
    public bool StrikersEnd => homePos.x > 0f;

    public void Start()
    {
        prevPos = transform.position;
        prevRot = transform.rotation;
        homePos = prevPos;
        homeRot = prevRot;
    }

    private void Update()
    {
        // Check if stump moved over the last frame
        if (Vector3.Distance(prevPos, transform.position) > 0.01f || Quaternion.Angle(prevRot, transform.rotation) > 0.01f)
        {
            Hit();
        }
    }

    private void LateUpdate()
    {
        prevPos = transform.position;
        prevRot = transform.rotation;
    }

    public void Hit()
    {
        Main inst = Main.Instance;
        // Only the striker's stumps can bowl him (StumpRules): a straight drive onto the bowler's
        // end, or a knock from whoever stands there, is not a wicket.
        if (!StumpRules.CountsAsBowled(StrikersEnd, inst.gameState))
            return;
        if (inst.gameState == eGameState.InGame_DeliverBallLoop ||
            inst.gameState == eGameState.InGame_BallHit ||
            inst.gameState == eGameState.InGame_BallHitLoop)
        {
            if (ballRigidBody == null)
                ballRigidBody = inst.theBallRigidBody;

            // dampen the velocity on the ball
            float mag = ballRigidBody.linearVelocity.magnitude;
            ballRigidBody.linearVelocity *= Random.Range(2f, 5f) / mag;

            StartCoroutine(PlayBallHitStumpSoundDelayed(transform.position));

            inst.gameState = eGameState.InGame_Bowled;
        }
    }

    public IEnumerator PlayBallHitStumpSoundDelayed(Vector3 point)
    {
        yield return new WaitForSeconds(0.3f);
        GameAudio.PlayAt(audioClip, point, GameAudio.Channel.Stumps);
    }
}
