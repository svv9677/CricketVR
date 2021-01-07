using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeeperCollider : MonoBehaviour
{
    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Ball" && Main.Instance.gameState == eGameState.InGame_DeliverBallLoop)
        {
            Main.Instance.ResetDelay = 2f;
            if(Main.Instance.gameState == eGameState.InGame_DeliverBallLoop)
                Main.Instance.gameState = eGameState.InGame_BallMissed;
            else
                Debug.LogError("GAMESTATE ERROR!! cannot set to 'InGame_BallMissed', state is: " + Main.Instance.gameState.ToString());
        }
    }
}
