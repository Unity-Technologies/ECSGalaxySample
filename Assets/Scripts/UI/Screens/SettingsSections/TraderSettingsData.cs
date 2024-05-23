using Unity.Entities;
using UnityEngine.UIElements;

public class TraderSettingsData
{
    private int m_TraderDataIndex;
    private VisualElement m_Root;
    
    private TextField m_ResourceExchangeRangeField;
    private TextField m_ResourceCarryCapacityField;
    private TextField m_ResourceCarryMinimumLoadField;
    private TextField m_MinStoredResourcesThresholdForGiverPlanetField;
    
    public TraderSettingsData(VisualElement parentElement, TraderData traderData, int index)
    {
        m_Root = parentElement;
        m_TraderDataIndex = index;
        SetVisualElements(parentElement);
        InitializeData(traderData);
        SubscribeToEvents();
    }
    
    private void SetVisualElements(VisualElement parentElement)
    {
        m_ResourceExchangeRangeField = m_Root.Q<TextField>("resource-exchange-range-field");
        m_ResourceCarryCapacityField = m_Root.Q<TextField>("resource-carry-capacity-field");
        m_ResourceCarryMinimumLoadField = m_Root.Q<TextField>("resource-carry-minimum-load-field");
        m_MinStoredResourcesThresholdForGiverPlanetField = m_Root.Q<TextField>("min-stored-resources-threshold-for-giver-planet-field");
    }
    
    private void InitializeData(TraderData traderData)
    {
        m_ResourceExchangeRangeField.value = traderData.ResourceExchangeRange.ToString();
        m_ResourceCarryCapacityField.value = traderData.ResourceCarryCapacity.ToString();
        m_ResourceCarryMinimumLoadField.value = traderData.ResourceCarryMinimumLoad.ToString();
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

                Entity shipPrefabEntity = shipCollectionBuffer[m_TraderDataIndex].PrefabEntity;
                if (entityManager.HasComponent<Trader>(shipPrefabEntity))
                {
                    Trader trader = entityManager.GetComponentData<Trader>(shipPrefabEntity);
                    ref TraderData traderData = ref trader.TraderData.Value;
                    traderData.ResourceExchangeRange = float.Parse(m_ResourceExchangeRangeField.value);
                    traderData.ResourceCarryCapacity = float.Parse(m_ResourceCarryCapacityField.value);
                    traderData.ResourceCarryMinimumLoad = float.Parse(m_ResourceCarryMinimumLoadField.value);
                } 
            }
        }
    }
}