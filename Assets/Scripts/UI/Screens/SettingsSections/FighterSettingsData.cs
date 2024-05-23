using Unity.Entities;
using UnityEngine.UIElements;

public class FighterSettingsData
{
    private int m_FighterDataIndex;
    private VisualElement m_Root;
    
    private TextField m_DetectionRangeField;
    private TextField m_AttackRangeField;
    private TextField m_AttackDelayField;
    private TextField m_AttackDamageField;
    private TextField m_DotProdThresholdForTargetInSightsField;
    private TextField m_ShipDetectionIntervalField;
    
    public FighterSettingsData(VisualElement parentElement, FighterData fighterData, int index)
    {
        m_Root = parentElement;
        m_FighterDataIndex = index;
        SetVisualElements(parentElement);
        InitializeData(fighterData);
        SubscribeToEvents();
    }
    
    private void SetVisualElements(VisualElement parentElement)
    {
        m_DetectionRangeField = m_Root.Q<TextField>("detection-range-field");
        m_AttackRangeField = m_Root.Q<TextField>("attack-range-field");
        m_AttackDelayField = m_Root.Q<TextField>("attack-delay-field");
        m_AttackDamageField = m_Root.Q<TextField>("attack-damage-field");
        m_DotProdThresholdForTargetInSightsField = m_Root.Q<TextField>("dot-prod-threshold-for-target-in-sights-field");
        m_ShipDetectionIntervalField = m_Root.Q<TextField>("ship-detection-interval-field");
    }
    
    private void InitializeData(FighterData fighterData)
    {
        m_DetectionRangeField.value = fighterData.DetectionRange.ToString();
        m_AttackRangeField.value = fighterData.AttackRange.ToString();
        m_AttackDelayField.value = fighterData.AttackDelay.ToString();
        m_AttackDamageField.value = fighterData.AttackDamage.ToString();
        m_DotProdThresholdForTargetInSightsField.value = fighterData.DotProdThresholdForTargetInSights.ToString();
        m_ShipDetectionIntervalField.value = fighterData.ShipDetectionInterval.ToString();
    }
    
    private void SubscribeToEvents()
    {
        m_Root.RegisterCallback<ChangeEvent<string>>(_ => ApplyChanges());
    }

    private void ApplyChanges()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();
        if (GameUtilities.TryGetSingletonEntity<Config>(entityManager, out Entity configEntity))
        {
            if (entityManager.HasBuffer<ShipCollection>(configEntity))
            {
                DynamicBuffer<ShipCollection> shipCollectionBuffer =
                    entityManager.GetBuffer<ShipCollection>(configEntity);

                Entity shipPrefabEntity = shipCollectionBuffer[m_FighterDataIndex].PrefabEntity;
                if (entityManager.HasComponent<Fighter>(shipPrefabEntity))
                {
                    Fighter fighter = entityManager.GetComponentData<Fighter>(shipPrefabEntity);
                    ref FighterData fighterData = ref fighter.FighterData.Value;
                    fighterData.DetectionRange = float.Parse(m_DetectionRangeField.value);
                    fighterData.AttackRange = float.Parse(m_AttackRangeField.value);
                    fighterData.AttackDelay = float.Parse(m_AttackDelayField.value);
                    fighterData.AttackDamage = float.Parse(m_AttackDamageField.value);
                    fighterData.DotProdThresholdForTargetInSights =
                        float.Parse(m_DotProdThresholdForTargetInSightsField.value);
                    fighterData.ShipDetectionInterval = float.Parse(m_ShipDetectionIntervalField.value);
                }
            }
        }
    }
}