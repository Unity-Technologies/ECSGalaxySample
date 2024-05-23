using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

public struct PlanetAssessmentCollector : ISpatialQueryCollector
{
    public DynamicBuffer<PlanetShipsAssessment> ShipsAssessmentBuffer;

    public PlanetAssessmentCollector(int querierTeam, DynamicBuffer<PlanetShipsAssessment> shipsAssessmentBuffer)
    {
        ShipsAssessmentBuffer = shipsAssessmentBuffer;
    } 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnVisitCell(in SpatialDatabaseCell cell, in UnsafeList<SpatialDatabaseElement> elements, out bool shouldEarlyExit)
    {
        shouldEarlyExit = false;

        for (int i = cell.StartIndex; i < cell.StartIndex + cell.ElementsCount; i++)
        {
            SpatialDatabaseElement element = elements[i];
            int elementTeam = (int)element.Team;
            PlanetShipsAssessment planetShipsAssessment = ShipsAssessmentBuffer[elementTeam];
            switch (element.Type)
            {
                case ActorType.FighterType:
                    planetShipsAssessment.FighterCount++;
                    break;
                case ActorType.WorkerType:
                    planetShipsAssessment.WorkerCount++;
                    break;
                case ActorType.TraderType:
                    planetShipsAssessment.TraderCount++;
                    break;
            }
            ShipsAssessmentBuffer[elementTeam] = planetShipsAssessment;
        }
    }
}