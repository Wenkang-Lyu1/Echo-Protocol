using System;
using UnityEngine;

/// <summary>Explicit opt-in; the tutorial never uses this configuration.</summary>
[Serializable]
public sealed class PreparationConfig
{
    public LevelConfig levelConfig;
    public float DurationSeconds => levelConfig.durationSeconds;
    public PlayerControl player;
    public EchoControl echo;
    public GameObject echoVisual;
    public Material shadowMaterial;
    public Material enemyShadowMaterial;
    public Transform effectsRoot;
    public PreparationEnemy[] enemies = new PreparationEnemy[0];
    public U_Level_MainView hud;
    public CombatConfig combat = new CombatConfig();
}
