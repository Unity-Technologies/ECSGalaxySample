using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class ShipSettingData
{
    private int m_ShipCollectionIndex;
    private string m_ShipCollectionName;
    private VisualElement m_Root;
    private Label m_TitleLabel;

    // General Movement
    private TextField m_MaxSpeedField;
    private TextField m_AcceleartionField;
    private TextField m_SteeringSharpnessField;

    // Construction
    private TextField m_ConstructionValueField;
    private TextField m_BuildProbabilityForShipTypeField;
    private Vector3Field m_ResourcesCostField;
    private TextField m_BuildTimeField;

    public ShipSettingData(VisualElement parentElement, ShipCollection shipCollection, int index, string name)
    {
        m_Root = parentElement;
        m_ShipCollectionIndex = index;
        m_ShipCollectionName = name;
        SetVisualElements();
        InitializeData(shipCollection);
        SubscribeToEvents();
    }

    private void SetVisualElements()
    {
        m_TitleLabel = m_Root.Q<Label>("section-title-label");

        m_MaxSpeedField = m_Root.Q<TextField>("max-speed-field");
        m_AcceleartionField = m_Root.Q<TextField>("acceleration-field");
        m_SteeringSharpnessField = m_Root.Q<TextField>("steering-sharpness-field");

        m_ConstructionValueField = m_Root.Q<TextField>("construction-value-field");
        m_BuildProbabilityForShipTypeField = m_Root.Q<TextField>("build-probability-for-ship-type-field");
        m_ResourcesCostField = m_Root.Q<Vector3Field>("resources-cost-field");
        m_BuildTimeField = m_Root.Q<TextField>("build-time-field");
    }

    private void InitializeData(ShipCollection shipCollection)
    {
        m_TitleLabel.text = m_ShipCollectionName;
        
        m_MaxSpeedField.value = shipCollection.ShipData.Value.MaxSpeed.ToString();
        m_AcceleartionField.value = shipCollection.ShipData.Value.Acceleration.ToString();
        m_SteeringSharpnessField.value = shipCollection.ShipData.Value.SteeringSharpness.ToString();
        
        m_ConstructionValueField.value = shipCollection.ShipData.Value.Value.ToString();
        m_BuildProbabilityForShipTypeField.value = shipCollection.ShipData.Value.BuildProbabilityForShipType.ToString();
        m_ResourcesCostField.value = shipCollection.ShipData.Value.ResourcesCost;
        m_BuildTimeField.value = shipCollection.ShipData.Value.BuildTime.ToString();
    }

    private void SubscribeToEvents()
    {
        m_Root.RegisterCallback<ChangeEvent<string>>(_ => ApplyChanges());
        m_Root.RegisterCallback<ChangeEvent<Vector2>>(_ => ApplyChanges());
        m_Root.RegisterCallback<ChangeEvent<Vector3>>(_ => ApplyChanges());
        m_Root.RegisterCallback<ChangeEvent<bool>>(_ => ApplyChanges());
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
                
                ref ShipData shipData = ref shipCollectionBuffer[m_ShipCollectionIndex].ShipData.Value;
                shipData.MaxSpeed = float.Parse(m_MaxSpeedField.value);
                shipData.Acceleration = float.Parse(m_AcceleartionField.value);
                shipData.SteeringSharpness = float.Parse(m_SteeringSharpnessField.value);
                shipData.Value = float.Parse(m_ConstructionValueField.value);
                shipData.BuildProbabilityForShipType = float.Parse(m_BuildProbabilityForShipTypeField.value);
                shipData.ResourcesCost = m_ResourcesCostField.value;
                shipData.BuildTime = float.Parse(m_BuildTimeField.value);
            }
        }
    }
}