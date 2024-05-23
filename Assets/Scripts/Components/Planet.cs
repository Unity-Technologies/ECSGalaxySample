using Unity.Entities;
using Unity.Mathematics;
    
public struct Planet : IComponentData
{
    public float ShipsAssessmentExtents;
    public float CaptureTime;

    public float3 ResourceMaxStorage;
    public float3 ResourceGenerationRate;
    
    public float3 ResourceCurrentStorage;
    public int ShipsAssessmentCounter;
    public float CaptureProgress;
    public int LastConvertingTeam;

    public ResearchBonuses ResearchBonuses;
}
    
public struct MoonReference : IBufferElementData
{
    public Entity Entity;
}
    
public struct PlanetNetwork : IBufferElementData
{
    public Entity Entity;
    public float3 Position;
    public float Distance;
    public float Radius;
}
    
public struct PlanetShipsAssessment : IBufferElementData
{
    public int FighterCount;
    public int WorkerCount;
    public int TraderCount;

    public int TotalCount => FighterCount + WorkerCount + TraderCount;
}

public struct CapturingWorker : IBufferElementData
{
    public float CaptureSpeed;
}