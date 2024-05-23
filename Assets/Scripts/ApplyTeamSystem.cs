using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[BurstCompile]
[UpdateAfter(typeof(BeginSimulationMainThreadGroup))]
public partial struct ApplyTeamSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Config>();
        state.RequireForUpdate<TeamManagerReference>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Config config = SystemAPI.GetSingleton<Config>();
        
        ApplyTeamSimpleJob applyTeamSimpleJob = new ApplyTeamSimpleJob
        {
            NeutralTeamColor = config.NeutralTeamColor,
            TeamManagerReferencesEntity = SystemAPI.GetSingletonEntity<TeamManagerReference>(),
            TeamManagerReferenceBufferLookup = SystemAPI.GetBufferLookup<TeamManagerReference>(true),
            TeamManagerLookup = SystemAPI.GetComponentLookup<TeamManager>(true),
        };
        state.Dependency = applyTeamSimpleJob.Schedule(state.Dependency);
        
        ApplyTeamLinkedJob applyTeamLinkedJob = new ApplyTeamLinkedJob
        {
            NeutralTeamColor = config.NeutralTeamColor,
            TeamManagerReferencesEntity = SystemAPI.GetSingletonEntity<TeamManagerReference>(),
            TeamManagerReferenceBufferLookup = SystemAPI.GetBufferLookup<TeamManagerReference>(true),
            TeamManagerLookup = SystemAPI.GetComponentLookup<TeamManager>(true),
            BaseColorLookup = SystemAPI.GetComponentLookup<URPMaterialPropertyBaseColor>(false),
            TeamLookup = SystemAPI.GetComponentLookup<Team>(false),
        };
        state.Dependency = applyTeamLinkedJob.Schedule(state.Dependency);
    }

    [BurstCompile]
    [WithNone(typeof(LinkedEntityGroup))]
    public partial struct ApplyTeamSimpleJob : IJobEntity
    {
        public float4 NeutralTeamColor;
        public Entity TeamManagerReferencesEntity;
        [ReadOnly]
        public ComponentLookup<TeamManager> TeamManagerLookup;
        [ReadOnly]
        public BufferLookup<TeamManagerReference> TeamManagerReferenceBufferLookup;
        
        void Execute(EnabledRefRW<ApplyTeam> applyTeam, ref Team team, ref URPMaterialPropertyBaseColor baseColor)
        {
            if (team.Index >= 0)
            {
                DynamicBuffer<TeamManagerReference> teamMetadataBuffer = TeamManagerReferenceBufferLookup[TeamManagerReferencesEntity];
                Entity managerEntity = teamMetadataBuffer[team.Index].Entity;
                if (TeamManagerLookup.HasComponent(managerEntity))
                {
                    TeamManager teamManager = TeamManagerLookup[managerEntity];
                    baseColor.Value = teamManager.Color;
                    team.ManagerEntity = managerEntity;
                }
            }
            else
            {
                baseColor.Value = NeutralTeamColor;
                team.ManagerEntity = Entity.Null;
            }

            applyTeam.ValueRW = false;
        }
    }

    [BurstCompile]
    [WithAll(typeof(Team))]
    [WithAll(typeof(LinkedEntityGroup))]
    public partial struct ApplyTeamLinkedJob : IJobEntity
    {
        public float4 NeutralTeamColor;
        public Entity TeamManagerReferencesEntity;
        [ReadOnly]
        public BufferLookup<TeamManagerReference> TeamManagerReferenceBufferLookup;
        public ComponentLookup<URPMaterialPropertyBaseColor> BaseColorLookup;
        public ComponentLookup<Team> TeamLookup;
        [ReadOnly]
        public ComponentLookup<TeamManager> TeamManagerLookup;

        void Execute(Entity entity, EnabledRefRW<ApplyTeam> applyTeam, in DynamicBuffer<LinkedEntityGroup> legBuffer)
        {
            Team selfTeam = TeamLookup[entity];
            float4 teamColor = NeutralTeamColor;
            
            if (selfTeam.Index >= 0)
            {
                DynamicBuffer<TeamManagerReference> teamMetadataBuffer = TeamManagerReferenceBufferLookup[TeamManagerReferencesEntity];
                Entity managerEntity = teamMetadataBuffer[selfTeam.Index].Entity;
                if (TeamManagerLookup.HasComponent(managerEntity))
                {
                    TeamManager teamManager = TeamManagerLookup[managerEntity];
                    teamColor = teamManager.Color;
                    selfTeam.ManagerEntity = managerEntity;
                }
            }
            else
            {
                teamColor = NeutralTeamColor;
                selfTeam.ManagerEntity = Entity.Null;
            }
            
            TeamLookup[entity] = selfTeam;
            
            for (int i = 0; i < legBuffer.Length; i++)
            {
                Entity legEntity = legBuffer[i].Value;
                if (BaseColorLookup.HasComponent(legEntity))
                {
                    BaseColorLookup[legEntity] = new URPMaterialPropertyBaseColor { Value = teamColor };
                }
                if (TeamLookup.HasComponent(legEntity))
                {
                    TeamLookup[legEntity] = selfTeam;
                }
            }

            applyTeam.ValueRW = false;
        }
    }
}
