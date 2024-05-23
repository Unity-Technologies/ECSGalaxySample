using UnityEngine;
using UnityEngine.VFX;

public class ManagedResources : MonoBehaviour
{
    public VisualEffect HitSparksGraph;
    public VisualEffect ExplosionsGraph;
    public VisualEffect ThrustersGraph;

    public void Awake()
    {
        VFXReferences.HitSparksGraph = HitSparksGraph;
        VFXReferences.ExplosionsGraph = ExplosionsGraph;
        VFXReferences.ThrustersGraph = ThrustersGraph;
    }
}
