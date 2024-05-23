using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class EntityNameAuthoring : MonoBehaviour
{
    public string Name;
    
    class Baker : Baker<EntityNameAuthoring>
    {
        public override void Bake(EntityNameAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            ref EntityNameBlobData definition = ref builder.ConstructRoot<EntityNameBlobData>();
            builder.AllocateString(ref definition.Name, authoring.Name);
            BlobAssetReference<EntityNameBlobData> blobReference = builder.CreateBlobAssetReference<EntityNameBlobData>(Allocator.Persistent);
            AddBlobAsset(ref blobReference, out var hash);
            builder.Dispose();
            
            AddComponent(entity, new EntityName
            {
                NameData = blobReference,
            });
        }
    }
}
