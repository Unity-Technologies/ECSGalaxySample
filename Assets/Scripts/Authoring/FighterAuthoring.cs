using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    [RequireComponent(typeof(ShipAuthoring))]
    public class FighterAuthoring : MonoBehaviour
    {
        public FighterDataObject FighterData;
        
        private class Baker : Baker<FighterAuthoring>
        {
            public override void Bake(FighterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Fighter
                {
                    FighterData = BlobAuthoringUtility.BakeToBlob(this, authoring.FighterData),
                    
                    DamageMultiplier = 1f,
                });
                AddComponent(entity, new ExecuteAttack());
                SetComponentEnabled<ExecuteAttack>(entity, false);
            }
        }
    }
}