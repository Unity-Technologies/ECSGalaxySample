using Unity.Entities;
using Unity.Mathematics;


public struct IgnoreCameraInputRequest : IComponentData
{
    public bool Value;
}

public struct SelectionRequest : IComponentData { }