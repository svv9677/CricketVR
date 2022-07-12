using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WideCollider : MonoBehaviour
{

    [SerializeField] private Transform player;
    [SerializeField] private bool leg;
    private BoxCollider boxCollider;

    // Start is called before the first frame update
    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        if (leg)
        {
            if (player.position.z < -0.12f)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, player.position.z - (boxCollider.size.z / 2) - 0.18f);
            }
            else
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, -(boxCollider.size.z / 2) - 0.3f);
            }
        }
        else
        {
            if (player.position.z > 0.12f)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, player.position.z + (boxCollider.size.z / 2) + 1.08f);
            }
            else
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, (boxCollider.size.z / 2) + 1.2f);
            }
        }
        
    }
}
