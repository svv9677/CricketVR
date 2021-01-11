using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stump : MonoBehaviour
{
    [SerializeField]
    protected AudioClip audioClip;

    private Collider myCollider;
    private Rigidbody ballRigidBody;

    public void Start()
    {
        myCollider = GetComponent<Collider>();
    }

    public void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.name == "Ball")
        {
            Main inst = Main.Instance;
            if (inst.gameState == eGameState.InGame_DeliverBallLoop ||
                inst.gameState == eGameState.InGame_BallHit ||
                inst.gameState == eGameState.InGame_BallHitLoop)
            {
                if (ballRigidBody == null)
                    ballRigidBody = collision.gameObject.GetComponent<Rigidbody>();

                // dampen the velocity on the ball
                float mag = ballRigidBody.velocity.magnitude;
                ballRigidBody.velocity *= (Random.Range(2f, 5f) / mag);

                StartCoroutine(PlayBallHitStumpSoundDelayed(transform.position));

                inst.gameState = eGameState.InGame_Bowled;
            }
        }
    }

    public IEnumerator PlayBallHitStumpSoundDelayed(Vector3 point)
    {
        yield return new WaitForSeconds(0.3f);

        AudioSource.PlayClipAtPoint(audioClip, point);
    }
}
