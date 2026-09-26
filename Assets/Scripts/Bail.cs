using UnityEngine;

/// <summary>
/// One bail. It sits in the grooves on top of the stumps, held kinematic so it cannot jitter off
/// on its own, and is knocked loose - made dynamic and flicked - when a stump under it moves or the
/// ball touches it. A bail dislodged by the ball is out bowled, the same as a stump going.
///
/// The stumps are watched by their transforms rather than through the Stump script, because the
/// bowler's-end wicket has no Stump scripts (it is not played at).
/// </summary>
public class Bail : MonoBehaviour
{
    /// The two stumps this bail rests on (the first is the middle stump).
    public Stump[] restsOn;

    private Rigidbody body;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Transform[] stumps;
    private Vector3[] stumpHomes;
    private Quaternion[] stumpRotations;
    private bool loose;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        body.isKinematic = true;

        // The bail sits in the stump grooves, so its collider overlaps the stumps (and the other
        // bail). Left to itself PhysX would push the stumps apart on the first step.
        Collider mine = GetComponent<Collider>();
        foreach (Collider other in transform.parent.GetComponentsInChildren<Collider>())
        {
            if (other != mine)
                Physics.IgnoreCollision(mine, other);
        }

        stumps = new Transform[restsOn != null ? restsOn.Length : 0];
        stumpHomes = new Vector3[stumps.Length];
        stumpRotations = new Quaternion[stumps.Length];
        for (int i = 0; i < stumps.Length; i++)
        {
            stumps[i] = restsOn[i] != null ? restsOn[i].transform : null;
            if (stumps[i] != null)
            {
                stumpHomes[i] = stumps[i].position;
                stumpRotations[i] = stumps[i].rotation;
            }
        }
        if (stumps.Length == 0 || stumps[0] == null)
            FindStumpsByPosition();
    }

    /// Fallback when the Stump scripts are missing: the two stump bodies nearest this bail.
    private void FindStumpsByPosition()
    {
        stumps = new Transform[2];
        stumpHomes = new Vector3[2];
        stumpRotations = new Quaternion[2];
        float best = float.MaxValue, second = float.MaxValue;
        foreach (Transform sibling in transform.parent)
        {
            if (sibling == transform || sibling.GetComponent<Bail>() != null || sibling.GetComponent<Rigidbody>() == null)
                continue;
            float d = Mathf.Abs(sibling.position.z - transform.position.z);
            if (d < best) { stumps[1] = stumps[0]; second = best; stumps[0] = sibling; best = d; }
            else if (d < second) { stumps[1] = sibling; second = d; }
        }
        for (int i = 0; i < 2; i++)
        {
            if (stumps[i] == null) continue;
            stumpHomes[i] = stumps[i].position;
            stumpRotations[i] = stumps[i].rotation;
        }
    }

    private void Update()
    {
        if (loose)
            return;
        for (int i = 0; i < stumps.Length; i++)
        {
            Transform s = stumps[i];
            if (s != null && (Vector3.Distance(s.position, stumpHomes[i]) > 0.01f || Quaternion.Angle(s.rotation, stumpRotations[i]) > 2f))
            {
                KnockOff(Vector3.up * 0.4f);
                return;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (loose || collision.gameObject.name != "Ball")
            return;
        KnockOff(collision.relativeVelocity * -0.05f + Vector3.up * 0.6f);
        if (restsOn != null && restsOn.Length > 0 && restsOn[0] != null)
            restsOn[0].Hit();
    }

    private void KnockOff(Vector3 impulse)
    {
        loose = true;
        body.isKinematic = false;
        body.AddForce(impulse, ForceMode.Impulse);
        body.AddTorque(Random.onUnitSphere * 0.002f, ForceMode.Impulse);
    }

    public void ResetBail()
    {
        loose = false;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        body.position = homePosition;
        body.rotation = homeRotation;
        transform.SetPositionAndRotation(homePosition, homeRotation);
    }
}
