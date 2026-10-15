# Echo Protocol — Unity MVC 射击关卡制框架（v2）

## 架构概览

本框架采用 **MVC + 全局 Control + 事件驱动** 架构，战斗系统命名为 `War`，Echo（回声）与 Weapon（武器）均为其子模块。

### 三层注册表

```
ControlRegistry  —— 全局 Control 注册表（新增 Control 只需注册一行）
UtilRegistry     —— 全局 Util 注册表（新增 Util 只需打特性或注册一行）
ViewRegistry     —— 全局 View 注册表（自动管理 View 生命周期）
```

### 目录结构

```
Assets/Scripts/
├── Core/                          # 核心基础设施
│   ├─ MonoSingleton.cs            单例基类
│   ├─ GlobalControl.cs            Control 基类（MVC 三件套）
│   ├─ ModelBase.cs                Model 基类
│   ├─ ViewBase.cs                 View 基类（含父子层级生命周期）
│   ├─ IView.cs                    View 接口 + 生命周期枚举 + 扩展方法
│   ├─ IControl.cs                 Control 接口
│   ├─ IUtil.cs                    Util 标记接口
│   ├─ ControlRegistry.cs          ★ 全局 Control 注册表
│   ├─ UtilRegistry.cs             ★ 全局 Util 注册表
│   ├─ ViewRegistry.cs             ★ 全局 View 注册表
│   ├─ DataContext.cs              ★ 父子 View 数据传递
│   ├─ GameManager.cs              全局流程控制
│   ├─ GameLauncher.cs             启动入口
│   └─ DamageSystem.cs             伤害分发
│
├── EventSystem/                   # 事件系统
│   ├─ EventCenter.cs              事件中心（47 事件）
│   ├─ GameEvent.cs                事件枚举
│   └─ GameEventArgs.cs            事件参数
│
├── War/                           # 战斗顶层系统
│   ├─ WarControl.cs              持有 WarModel，聚合子模块
│   ├─ WarModel.cs                战斗持久数据
│   ├─ WarView.cs                 HUD 表现
│   │
│   ├─ Player/
│   │   ├─ PlayerControl
│   │   ├─ PlayerModel
│   │   └─ PlayerView
│   │
│   ├─ Weapon/
│   │   ├─ WeaponControl
│   │   ├─ WeaponModel
│   │   └─ WeaponView
│   │
│   ├─ Echo/
│   │   ├─ EchoControl             录制/部署/回放调度
│   │   ├─ EchoModel               时间线数据
│   │   ├─ EchoView                REC 指示器/残影数量
│   │   ├─ EchoFrame              帧数据结构
│   │   └─ EchoPlayback            残影回放实体
│   │
│   └─ Enemy/
│       ├─ EnemyControl
│       ├─ EnemyModel
│       └─ EnemyView
│
├── Level/
│   ├─ LevelControl
│   ├─ LevelModel
│   └─ LevelView
│
├── UI/
│   ├─ UIControl                  面板栈管理
│   ├─ UIModel
│   ├─ UIEventBinder             流程事件接入
│   └─ UIPanel
│
├── Animation/
│   ├─ AnimDriver                方案 B：OverrideController
│   └─ AnimClipSet
│
├── Resource/
│   ├─ AssetLoader
│   └─ ObjectPool<T>
│
├── Utils/                        # 纯静态无状态工具
│   ├─ EchoUtil
│   ├─ WeaponUtil
│   ├─ DamageUtil
│   ├─ EnemyUtil
│   ├─ WarVictoryUtil
│   └─ InputManager
│
└─ Editor/
    └─ ScriptOrder.cs            执行顺序管理
```

---

## 核心规则

### 1. 数据访问规则（最重要）

| 场景 | 正确做法 | 错误做法 |
|------|----------|----------|
| AView 想读 AModel | `AControl.Instance.GetModel()` | 直接持有 Model |
| AView 想读 BModel | `BControl.Instance.GetModel().GetXxx()` | 直接持有 BModel |
| 业务计算 | `BUtil.GetXxx()` | 在 View 里算 |
| 跨模块调用 | `BControl.Instance.DoXxx()` 或事件 | 直接引用 MonoBehaviour |

**View 永远不直接持有 Model**，必须通过 Control 访问。

### 2. 新增 Control 只需注册一行

```csharp
// 方式一：打特性（推荐）
[RegisterControl(typeof(MyControl), ControlPhase.Gameplay, 100)]
public class MyControl : GlobalControl<MyControl, MyModel>
{
    public override void OnInit() { /* 初始化 */ }
    public override void Tick(float dt) { /* 每帧更新 */ }
    public override void Reset() { /* 重置 */ }
}

// 方式二：手动注册
public class MyControl : GlobalControl<MyControl, MyModel>
{
    protected override void Awake()
    {
        base.Awake();
        ControlRegistry.Register(this);  // 已在基类完成
    }
}
```

### 3. 新增 Util 只需注册一行

```csharp
// 方式一：打特性（推荐）
[RegisterUtil("MyTool", UtilCategory.Combat)]
public static class MyUtil
{
    public static int CalculateSomething(int input) => input * 2;
}

// 方式二：手动注册
UtilRegistry.Register<MyUtil>("MyTool", UtilCategory.Combat);
```

