using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    [RequireComponent(typeof(ShipAuthoring))]
    public class TraderAuthoring : MonoBehaviour
    {
        public TraderDataObject TraderData;
        
        private class Baker : Baker<TraderAuthoring>
        {
            public override void Bake(TraderAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Trader
                {
                    TraderData = BlobAuthoringUtility.BakeToBlob(this, authoring.TraderData),
                    FindTradeRouteAttempts = 0,
                });
                AddComponent(entity, new ExecuteTrade());
                SetComponentEnabled<ExecuteTrade>(entity, false);
            }
        }
    }
}