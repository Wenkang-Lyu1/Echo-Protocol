# Kenney Space Station Kit

已整理为 Unity 2021.3.5f1 / Built-in Render Pipeline 可用的场景素材。

## 使用

- 将 `Prefabs/` 中的预制体直接拖入场景。预制体引用原始 FBX，使用统一的 `Materials/SpaceStation.mat`。
- `wall-*`、`floor-*`、`door-*`、`stairs-*`、`balcony-*` 用于建筑搭建；`computer-*`、`display-*`、`table-*`、`chair-*`、`bed-*`、`container-*` 等用于陈设。
- 打开 `Documentation/Overview.html` 可查看全部模型缩略图与名称；`Preview.png`、`Sample.png` 为官方效果参考。
- `Materials/SpaceStationVariationA.mat` 使用官方备用色板。需要切换时，复制预制体或创建 Prefab Variant，再替换 Renderer 材质。
- 保留原始模型比例、轴向和配色；未自动添加碰撞体、导航、门动画或交互脚本。门的 closed / half 等模型是不同静态外形。
- 项目美术适配可通过灯光和派生材质完成。建议蓝灰环境、青色局部灯光、少量橙色交互提示；本次保留官方配色，未覆盖原图。

## 目录

| 目录 | 内容 |
| --- | --- |
| Models | 97 个官方 FBX；省去重复的 OBJ / GLB 格式 |
| Textures | 官方 colormap.png、variation-a.png |
| Materials | 两套共享 Standard 材质；点过滤、Clamp、无压缩色板贴图 |
| Prefabs | 每个模型对应一个可拖入场景的预制体 |
| Documentation | 原始 CC0 授权、预览、模型索引、导入验证结果 |
| Editor | 手动重建工具，不会在打开项目时自动改写素材 |

如需重新生成预制体，使用菜单 `Tools > Echo Protocol > Space Station > Rebuild Prefabs`。此操作会重建本目录的同名预制体；自定义改动请放在独立 Prefab Variant 中。

## 来源与授权

- 作者：Kenney；包版本：1.0（官方授权文件标注 2024-04-10）。
- 来源：https://kenney.nl/assets/space-station-kit
- 下载：https://kenney.nl/media/pages/assets/space-station-kit/6475288f2e-1712749919/kenney_space-station-kit.zip
- 整理日期：2026-10-23。
- 原始 ZIP 大小：1,835,875 字节。
- 原始 ZIP SHA-256：`215e79bd5415cff93665183390f0343ed9acf87780306331013b78520170c6d8`。
- 授权：CC0，可个人及商业使用，无强制署名要求；保留原文见 `Documentation/License.txt`。
- 官方网页概称约 90 个资源；实际 ZIP 包中包含 97 个 FBX，本次全部保留。

`Overview.html` 中的贴图链接已按整理后的目录修正，内容与预览图保留原样。

导入验证在同版本临时 Unity 项目执行，检查模型网格非空以及预制体材质、贴图引用完整；结果见 `Documentation/ImportValidation.txt`。未对项目现有关卡进行布置或更换。
