using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy
{
    public class PlanetStatsScreen : UIScreen
    {
        private VisualElement m_StatsPanel;
        private Label m_TeamLabel;
        private ProgressBar m_ConversionProgressBar;
        private ProgressBar m_Resource1Bar;
        private ProgressBar m_Resource2Bar;
        private ProgressBar m_Resource3Bar;
        private Label m_Resource1RateLabel;
        private Label m_Resource2RateLabel;
        private Label m_Resource3RateLabel;

        public PlanetStatsScreen(VisualElement parentElement) : base(parentElement)
        {
            SetVisualElements();
            SubscribeToEvents();

            m_IsTransparent = true;
        }

        private void SetVisualElements()
        {
            m_StatsPanel = m_RootElement.Q<VisualElement>("stats-panel");
            m_ConversionProgressBar = m_RootElement.Q<ProgressBar>("conversion-progress-bar");
            m_Resource1Bar = m_RootElement.Q<ProgressBar>("resource-1-bar");
            m_Resource2Bar = m_RootElement.Q<ProgressBar>("resource-2-bar");
            m_Resource3Bar = m_RootElement.Q<ProgressBar>("resource-3-bar");
            m_Resource1RateLabel = m_RootElement.Q<Label>("resource-1-rate");
            m_Resource2RateLabel = m_RootElement.Q<Label>("resource-2-rate");
            m_Resource3RateLabel = m_RootElement.Q<Label>("resource-3-rate");
            m_TeamLabel = m_RootElement.Q<Label>("team-label");
        }

        private void SubscribeToEvents()
        {
            UIEvents.UpdatePlanetSelection += UpdateStats;
            UIEvents.OnRequestSimulationStart += Show;
        }

        private void UpdateStats(StatsData statsData)
        {
            if (statsData.Visible)
            {
                m_StatsPanel.style.display = DisplayStyle.Flex;
                UpdatePanelPosition(statsData.TargetPosition);

                // Update stats
                m_TeamLabel.text = statsData.PlanetData.TeamIndex < 0
                    ? "NONE"
                    : statsData.PlanetData.TeamIndex.ToString();
                SetProgressBar(m_Resource1Bar, statsData.PlanetData.ResourceCurrentStorage.x,
                    statsData.PlanetData.ResourceMaxStorage.x);
                m_Resource1RateLabel.text = $"x{statsData.PlanetData.ResourceGenerationRate.x:F0}";
                SetProgressBar(m_Resource2Bar, statsData.PlanetData.ResourceCurrentStorage.y,
                    statsData.PlanetData.ResourceMaxStorage.y);
                m_Resource2RateLabel.text = $"x{statsData.PlanetData.ResourceGenerationRate.y:F0}";
                SetProgressBar(m_Resource3Bar, statsData.PlanetData.ResourceCurrentStorage.z,
                    statsData.PlanetData.ResourceMaxStorage.z);
                m_Resource3RateLabel.text = $"x{statsData.PlanetData.ResourceGenerationRate.z:F0}";

                float conversionProgress =
                    (1f - statsData.PlanetData.ConversionProgress / statsData.PlanetData.ConversionTime) * 100f;
                SetProgressBar(m_ConversionProgressBar, conversionProgress, 100f);
            }
            else
            {
                m_StatsPanel.style.display = DisplayStyle.None;
                m_Resource1Bar.value = 0;
                m_Resource2Bar.value = 0;
                m_Resource3Bar.value = 0;
            }
        }

        private void SetProgressBar(ProgressBar progressBar, float value, float maxValue = 100f)
        {
            progressBar.highValue = maxValue;
            progressBar.value = value;
            progressBar.title = value.ToString("F2");
        }

        private void UpdatePanelPosition(Vector3 targetPosition)
        {
            if (Camera.main == null || m_RootElement == null || m_RootElement.panel == null)
                return;

            Camera mainCamera = Camera.main;
            Vector3 cameraOffset =  targetPosition - mainCamera.transform.position;
            Vector2 newPosition = RuntimePanelUtils.CameraTransformWorldToPanel(m_RootElement.panel, targetPosition + cameraOffset, mainCamera);
            if (math.dot(mainCamera.transform.forward, targetPosition - mainCamera.transform.position) < 0f)
            {
                m_StatsPanel.transform.position = new Vector2(-1000, -1000); 
            }
            else
            {
                m_StatsPanel.transform.position = newPosition; 
            }
        }
    }
}