using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    public class ActorTypeAuthoring : MonoBehaviour
    {
        public bool IsTargetableByAttacks = true;
        
        class Baker : Baker<ActorTypeAuthoring>
        {
            public override void Bake(ActorTypeAuthoring authoring)
            {
                byte actorType = 0;
                if (authoring.gameObject.GetComponent<FighterAuthoring>() != null)
                {
                    actorType = ActorType.FighterType;
                }
                else if (authoring.gameObject.GetComponent<WorkerAuthoring>() != null)
                {
                    actorType = ActorType.WorkerType;
                }
                else if (authoring.gameObject.GetComponent<TraderAuthoring>() != null)
                {
                    actorType = ActorType.TraderType;
                }
                else if (authoring.gameObject.GetComponent<FactoryAuthoring>() != null)
                {
                    actorType = ActorType.FactoryType;
                }
                else if (authoring.gameObject.GetComponent<TurretAuthoring>() != null)
                {
                    actorType = ActorType.TurretType;
                }
                else if (authoring.gameObject.GetComponent<ResearchAuthoring>() != null)
                {
                    actorType = ActorType.ResearchType;
                }
                
                Entity entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new ActorType
                {
                    Type = actorType,
                });
                if (authoring.IsTargetableByAttacks)
                {
                    AddComponent(entity, new Targetable());
                    AddComponent(entity, new SpatialDatabaseCellIndex());
                }
            }
        }
    }
}