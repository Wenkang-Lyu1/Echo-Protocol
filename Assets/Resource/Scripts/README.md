# Echo Protocol — 轻量 Unity 框架

一句话：**Model 存数据，Control 写逻辑，View 只显示，Util 只计算，EventCenter 只传消息。**

目标是「想创意的时间多过想框架的时间」，所以这里只有 5 个概念、2 个注册表。

## 一、五个概念

| 概念 | 基类 / 接口 | 一句话职责 |
|------|-------------|-----------|
| Model | `ModelBase` | 纯数据，可序列化，不含逻辑 |
| Control | `GlobalControl<T, M>` | 全局唯一的逻辑单元，持有并唯一拥有 M |
| View | `ViewBase : MonoBehaviour` | 只把 Model 显示出来，绝不修改它 |
| Util | 普通 `static class` | 纯计算，无状态，不派发事件 |
| Event | `EventCenter` + `GameEvent` | 模块之间唯一的解耦通道 |

两个注册表，各有明确分工：

```
ControlRegistry   驱动所有 Control 的生命周期（Initialize / Tick / ResetState / Shutdown）
ViewRegistry      按名字或类型查找 View（可选，不写也能跑）
```

Util 不需要注册表：静态类直接调用。上一版的 `UtilRegistry` / `IUtil` / `UtilBase`
已删除——它既无法返回静态类实例，也没人调用，纯属负担。

## 二、数据流向（最重要的一条规则）

```
            ┌─────────────┐
            │    Model    │   纯数据
            └──────┬──────┘
            持有   │   读
            ┌──────▼──────┐        ┌──────────┐
            │   Control   │◄───────┤   Util   │  纯计算
            └──────┬──────┘        └──────────┘
              驱动 │ 事件
            ┌──────▼──────┐
            │    View     │   只显示
            └─────────────┘
```

| 场景 | 正确做法 |
|------|----------|
| AView 读 AModel | `AControl.Instance.GetModel()`（View 的 `_model` 就是它） |
| AView 读 BModel | `BControl.Instance.GetModel().Xxx` |
| 业务计算 | `XxxUtil.DoSomething()` |
| 跨模块通知 | `EventCenter.Instance.Dispatch(...)` |

**View 不写 Model，Control 不碰 UI 控件，Util 不派发事件。**

## 三、目录结构

```
Assets/Resource/Scripts/
├── Core/
│   ├─ MonoSingleton.cs      单例基类（场景里有就用场景的，没有就自动创建）
│   ├─ GlobalControl.cs      Control 基类
│   ├─ IControl.cs           Control 接口（6 个成员）ResetState 
│   ├─ ControlRegistry.cs    ★ 驱动所有 Control
│   ├─ ModelBase.cs          Model 基类
│   ├─ ViewBase.cs           View 基类（含父子层级）
│   ├─ IView.cs              View 接口 + GetChild 扩展
│   ├─ ViewRegistry.cs       View 查找表
│   ├─ DataContext.cs        父子 View 传数据的小袋子
│   ├─ GameLauncher.cs       启动入口（挂在 Frame 上）
│   ├─ GameManager.cs        游戏状态机
│   └─ DamageSystem.cs       伤害分发（含 IDamageable / HitBox）
├── EventSystem/             EventCenter / GameEvent / GameEventArgs
├── War/                     WarControl + Player / Weapon / Echo / Enemy 子模块
├── Level/                   LevelControl / LevelModel / LevelView
├── UI/                      UIControl / UIModel / UIEventBinder / UIPanel
├── Animation/               AnimDriver / AnimClipSet
├── Resource/                AssetLoader / ObjectPool
└── Utils/                   纯静态工具：Damage / Echo / Enemy / WarVictory / Weapon / Input
```

## 四、生命周期

```
Control   Initialize → (Tick 每帧) → ResetState → Shutdown     ← 全部由 ControlRegistry 驱动
View      Initialize → Show → Refresh → Hide → Shutdown
```

**Control 不要自己写 `Update()`**：注册表统一 Tick，写两次会被更新两遍。
需要「只在战斗时更新」的子模块，交给父 Control（见下方 `AutoRegister`）。

## 五、命名约定（看代码时会遇到的三个问题）

1. **为什么 Control 是 `ResetState()` 而不是 `Reset()`**
   `Reset()` 是 MonoBehaviour 的编辑器消息（加组件、右键 Reset 时会被 Unity 调用）。
   逻辑重置换个名字，就不会被编辑器误触发。Model 不是 MonoBehaviour，所以 `ModelBase.Reset()` 照常叫 `Reset()`。

