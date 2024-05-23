using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    public class SimulationPresenter : MonoBehaviour
    {
        private void OnEnable()
        {
            UIEvents.OnRequestSimulationStart += OnRequestSimulationStart;
        }

        private void OnDisable()
        {
            UIEvents.OnRequestSimulationStart -= OnRequestSimulationStart;
        }

        private void OnRequestSimulationStart()
        {
            CursorUtils.HideCursor();
            
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            if (GameUtilities.TryGetSingletonRW(entityManager, out RefRW<Config> config))
            {
                config.ValueRW.MustInitializeGame = true;
            }
        }
    }
}