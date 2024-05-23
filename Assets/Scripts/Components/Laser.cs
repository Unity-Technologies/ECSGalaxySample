using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct Laser : IComponentData
{
    public float MaxLifetime;
    public float LifetimeCounter;
    public byte HasExistedOneFrame;
}
