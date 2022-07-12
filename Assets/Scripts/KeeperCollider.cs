using System.Collections;
using UnityEngine;

public class KeeperCollider : MonoBehaviour
{
    public static bool canCheck;

    public void OnTriggerExit(Collider other)
    {
        
        if (other.gameObject.name == "Ball" && Main.Instance.gameState == eGameState.InGame_DeliverBallLoop)
        {
            Main.Instance.resetDelay = 2f;
            if(Main.Instance.gameState == eGameState.InGame_DeliverBallLoop)
            {
               
                if (gameObject.name.Contains("WideCollider"))
                {
                    Main.Instance.theBallScript.wide = true;
                }
                else
                {
                    StartCoroutine(CheckBallMissed());
                }
                
            }
                
            else
                Debug.LogError("GAMESTATE ERROR!! cannot set to 'InGame_BallMissed', state is: " + Main.Instance.gameState.ToString());
        }
    }

    public IEnumerator CheckBallMissed()
    {
        yield return new WaitUntil(() => canCheck == true);
        if (Main.Instance.gameState == eGameState.InGame_DeliverBallLoop)
        {
            Main.Instance.gameState = eGameState.InGame_BallMissed;
        }

    }
}

