using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fielder : MonoBehaviour
{
    [SerializeField]
    protected GameObject theObject;
    [SerializeField]
    protected bool isKeeper;

    [HideInInspector]
    public Material JerseyMaterial;
    [HideInInspector]
    public Vector3 StartPosition;

    private bool holdBall;
    private eFielderState fielderState;

    // Start is called before the first frame update
    void Start()
    {
        if (theObject != null)
        {
            Material[] mats = theObject.GetComponent<Renderer>().materials;
            foreach (Material mat in mats)
            {
                if (mat.name.Contains("Material.001"))
                {
                    JerseyMaterial = mat;
                    break;
                }
            }
        }
        StartPosition = this.transform.position;
        Main.Instance.onGameStateChanged += HandleGameState;
        holdBall = false;
        fielderState = eFielderState.Idle;

        if (this.gameObject.name.Contains("Keeper"))
            isKeeper = true;
    }

    public void OnDestroy()
    {
        Main.Instance.onGameStateChanged -= HandleGameState;
    }

    //private void OnDrawGizmos()
    //{
    //    Gizmos.DrawSphere(transform.position + (transform.forward * 0.85f) + transform.up, 0.365f);
    //}

    public void HandleGameState()
    {
        Main inst = Main.Instance;
        if ((inst.gameState == eGameState.InGame_BallFielded ||
            inst.gameState == eGameState.InGame_BallFielded_Loop ||
            inst.gameState == eGameState.InGame_ResetToReady ||
            inst.gameState == eGameState.InGame_ResetToReadyLoop) &&
           inst.currentFielderName == transform.name)
        {
            holdBall = true;
        }
        else
        {
            holdBall = false;
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Ball" && !holdBall)
        {
            Main inst = Main.Instance;
            if (inst.gameState != eGameState.InGame_BallHitLoop && inst.gameState != eGameState.InGame_DeliverBallLoop)
            {
                Debug.LogWarning("CAUTION: " + gameObject.name + " collided with ball, but game state was " + inst.gameState.ToString());
                return;
            }
            if (isKeeper)
            {
                // batsman missed the ball
                if (inst.gameState == eGameState.InGame_DeliverBallLoop)
                    inst.gameState = eGameState.InGame_BallMissed;
                if (inst.gameState == eGameState.InGame_BallHitLoop)
                {
                    inst.currentFielderName = transform.name;
                    inst.gameState = eGameState.InGame_BallFielded;
                }
            }
            else
            {
                inst.currentFielderName = transform.name;
                inst.gameState = eGameState.InGame_BallFielded;
            }

            // pause the particles
            //inst.theBallScript.myParticles.Stop();   PARTICLE
            //inst.theBallScript.myParticles.Clear();
            inst.theBallScript.myParticles.Clear();
            // disable physics
            inst.theBallRigidBody.isKinematic = true;
            // make the ball static
            inst.theBallRigidBody.velocity = Vector3.zero;

        }
    }

    public void SetState(object state)
    {
        fielderState = (eFielderState)state;
    }

    public void LateUpdate()
    {
        Main inst = Main.Instance;
        if (holdBall)
            inst.theBall.transform.position = transform.position + (transform.forward * 0.35f) + transform.up;

        if (inst.gameState == eGameState.InGame_SelectDelivery ||
           inst.gameState == eGameState.InGame_SelectDeliveryLoop ||
           inst.gameState == eGameState.InGame_DeliverBall ||
           inst.gameState == eGameState.InGame_Ready ||
           inst.gameState == eGameState.InGame_ResetToReadyLoop)
        {
            if (fielderState != eFielderState.Idle && fielderState != eFielderState.ToIdle)
            {
                fielderState = eFielderState.ToIdle;

                Hashtable args = new Hashtable();
                Vector3 pos = inst.theStumps.transform.position;
                pos.y = transform.position.y;
                args["position"] = StartPosition;
                args["speed"] = inst.fielderSpeed;
                args["looktarget"] = pos;
                args["looktime"] = 1f;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.Idle;
                iTween.MoveTo(this.gameObject, args);
            }
        }

        if (inst.gameState == eGameState.InGame_DeliverBallLoop)
        {
            if (fielderState != eFielderState.TakingStart && fielderState != eFielderState.ToTakingStart &&
                !isKeeper)
            {
                fielderState = eFielderState.ToTakingStart;

                Hashtable args = new Hashtable();
                Vector3 pos = transform.position + (transform.forward * 2f);
                pos.y = transform.position.y;
                args["position"] = pos;
                args["speed"] = inst.fielderSpeed;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.TakingStart;
                iTween.MoveTo(this.gameObject, args);
            }
        }

        if (inst.gameState == eGameState.InGame_BallHit ||
           inst.gameState == eGameState.InGame_BallHitLoop)
        {
            if (fielderState != eFielderState.Fielded &&
                !isKeeper)
            {
                fielderState = eFielderState.MovingTowardsBall;

                Hashtable args = new Hashtable();
                Vector3 pos = inst.theBall.transform.position;
                pos.y = transform.position.y;
                // MoveUpdate does not take speed, it only takes time,
                //  so we need to convert our speed into time
                float t = (pos - transform.position).magnitude / inst.fielderSpeed;
                t = Mathf.Max(t, 1f);
                args["position"] = pos;
                args["looktarget"] = pos;
                args["time"] = t;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.Fielded;
                iTween.MoveUpdate(this.gameObject, args);
            }
        }
    }
}