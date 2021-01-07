using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum eFielderState
{
    Idle,
    ToIdle,

    TakingStart,
    ToTakingStart,

    MovingTowardsBall,
    Fielded
}

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
        if(theObject != null)
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

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(transform.position + (transform.forward * 0.85f) + transform.up, 0.365f);
    }

    public void HandleGameState()
    {
        if((Main.Instance.gameState == eGameState.InGame_BallFielded ||
            Main.Instance.gameState == eGameState.InGame_BallFielded_Loop ||
            Main.Instance.gameState == eGameState.InGame_ResetToReady ||
            Main.Instance.gameState == eGameState.InGame_ResetToReadyLoop) &&
           Main.Instance.currentFielderName == transform.name)
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
        if(collision.gameObject.name == "Ball" && !holdBall)
        {
            if (Main.Instance.gameState != eGameState.InGame_BallHitLoop)
            {
                Debug.Log("CAUTION: " + gameObject.name + " collided with ball, but game state was " + Main.Instance.gameState.ToString());
                return;
            }

            // pause the particles
            Main.Instance.theBallScript.myParticles.Stop();
            Main.Instance.theBallScript.myParticles.Clear();
            // disable physics
            Main.Instance.theBallRigidBody.isKinematic = true;
            // make the ball static
            Main.Instance.theBallRigidBody.velocity = Vector3.zero;

            Main.Instance.currentFielderName = transform.name;
            Main.Instance.gameState = eGameState.InGame_BallFielded;
        }
    }

    public void SetState(object state)
    {
        fielderState = (eFielderState)state;
    }

    public void LateUpdate()
    {
        if(holdBall)
            Main.Instance.theBall.transform.position = transform.position + (transform.forward * 0.35f) + transform.up;

        if(Main.Instance.gameState == eGameState.InGame_SelectDelivery ||
           Main.Instance.gameState == eGameState.InGame_SelectDeliveryLoop ||
           Main.Instance.gameState == eGameState.InGame_DeliverBall ||
           Main.Instance.gameState == eGameState.InGame_Ready ||
           Main.Instance.gameState == eGameState.InGame_ResetToReadyLoop)
        {
            if (fielderState != eFielderState.Idle && fielderState != eFielderState.ToIdle)
            {
                fielderState = eFielderState.ToIdle;

                Hashtable args = new Hashtable();
                Vector3 pos = Main.Instance.theStumps.transform.position;
                pos.y = transform.position.y;
                args["position"] = StartPosition;
                args["speed"] = Main.Instance.FielderSpeed;
                args["looktarget"] = pos;
                args["looktime"] = 1f;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.Idle;
                iTween.MoveTo(this.gameObject, args);
            }
        }

        if (Main.Instance.gameState == eGameState.InGame_DeliverBallLoop)
        {
            if(fielderState != eFielderState.TakingStart && fielderState != eFielderState.ToTakingStart &&
                !isKeeper)
            {
                fielderState = eFielderState.ToTakingStart;

                Hashtable args = new Hashtable();
                Vector3 pos = transform.position + (transform.forward * 2f);
                pos.y = transform.position.y;
                args["position"] = pos;
                args["speed"] = Main.Instance.FielderSpeed;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.TakingStart;
                iTween.MoveTo(this.gameObject, args);
            }
        }

        if(Main.Instance.gameState == eGameState.InGame_BallHit ||
           Main.Instance.gameState == eGameState.InGame_BallHitLoop)
        {
            if(fielderState != eFielderState.Fielded &&
                !isKeeper)
            {
                fielderState = eFielderState.MovingTowardsBall;

                Hashtable args = new Hashtable();
                Vector3 pos = Main.Instance.theBall.transform.position;
                pos.y = transform.position.y;
                // MoveUpdate does not take speed, it only takes time,
                //  so we need to convert our speed into time
                float t = (pos - transform.position).magnitude / Main.Instance.FielderSpeed;
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
