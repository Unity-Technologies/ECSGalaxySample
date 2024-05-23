using Unity.Entities;
using Unity.Mathematics;

public struct Ship : IComponentData
{
    public byte IgnoreAvoidance;
    public byte BlockNavigation;
    public float3 Velocity;
    
    public Entity NavigationTargetEntity;
    public float3 NavigationTargetPosition;
    public float NavigationTargetRadius;

    public float AccelerationMultiplier;
    public float MaxSpeedMultiplier;

    public int ThrusterVFXIndex;
    
    public BlobAssetReference<ShipData> ShipData;
}

public struct Worker : IComponentData
{
    public Entity DesiredBuildingPrefab;
    public BlobAssetReference<WorkerData> WorkerData; 
}

public struct Trader : IComponentData
{
    public Entity ReceivingPlanetEntity;
    public float3 ReceivingPlanetPosition;
    public float ReceivingPlanetRadius;

    public float3 ChosenResourceMask;
    public float3 CarriedResources;
    public int FindTradeRouteAttempts;
    
    public BlobAssetReference<TraderData> TraderData; 
}

public struct Fighter : IComponentData
{
    public float AttackTimer;
    public float DetectionTimer;
    public byte TargetIsEnemyShip;
    
    public float DamageMultiplier;
    public BlobAssetReference<FighterData> FighterData; 
}

public struct Initialize : IComponentData, IEnableableComponent
{ }

public struct ExecuteAttack : IComponentData, IEnableableComponent
{ }

public struct ExecutePlanetCapture : IComponentData, IEnableableComponent
{ }

public struct ExecuteBuild : IComponentData, IEnableableComponent
{ }

public struct ExecuteTrade : IComponentData, IEnableableComponent
{ }