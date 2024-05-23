using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct FinishInitializeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FinishInitializeJob job = new FinishInitializeJob();
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Initialize))]
    public partial struct FinishInitializeJob : IJobEntity
    {
        private void Execute(EnabledRefRW<Initialize> initialized)
        {
            initialized.ValueRW = false;
        }
    }
}
