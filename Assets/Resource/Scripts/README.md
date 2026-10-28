# Echo Protocol 脚本入口

完整文档见 [AIDoc 索引](../AIDoc/S-索引.md)。功能修改前读取相关需求与实现说明。

## 职责与目录

| 概念 | 职责 | 主要位置 |
| --- | --- | --- |
| Model | 纯数据，由 Control 持有与修改 | 对应业务模块 |
| Control | 业务、状态与通知 | `Control/`、`War/`、`Level/` |
| View | 展示与交互请求，不写 Model | `UI/` 或玩法模块 |
| Util | 无状态计算，不派发事件 | `Utils/` 或业务目录 |
| Event | 广播及解耦请求；也允许直接调用 Control | `EventSystem/` 与模块 Event |

`Core/` 为基类、注册表及通用服务，`Resource/` 为加载与池，`Animation/` 为动画驱动，`Editor/` 为构建与验证工具。

## 开发约定

- GlobalControl 是单例，Awake 自动注册；ControlRegistry 驱动 Initialize、Tick、ResetState、Shutdown，不另写 Update。
- `AutoRegister = false` 的子模块由父 Control 唯一驱动。多实例角色或敌人不应复制单例控制器。
- ViewRegistry 提供可选查找；View 可通过 `GetModel()` 读数据，新 UI 显式绑定 Model。
- 新 UI 使用 UIViewBase / UIViewManager，命名、目录和 10 秒回收规则见[UI 规范](../AIDoc/X-项目规范UI.md)。
- 事件采用 `模块名Event.On事件名` 与按需 EventScope，使用与清理见[Event 说明](../AIDoc/E-Event实现说明.md)。
- 单例 Instance 不扫描场景，访问可能创建对象；常驻与场景归属见[Frame 说明](../AIDoc/F-Frame框架.md)。

## 当前入口与限制

Scene_Start 默认打开登录并进入 Scene_Level1 正式关卡回声准备阶段；第 1、2 部分 [已完成]，见[关卡流程](../AIDoc/G-关卡完整流程.md)。Scene_Tutorial 保留试玩，由 TutorialControl 驱动，操作和验证见[试玩说明](../AIDoc/X-新手试玩关卡.md)。正式开战、回放与 Boss 待实现。

InputManager 未配置 Input System 动作时回退 Legacy Input。正式 WeaponControl 目前只广播命中事件，伤害需显式交给 DamageSystem；试玩有独立伤害流程。现有普通 EnemyControl 的单例结构不支持直接多实例化，正式波次需补齐实例管理。
