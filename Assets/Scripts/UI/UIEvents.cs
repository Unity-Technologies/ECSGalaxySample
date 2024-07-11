using System;

public static class UIEvents
{
    public static Action ScreenClosed;
    public static Action SettingsShown;
    public static Action HomeScreenShown;
    public static Action HUDScreenShown;
    public static Action InitializeUISettings;
    public static Action OnRequestSimulationStart;
    public static Action DisableUI;
    public static Action TogglePause;
    public static Action SimulateGame;
    public static Action<bool> IgnoreCameraInput;
    
    // Planet Stats
    public static Action<StatsData> UpdatePlanetSelection;
}