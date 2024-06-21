using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(TransformSystemGroup), OrderLast = true)]
partial struct CopyEntityLocalTransformAsLtWSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new CopyEntityLocalTransformAsLtWJob
        {
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct CopyEntityLocalTransformAsLtWJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<LocalTransform> LocalTransformLookup;
        
        void Execute(ref LocalToWorld ltw, in CopyEntityLocalTransformAsLtW copyEntityTransform)
        {
            if (LocalTransformLookup.TryGetComponent(copyEntityTransform.TargetEntity,
                    out LocalTransform targetLocalTransform))
            {
                ltw.Value = targetLocalTransform.ToMatrix();
            }
        }
    }
}
