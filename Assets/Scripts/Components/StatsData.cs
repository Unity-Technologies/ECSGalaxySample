using Unity.Mathematics;

public struct StatsData
{
    public float3 TargetPosition;
    public bool Visible;
    public PlanetData PlanetData;
}

public struct PlanetData
{
    public int TeamIndex;
    public float3 ResourceCurrentStorage;
    public float3 ResourceMaxStorage;
    public float3 ResourceGenerationRate;
    public float ConversionTime;
    public float ConversionProgress;
}