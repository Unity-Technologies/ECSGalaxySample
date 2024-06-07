using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(BeginSimulationMainThreadGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct LaserSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        LaserJob job = new LaserJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct LaserJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        private int _chunkIndex;

        public void Execute(Entity entity, ref Laser laser, ref PostTransformMatrix postTransformMatrix)
        {
            laser.LifetimeCounter -= DeltaTime;

            if (laser.HasExistedOneFrame == 1)
            {
                // Scaling
                float lifetimeRatio = math.saturate(laser.LifetimeCounter / laser.MaxLifetime);
                float originalScaleZ = postTransformMatrix.Value.Scale().z;
                postTransformMatrix.Value = float4x4.Scale(lifetimeRatio, lifetimeRatio, originalScaleZ);

                if (laser.LifetimeCounter <= 0f)
                {
                    ECB.DestroyEntity(_chunkIndex, entity);
                }
            }

            laser.HasExistedOneFrame = 1;
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            _chunkIndex = unfilteredChunkIndex;
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }
}
