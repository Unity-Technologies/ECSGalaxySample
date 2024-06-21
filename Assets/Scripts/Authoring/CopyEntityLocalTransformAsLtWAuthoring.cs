using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

class CopyRootLocalTransformAsLtWAuthoring : MonoBehaviour
{ }

class CopyRootLocalTransformAsLtWBaker : Baker<CopyRootLocalTransformAsLtWAuthoring>
{
    public override void Bake(CopyRootLocalTransformAsLtWAuthoring asLtWAuthoring)
    {
        Entity entity = GetEntity(asLtWAuthoring, TransformUsageFlags.ManualOverride);
        AddComponent(entity, new LocalToWorld { Value = asLtWAuthoring.transform.localToWorldMatrix });
        AddComponent(entity, new CopyEntityLocalTransformAsLtW
        {
            TargetEntity = GetEntity(asLtWAuthoring.transform.root, TransformUsageFlags.None),
        });
    }
}
