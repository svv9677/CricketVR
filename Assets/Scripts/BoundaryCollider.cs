using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundaryCollider : MonoBehaviour
{
    public void OnTriggerExit(Collider other)
    {
        if(other.gameObject.name == "Ball" && Main.Instance.gameState == eGameState.InGame_BallHitLoop)
        {
            Main.Instance.ResetDelay = 4f;
            if(Main.Instance.gameState == eGameState.InGame_BallHitLoop)
                Main.Instance.gameState = eGameState.InGame_BallPastBoundary;
            else
                Debug.LogError("GAMESTATE ERROR!! cannot set to 'InGame_BallPastBoundary', state is: " + Main.Instance.gameState.ToString());
        }
    }
}
