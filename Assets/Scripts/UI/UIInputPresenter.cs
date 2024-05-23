using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    public class UIInputPresenter : MonoBehaviour
    {
        private void OnEnable()
        {
            UIEvents.IgnoreCameraInput += IgnoreCameraInput;
        }

        private void OnDisable()
        {
            UIEvents.IgnoreCameraInput -= IgnoreCameraInput;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UIEvents.TogglePause?.Invoke();
            }
        }

        private void IgnoreCameraInput(bool value)
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity ignoreCameraInputRequest = entityManager.CreateEntity(typeof(IgnoreCameraInputRequest));
            entityManager.SetComponentData(ignoreCameraInputRequest, new IgnoreCameraInputRequest
            {
                Value = value
            });
        }
    }
}