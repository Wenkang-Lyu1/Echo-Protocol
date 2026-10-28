using System;
using UnityEngine;

/// <summary>Explicit opt-in; the tutorial never uses this configuration.</summary>
[Serializable]
public sealed class PreparationConfig
{
    public PlayerControl player;
    public EchoControl echo;
    public GameObject echoVisual;
    public Material shadowMaterial;
    public Transform effectsRoot;
    public PreparationEnemy[] enemies;
    public U_Level_MainView hud;
}
