using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

public struct ShipQueryCollector : ISpatialQueryCollector
{
    public Entity QuerierEntity;
    public float3 QuerierPosition;
    public byte QuerierTeam;
        
    public SpatialDatabaseElement ClosestEnemy;
    public float ClosestEnemyDistanceSq;

    public ShipQueryCollector(Entity querierEntity, float3 querierPosition, int querierTeam)
    {
        QuerierEntity = querierEntity;
        QuerierPosition = querierPosition;
        QuerierTeam = (byte)querierTeam;
            
        ClosestEnemy = default;
        ClosestEnemyDistanceSq = float.MaxValue;
    } 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnVisitCell(in SpatialDatabaseCell cell, in UnsafeList<SpatialDatabaseElement> elements, out bool shouldEarlyExit)
    {
        shouldEarlyExit = false;

        for (int i = cell.StartIndex; i < cell.StartIndex + cell.ElementsCount; i++)
        {
            SpatialDatabaseElement element = elements[i];
            if (element.Team != QuerierTeam)
            {
                float distSq = math.distancesq(QuerierPosition, element.Position);
                if (distSq < ClosestEnemyDistanceSq)
                {
                    if (element.Entity.Index != QuerierEntity.Index)
                    {
                        ClosestEnemy = element;
                        ClosestEnemyDistanceSq = distSq;
                        shouldEarlyExit = true;
                        break;
                    }
                }
            }
        }
    }
}