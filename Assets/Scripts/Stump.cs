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
        AudioSource.PlayClipAtPoint(audioClip, point);
    }
}
