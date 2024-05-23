using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(TeamAuthoring))]
public class TeamManagerAuthoring : MonoBehaviour
{
    [Header("AI - Fighter Attack")]
    public float FighterAttackPlanetConsideration = 1f;
    public float2 FighterAttackThreatLevelConsiderationClamp = new float2(0f, 1f);
    public float2 FighterAttackResourceScoreConsiderationClamp = new float2(0f, 1f);
    public float2 FighterAttackDistanceFromOwnedPlanetsConsiderationClamp = new float2(0f, 1f);
    
    [Header("AI - Fighter Defend")]
    public float FighterDefendPlanetConsideration = 1f;
    public float2 FighterDefendThreatLevelConsiderationClamp = new float2(0f, 1f);
    public float2 FighterDefendResourceScoreConsiderationClamp = new float2(0f, 1f);
    
    [Header("AI - Worker Capture")]
    public float WorkerCapturePlanetConsideration = 1f;
    public float2 WorkerCaptureSafetyLevelConsiderationClamp = new float2(0f, 1f);
    public float2 WorkerCaptureResourceScoreConsiderationClamp = new float2(0f, 1f);
    public float2 WorkerCaptureDistanceFromOwnedPlanetsConsiderationClamp = new float2(0f, 1f);
    
    [Header("AI - Worker Build")]
    public float WorkerBuildConsideration = 1f;
    public float2 WorkerBuildSafetyLevelConsiderationClamp = new float2(0f, 1f);
    public float2 WorkerBuildResourceScoreConsiderationClamp = new float2(0f, 1f);
    
    [Header("AI - Trader")]
    public float2 TraderSafetyLevelConsiderationClamp = new float2(0f, 1f);
    
    [Header("AI - Ship Production")]
    public float MaxShipProductionBias = 100f;
    public float DesiredFightersPerOtherShip = 1f;
    public float DesiredWorkerValuePerPlanet = 10f;
    public float DesiredTraderValuePerOwnedPlanet = 10f;

    private class Baker : Baker<TeamManagerAuthoring>
    {
        public override void Bake(TeamManagerAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new TeamManager());
            AddComponent(entity, new TeamManagerAI
            {
                FighterAttackPlanetConsideration = math.saturate(authoring.FighterAttackPlanetConsideration),
                FighterAttackThreatLevelConsiderationClamp = math.saturate(authoring.FighterAttackThreatLevelConsiderationClamp),
                FighterAttackResourceScoreConsiderationClamp = math.saturate(authoring.FighterAttackResourceScoreConsiderationClamp),
                FighterAttackDistanceFromOwnedPlanetsConsiderationClamp = math.saturate(authoring.FighterAttackDistanceFromOwnedPlanetsConsiderationClamp),
                
                FighterDefendPlanetConsideration = math.saturate(authoring.FighterDefendPlanetConsideration),
                FighterDefendThreatLevelConsiderationClamp = math.saturate(authoring.FighterDefendThreatLevelConsiderationClamp),
                FighterDefendResourceScoreConsiderationClamp = math.saturate(authoring.FighterDefendResourceScoreConsiderationClamp),
                
                WorkerCapturePlanetConsideration = math.saturate(authoring.WorkerCapturePlanetConsideration),
                WorkerCaptureSafetyLevelConsiderationClamp = math.saturate(authoring.WorkerCaptureSafetyLevelConsiderationClamp),
                WorkerCaptureResourceScoreConsiderationClamp = math.saturate(authoring.WorkerCaptureResourceScoreConsiderationClamp),
                WorkerCaptureDistanceFromOwnedPlanetsConsiderationClamp = math.saturate(authoring.WorkerCaptureDistanceFromOwnedPlanetsConsiderationClamp),
                
                WorkerBuildConsideration = math.saturate(authoring.WorkerBuildConsideration),
                WorkerBuildSafetyLevelConsiderationClamp = math.saturate(authoring.WorkerBuildSafetyLevelConsiderationClamp),
                WorkerBuildResourceScoreConsiderationClamp = math.saturate(authoring.WorkerBuildResourceScoreConsiderationClamp),
                
                TraderSafetyLevelConsiderationClamp = math.saturate(authoring.TraderSafetyLevelConsiderationClamp),
                
                MaxShipProductionBias = authoring.MaxShipProductionBias,
                DesiredFightersPerOtherShip = authoring.DesiredFightersPerOtherShip,
                DesiredWorkerValuePerPlanet = authoring.DesiredWorkerValuePerPlanet,
                DesiredTraderValuePerOwnedPlanet = authoring.DesiredTraderValuePerOwnedPlanet,
            });
            AddBuffer<PlanetIntel>(entity);
            AddBuffer<FighterAction>(entity);
            AddBuffer<WorkerAction>(entity);
            AddBuffer<TraderAction>(entity);
            AddBuffer<FactoryAction>(entity);
        }
    }
}
