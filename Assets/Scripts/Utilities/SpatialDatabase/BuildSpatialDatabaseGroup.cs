using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

[UpdateAfter(typeof(BeginSimulationMainThreadGroup))]
public partial class BuildSpatialDatabaseGroup : ComponentSystemGroup
{
    
}