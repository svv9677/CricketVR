using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stump : MonoBehaviour
{
    [SerializeField]
    protected AudioClip audioClip;

    private Collider myCollider;
    private Rigidbody ballRigidBody;

    private Main inst;

    public void Start()
    {
        myCollider = GetComponent<Collider>();
        inst = Main.Instance;
    }



    //public void OnCollisionExit(Collision collision)
    //{
    //    if (collision.gameObject.name == "Ball")
    //    {
    //        if (inst.gameState == eGameState.InGame_DeliverBallLoop ||
    //            inst.gameState == eGameState.InGame_BallHit ||
    //            inst.gameState == eGameState.InGame_BallHitLoop ||
    //            inst.gameState == eGameState.InGame_BallMissed ||
    //            inst.gameState == eGameState.InGame_BallMissedLoop)
    //        {
    //            KeeperCollider.canCheck = false;
    //            if (ballRigidBody == null)
    //                ballRigidBody = collision.gameObject.GetComponent<Rigidbody>();

    //            StartCoroutine(PlayBallHitStumpSoundDelayed(transform.position, new Vector3(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z)));
    //        }
    //    }
    //}

    //public IEnumerator PlayBallHitStumpSoundDelayed(Vector3 point, Vector3 currentRot)
    //{
    //    for (int i = 0; i < 60; i++)
    //    {
    //        yield return new WaitForFixedUpdate();
    //    }
        
    //    if (Vector3.Distance(transform.position, point) > 0f)
    //    {
    //        if (inst.gameState == eGameState.InGame_DeliverBallLoop ||
    //            inst.gameState == eGameState.InGame_BallHit ||
    //            inst.gameState == eGameState.InGame_BallHitLoop ||
    //            inst.gameState == eGameState.InGame_BallMissed ||
    //            inst.gameState == eGameState.InGame_BallMissedLoop)
    //        {
    //            // dampen the velocity on the ball
    //            float mag = ballRigidBody.velocity.magnitude;
    //            ballRigidBody.velocity *= (Random.Range(2f, 5f) / mag);
    //            AudioSource.PlayClipAtPoint(audioClip, point);
    //            inst.gameState = eGameState.InGame_Bowled;
    //        }
                
    //    }

    //    KeeperCollider.canCheck = true;

    //    Debug.Log("Previous: " + point);
    //    Debug.Log("Current: " + transform.position);
    //}

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



