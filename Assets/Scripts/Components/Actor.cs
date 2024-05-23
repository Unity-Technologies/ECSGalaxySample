using Unity.Entities;

public struct ActorType : IComponentData
{
    public byte Type;
    
    // Ships
    public const byte FighterType = 1;
    public const byte WorkerType = 2;
    public const byte TraderType = 3;
    
    // Buildings
    public const byte FactoryType = 11;
    public const byte TurretType = 12;
    public const byte ResearchType = 13;
}

public struct Targetable : IComponentData
{ }

public struct EntityName : IComponentData
{
    public BlobAssetReference<EntityNameBlobData> NameData;
}

public struct EntityNameBlobData
{
    public BlobString Name;
}