// ============================================================
// GameEvent -- global event enum.
// Naming rule: Module_Action_State
// ============================================================
public enum GameEvent
{
    None,

        // ---- Game Flow ----
    Game_Start,
    Game_StateChanged,
    Game_TogglePause,
    Game_GameOver,
    Game_Victory,
    Game_Restart,

        // ---- War (combat root) ----
    War_Start,
    War_End,
    War_Pause,
    War_Resume,
    War_KillCountChanged,
    War_TimeChanged,

        // ---- Player ----
    Player_Spawned,
    Player_Moved,
    Player_Hurt,
    Player_Died,
    Player_InputChanged,

        // ---- Weapon ----
    Weapon_Fire,
    Weapon_Fired,
    Weapon_Reload,
    Weapon_Reloaded,
    Weapon_AmmoChanged,
    Weapon_Switch,

        // ---- Echo ----
    Echo_RecordStart,
    Echo_RecordTick,
    Echo_RecordEnd,
    Echo_Deploy,
    Echo_DeployFailed,
    Echo_PlaybackStart,
    Echo_PlaybackTick,
    Echo_PlaybackEnd,
    Echo_Expired,
    Echo_CountChanged,

        // ---- Enemy ----
    Enemy_Spawn,
    Enemy_Alert,
    Enemy_Chase,
    Enemy_Attack,
    Enemy_Hurt,
    Enemy_Die,

        // ---- Level ----
    Level_Load,
    Level_Start,
    Level_WaveStart,
    Level_ObjectiveUpdated,
    Level_Complete,

        // ---- Resource ----
    Resource_LoadRequest,
    Resource_Loaded,
    Resource_Unload,

        // ---- Combat (damage pipeline) ----
    Combat_HitConfirmed,

        // ---- UI ----
    UI_OpenPanel,
    UI_ClosePanel,
    UI_Refresh,
    UI_Message,

    // Backing identifiers for module event names (use LoginEvent at call sites).
    Login_Changed,
    Login_Done,
}
