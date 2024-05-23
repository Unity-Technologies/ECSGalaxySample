using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct DeathSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VFXExplosionsSingleton>();
        state.RequireForUpdate<VFXThrustersSingleton>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        VFXExplosionsSingleton vfxExplosionSingleton = SystemAPI.GetSingletonRW<VFXExplosionsSingleton>().ValueRW;
        VFXThrustersSingleton vfxThrustersSingleton = SystemAPI.GetSingletonRW<VFXThrustersSingleton>().ValueRW;
        
        BuildingDeathJob buildingDeathJob = new BuildingDeathJob
        {
            ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged),
            ExplosionsManager = vfxExplosionSingleton.Manager,
        };
        state.Dependency = buildingDeathJob.Schedule(state.Dependency);

        ShipDeathJob shipDeathJob = new ShipDeathJob
        {
            ExplosionsManager = vfxExplosionSingleton.Manager,
            ThrustersManager = vfxThrustersSingleton.Manager,
        };
        state.Dependency = shipDeathJob.Schedule(state.Dependency);
        
        FinalizedDeathJob finalizeDeathJob = new FinalizedDeathJob
        {
            ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
        };
        state.Dependency = finalizeDeathJob.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct BuildingDeathJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        public VFXManager<VFXExplosionRequest> ExplosionsManager;
        
        public void Execute(Entity entity, in LocalToWorld ltw, in Building building, in Health health)
        {
            if (health.IsDead)
            {
                ECB.SetComponent(building.MoonEntity, new BuildingReference
                {
                    BuildingEntity = Entity.Null,
                });
            
                // VFX
                {
                    Random random = GameUtilities.GetDeterministicRandom(entity.Index);
                    BuildingData buildingData = building.BuildingData.Value;
                    float explosionSize = random.NextFloat(buildingData.ExplosionScaleRange.x,
                        buildingData.ExplosionScaleRange.y);
                    ExplosionsManager.AddRequest(new VFXExplosionRequest
                    {
                        Position = ltw.Position,
                        Scale = explosionSize,
                    });
                }
            }
        }
    }

    [BurstCompile]
    public partial struct ShipDeathJob : IJobEntity
    {
        public VFXManagerParented<VFXThrusterData> ThrustersManager;
        public VFXManager<VFXExplosionRequest> ExplosionsManager;
        
        public void Execute(Entity entity, in LocalTransform transform, in Ship ship, in Health health)
        {
            if (health.IsDead)
            {
                // VFX
                {
                    // Explosion
                    Random random = GameUtilities.GetDeterministicRandom(entity.Index);
                    ShipData shipData = ship.ShipData.Value;
                    float explosionSize =
                        random.NextFloat(shipData.ExplosionScaleRange.x, shipData.ExplosionScaleRange.y);
                    ExplosionsManager.AddRequest(new VFXExplosionRequest
                    {
                        Position = transform.Position,
                        Scale = explosionSize,
                    });

                    // Thruster
                    ThrustersManager.Kill(ship.ThrusterVFXIndex);
                }
            }
        }
    }

    [BurstCompile]
    public partial struct FinalizedDeathJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        private int _chunkIndex;
        
        public void Execute(Entity entity, in Health health)
        {
            if (health.IsDead)
            {
                ECB.DestroyEntity(_chunkIndex, entity);
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            _chunkIndex = unfilteredChunkIndex;
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        { }
    }
}