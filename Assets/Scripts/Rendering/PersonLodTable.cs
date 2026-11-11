using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Each person mesh (fielders, bowler, keeper, umpire, team-mates) and its lighter versions for
/// further away, baked in the editor by PersonLodBaker (Tools > CricketVR > Bake Person LODs) into
/// Resources/PersonLodTable.asset, the light meshes stored inside it. PersonLod swaps between them.
/// </summary>
public class PersonLodTable : ScriptableObject
{
    public const string ResourceName = "PersonLodTable";

    public Mesh[] full = new Mesh[0];
    /// Beyond PersonLod.MidBeyond.
    public Mesh[] mid = new Mesh[0];
    /// Beyond PersonLod.FarBeyond.
    public Mesh[] far = new Mesh[0];

    private Dictionary<Mesh, int> index;

    /// The set `mesh` belongs to (as any of its versions), or -1.
    public int Find(Mesh mesh)
    {
        if (mesh == null)
            return -1;
        if (index == null)
        {
            index = new Dictionary<Mesh, int>();
            int n = Mathf.Min(full.Length, Mathf.Min(mid.Length, far.Length));
            for (int i = 0; i < n; i++)
            {
                if (full[i] == null || mid[i] == null || far[i] == null)
                    continue;
                index[full[i]] = i;
                index[mid[i]] = i;
                index[far[i]] = i;
            }
        }
        return index.TryGetValue(mesh, out int at) ? at : -1;
    }
}
