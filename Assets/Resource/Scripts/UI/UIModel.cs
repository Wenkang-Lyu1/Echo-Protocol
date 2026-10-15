using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UIModel : ModelBase
{
    public UIPanelType currentPanel = UIPanelType.None;
    public Stack<UIPanelType> panelStack = new Stack<UIPanelType>();
    public bool isAnyPanelOpen;

    public override void Reset()
    {
        currentPanel = UIPanelType.None;
        panelStack.Clear();
        isAnyPanelOpen = false;
    }
}

public enum UIPanelType
{
    None,
    MainMenu,
    HUD,
    Pause,
    Result,
    Setting,
    Objective
}
