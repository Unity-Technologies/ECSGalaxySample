using Unity.Entities;
using UnityEngine.UIElements;

public class WorkerSettingsData
{
    private int m_WorkerDataIndex;
    private VisualElement m_Root;

    private TextField m_CaptureRangeField;
    private TextField m_CaptureSpeedField;
    private TextField m_BuildRangeField;
    private TextField m_BuildSpeedField;

    public WorkerSettingsData(VisualElement parentElement, WorkerData workerData, int index)
    {
        m_Root = parentElement;
        m_WorkerDataIndex = index;
        SetVisualElements();
        InitializeData(workerData);
        SubscribeToEvents();
    }

    private void SetVisualElements()
    {
        m_CaptureRangeField = m_Root.Q<TextField>("capture-range-field");
        m_CaptureSpeedField = m_Root.Q<TextField>("capture-speed-field");
        m_BuildRangeField = m_Root.Q<TextField>("build-range-field");
        m_BuildSpeedField = m_Root.Q<TextField>("build-speed-field");
    }

    private void InitializeData(WorkerData workerData)
    {
        m_CaptureRangeField.value = workerData.CaptureRange.ToString();
        m_CaptureSpeedField.value = workerData.CaptureSpeed.ToString();
        m_BuildRangeField.value = workerData.BuildRange.ToString();
        m_BuildSpeedField.value = workerData.BuildSpeed.ToString();
    }

    private void SubscribeToEvents()
    {
        m_Root.RegisterCallback<ChangeEvent<string>>(_ => SendChanges());
    }

    private void SendChanges()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();
        if (GameUtilities.TryGetSingletonEntity<Config>(entityManager, out Entity configEntity))
        {
            if (entityManager.HasBuffer<ShipCollection>(configEntity))
            {
                DynamicBuffer<ShipCollection> shipCollectionBuffer =
                    entityManager.GetBuffer<ShipCollection>(configEntity);

                Entity shipPrefabEntity = shipCollectionBuffer[m_WorkerDataIndex].PrefabEntity;
                if (entityManager.HasComponent<Worker>(shipPrefabEntity))
                {
                    Worker worker = entityManager.GetComponentData<Worker>(shipPrefabEntity);
                    ref WorkerData workerData = ref worker.WorkerData.Value;
                    workerData.CaptureRange = float.Parse(m_CaptureRangeField.value);
                    workerData.CaptureSpeed = float.Parse(m_CaptureSpeedField.value);
                    workerData.BuildRange = float.Parse(m_BuildRangeField.value);
                    workerData.BuildSpeed = float.Parse(m_BuildSpeedField.value);
                }
            }
        }
    }
}