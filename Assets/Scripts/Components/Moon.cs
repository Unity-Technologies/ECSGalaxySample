using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;


public struct Moon : IComponentData
{
    public Entity PlanetEntity;

    public float CummulativeBuildSpeed;
    public float BuildProgress;
    public Entity BuiltPrefab;

    public Team PreviousTeam;
}

public struct BuildingReference : IComponentData
{
    public Entity BuildingEntity;
}