2. **View 怎么找到自己的 Control**
   约定：`PlayerView` → `PlayerControl`（去掉 `View` 补 `Control`），用于**顶层 Control**（WarView / LevelView）。
   子模块（Player / Weapon / Echo / Enemy）不在注册表里，找不到名字，
   所以它们的 View 要在 Inspector 里拖到 Control 的 `view` 字段。
   拖了字段的 View 就不需要约定查找。

3. **`AutoRegister => false` 是什么**
   Player / Weapon / Echo / Enemy 是 `WarControl` 的子模块，由它创建并 Tick。
   它们退出注册表，注册表就不会再 Tick 一次。

## 六、新增一个功能（三步）

```csharp
// 1) 数据
public class DoorModel : ModelBase
{
    public bool isOpen;
    public override void Reset() { isOpen = false; }
}

// 2) 逻辑
public class DoorControl : GlobalControl<DoorControl, DoorModel>
{
    protected override void OnInitialize()
    {
        EventCenter.Instance.AddListener(GameEvent.Level_Start, OnLevelStart);
    }

    protected override void Unsubscribe()
    {
        EventCenter.Instance.RemoveListener(GameEvent.Level_Start, OnLevelStart);
    }

    public void Open()
    {
        model.isOpen = true;
        EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.Level_ObjectiveUpdated });
    }

    private void OnLevelStart(GameEventArgs args) { }
}

// 3) 显示
public class DoorView : ViewBase
{
    DoorModel _door;
    protected override void OnRefresh() { _door = _model as DoorModel; }
    protected override void Subscribe() { EventCenter.Instance.AddListener(GameEvent.Level_ObjectiveUpdated, _ => Refresh()); }
    protected override void Unsubscribe() { }
}
```

新 Control 不用注册：`GlobalControl.Awake` 自动进 `ControlRegistry`。
想让它在 `InitializeAll` 里排前面，加一个特性：

```csharp
[RegisterControl(ControlPhase.Input, 10)]
```

## 七、事件

```csharp
// 派发
EventCenter.Instance.Dispatch(new GameEventArgs { eventType = GameEvent.War_Start, data = model });

// 订阅 / 取消（在 Subscribe / Unsubscribe 里成对写）
EventCenter.Instance.AddListener(GameEvent.Player_Died, OnPlayerDied);
EventCenter.Instance.RemoveListener(GameEvent.Player_Died, OnPlayerDied);
```

`GameEvent` 用 `模块_动作` 命名；一次派发期间再次派发的事件会延后到下一帧，避免重入。

## 八、场景：Scene_Start 的 Frame

框架能力全部挂在 `Scene_Start` 的 **Frame** 下面，一个节点一项能力：

```
Scene_Start
├── Main Camera
├── Directional Light
└── Frame                     ← 框架根，运行后整棵树常驻
    ├── Frame 自身            GameLauncher + ControlRegistry
    ├── Event                 EventCenter
    ├── Flow                  GameManager
    ├── Input                 InputManager
    ├── Combat                DamageSystem
    ├── Assets                AssetLoader
    └── Audio                 （保留空节点，以后放 AudioControl）
```

细节、以及怎么加/减能力，见 `Assets/Resource/AIDoc/Frame.txt`。

## 九、扩展点

- **新武器**：`WeaponModel` + `WeaponUtil`
- **新敌人**：`EnemyType` + `EnemyModel`
- **新战术**：`EchoModel` 时间线 + `EchoUtil` 采样规则
- **存档**：Model 都是 `[Serializable]`，直接 JSON
- **换资源方案**：只改 `AssetLoader.cs`

## 十、已知限制

1. 无 Unity 编辑器环境下只做编译级校验，玩法数值需在编辑器里调
2. Input System 用反射接入；未配置动作资产时，InputManager 使用 Legacy Input 提供键鼠输入
3. Echo 残影的 NavMeshAgent 在狭窄地形可能穿墙，建议 CharacterController
4. 伤害管线默认不结算：`WeaponControl` 只广播 `Weapon_Fired`，
   需要伤害时把同一个 `HitArgs` 再交给 `DamageSystem.Instance.Dispatch(hit)`

新手试玩入口为 `Assets/Scenes/Scene_Level1.unity`，可直接 Play。该关卡使用独立 TutorialControl 结算射击与回声伤害；操作和实现说明见 `Assets/Resource/AIDoc/L-新手试玩关卡.md`。
