using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Galaxy
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct MainCameraSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MainCamera>();
        }

        public void OnUpdate(ref SystemState state)
        {
            LocalToWorld mainCameraLtW = SystemAPI.GetComponent<LocalToWorld>(SystemAPI.GetSingletonEntity<MainCamera>());
            if (Camera.main != null)
            {
                Camera.main.transform.SetPositionAndRotation(mainCameraLtW.Position, mainCameraLtW.Rotation);
            }
        }
    }
}