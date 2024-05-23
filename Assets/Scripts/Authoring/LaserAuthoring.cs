using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class LaserAuthoring : MonoBehaviour
{
    public float Lifetime = 0.2f;

    class Baker : Baker<LaserAuthoring>
    {
        public override void Bake(LaserAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Laser
            {
                MaxLifetime = authoring.Lifetime,
                LifetimeCounter = authoring.Lifetime,
                HasExistedOneFrame = 0,
            });
        }
    }
}
