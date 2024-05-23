using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public abstract class BakedScriptableObject<T> : ScriptableObject where T : unmanaged
{
    public BlobAssetReference<T> BakeToBlob(IBaker baker)
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);
        ref T definition = ref builder.ConstructRoot<T>();
    
        BakeToBlobData(ref definition, ref builder);
        
        BlobAssetReference<T> blobReference = builder.CreateBlobAssetReference<T>(Allocator.Persistent);
        baker.AddBlobAsset(ref blobReference, out var hash);
        builder.Dispose();

        baker.DependsOn(this);
        
        return blobReference;
    }
    
   protected abstract void BakeToBlobData(ref T data, ref BlobBuilder blobBuilder);
}