### 4. View 层级生命周期

```
MainView (父)
 ├─ CityView (子)
 ├─ RoleHeadView (子)
 └─ BagView (子)
```

**规则**：
- 关闭 `MainView` 时，递归关闭所有子 View
- 销毁 `MainView` 时，递归销毁所有子 View
- 父 View 销毁时，子 View 随 GameObject 一起销毁
- 父子间通过 `GetChild/GetChild<T>` 互相访问

```csharp
// 父 View 获取子 View 数据
var cityView = this.GetChild<CityView>();
var cityData = cityView.GetData<CityData>("city");

// 子 View 获取父 View 数据（自动向上查找）
var mainData = this.GetData<MainData>("main");

// 父 View 设置数据，子 View 自动可读
this.SetData("sharedKey", sharedValue);
```

### 5. Model 规则

- 纯数据，可序列化
- 不引用 View / Control / MonoBehaviour
- 用 ID / 路径代替 Unity 对象引用

### 6. View 规则

- 只负责显示，不修改逻辑
- 通过 `Control.GetModel()` 获取数据
- 订阅事件驱动刷新
- 不持有 Control 引用

### 7. Util 规则

- 纯静态类，无状态
- 只做计算与数据加工
- 可被任何 Control / View 调用
- 不调用 EventCenter（避免隐式耦合）

---

## 事件系统

事件枚举：`GameEvent`（47 个事件）

事件参数：`GameEventArgs`

```csharp
// 派发
EventCenter.Instance.Dispatch(new GameEventArgs
{
    eventType = GameEvent.Xxx_Yyy,
    data = new ValueArgs<int>(123)
});

// 订阅
EventCenter.Instance.AddListener(GameEvent.Xxx_Yyy, OnXxx);

// 取消订阅（重要！防止泄漏）
EventCenter.Instance.RemoveListener(GameEvent.Xxx_Yyy, OnXxx);
```

---

## 动画方案（方案 B）

使用 `AnimatorOverrideController`：

1. Animator Controller 只放状态容器（不拖具体 Clip）
2. 具体 Clip 由 `AnimClipSet`（ScriptableObject）注入
3. 业务层只调 `AnimDriver.Play("Fire")` 等语义接口

**注意**：基础 Animator Controller 里每个状态必须挂一个占位 Clip（哪怕 1 帧空的），否则 `OverrideController` 找不到映射目标。

---

## 执行顺序

`ScriptOrder.cs` 集中管理：

| 顺序 | 模块 |
|------|------|
| -2000 | EventCenter |
| -1500 | ControlRegistry |
| -1000 | GameLauncher |
| -900 | GameManager / InputManager |
| -850 | DamageSystem / AssetLoader |
| -800 | War 子模块 Controls |
| -700 | UIControl / UIEventBinder |
| -500 | Views |

---

## 快速开始

### 1. 场景搭建

```
Scene
 ├─ EventCenter (GameObject + EventCenter.cs)
 ├─ ControlRegistry (GameObject + ControlRegistry.cs)
 ├─ GameLauncher (GameObject + GameLauncher.cs)
 ├─ GameManager (GameObject + GameManager.cs)
 ├─ InputManager (GameObject + InputManager.cs)
 │
 ├─ WarRoot
 │   ├─ WarControl
 │   ├─ PlayerControl + PlayerView + CharacterController
 │   ├─ WeaponControl + WeaponView
 │   ├─ EchoControl
 │   └─ EnemyManager
 │
 ├─ LevelRoot
 │   └─ LevelControl
 │
 └─ Canvas
     ├─ MainView (ViewBase)
     │   ├─ HUDView (子 View)
     │   ├─ CityView (子 View)
     │   └─ RoleHeadView (子 View)
     ├─ MainMenuPanel
     ├─ PausePanel
     ├─ ResultPanel
     └─ SettingPanel
```

### 2. 关键 Inspector 配置

- `WarControl`：拖入 PlayerControl / WeaponControl / EchoControl / EnemyControl 引用
- `EchoControl`：设置 `echoPrefab`（EchoPlayback 预制体）
- `LevelControl`：配置 `waves` 数组、`enemyPrefabs`、`spawnPoints`
- `AnimDriver`：挂载 `defaultClipSet`（AnimClipSet 资产）

### 3. 导航网格

Enemy 使用 `NavMeshAgent`，需要烘焙导航网格：
`Window → AI → Navigation → Bake`

---

## 扩展点

- **新武器**：继承 WeaponModel，扩展 WeaponUtil
- **新敌人类型**：扩展 EnemyType 枚举 + EnemyModel
- **新战术**：Echo 时间线数据 + EchoUtil 新增采样规则
- **网络同步**：EchoTimeline 天然可序列化，适合做回放同步
- **存档**：Model 均 [Serializable]，直接 JSON 序列化

---

## 已知限制

1. 无编译器环境，仅做结构性静态检查
2. InputSystem API 在 2022.3 与 6.0 间有细微差异，需真机验证
3. Echo 残影的 NavMeshAgent 移动在狭窄地形可能有穿墙，建议改用 CharacterController
4. 波次系统较简单，复杂关卡建议扩展 LevelControl
