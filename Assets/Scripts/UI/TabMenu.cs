using UnityEngine.UIElements;

namespace Galaxy
{
    /// <summary>
    /// Adapted from https://docs.unity3d.com/Manual/UIE-create-tabbed-menu-for-runtime.html
    /// </summary>
    public class TabMenu
    {
        private const string k_TabClassName = "tab-button";
        private const string k_TabSelectedClassName = "tab-selected";
        private const string k_TabContentHiddenClassName = "tab-content-hidden";
        private const string k_TabNameSuffix = "tab";
        private const string k_ContentNameSuffix = "content";

        private readonly VisualElement m_Root;

        public TabMenu(VisualElement mRoot)
        {
            m_Root = mRoot;
        }

        public void RegisterTabCallbacks()
        {
            UQueryBuilder<VisualElement> tabs = GetAllTabs();
            tabs.ForEach(tab => { tab.RegisterCallback<ClickEvent>(TabOnClick); });
        }

        private void TabOnClick(ClickEvent evt)
        {
            VisualElement clickedTab = evt.currentTarget as VisualElement;
            if (!TabIsCurrentlySelected(clickedTab))
            {
                GetAllTabs().Where(tab => tab != clickedTab && TabIsCurrentlySelected(tab)).ForEach(UnselectTab);
                SelectTab(clickedTab);
            }
        }

        private static bool TabIsCurrentlySelected(VisualElement tab)
        {
            return tab.ClassListContains(k_TabSelectedClassName);
        }

        private UQueryBuilder<VisualElement> GetAllTabs()
        {
            return m_Root.Query<VisualElement>(className: k_TabClassName);
        }

        private void SelectTab(VisualElement tab)
        {
            tab.AddToClassList(k_TabSelectedClassName);
            VisualElement content = FindContent(tab);
            content.RemoveFromClassList(k_TabContentHiddenClassName);
        }

        private void UnselectTab(VisualElement tab)
        {
            tab.RemoveFromClassList(k_TabSelectedClassName);
            VisualElement content = FindContent(tab);
            content.AddToClassList(k_TabContentHiddenClassName);
        }

        private static string GenerateContentName(VisualElement tab) =>
            tab.name.Replace(k_TabNameSuffix, k_ContentNameSuffix);

        private VisualElement FindContent(VisualElement tab)
        {
            return m_Root.Q(GenerateContentName(tab));
        }
    }
}