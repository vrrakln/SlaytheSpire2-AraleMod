# BanG Dream! 梦限大 · 仲町阿拉蕾 Mod

为《杀戮尖塔 2 / Slay the Spire 2》添加可玩角色 **仲町阿拉蕾（Nakamachi Arale）**，基于 **RitsuLib** 开发，支持简体中文和英文。

快来和主唱阿拉蕾一起爬塔！

## 内容介绍

- 专属卡池，包括多人联机卡牌，围绕梦想、锻炼、歌声与保留构筑。
- 专属遗物与三种专属药水。
- 任务书与团魂机制：完成任务获得队友的团魂，补强对应体系。
- 新增第三幕 Boss 薇欧拉及其召唤物。
- 角色立绘、战斗动画、卡框、能力图标、音效、音乐及先古之民对话。
- 模组设置支持文字加深、攻击牌音效，以及薇欧拉遭遇的关闭／随机／锁定模式。
- 可选择是否让原版角色的跨职业随机卡池包含阿拉蕾卡牌；多人游戏中该选项按玩家自身设置生效，薇欧拉遭遇设置以房主为准。

## 游玩与反馈

在 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3803538264)订阅本 Mod，并安装所需的 RitsuLib。模组目前面向游戏测试版开发，游戏或基础库更新后可能需要同步更新模组。

反馈问题时请提供游戏版本、模组版本、单人／多人模式、复现步骤与日志。游戏内按 `~` 打开控制台，输入 `open logs`，或直接打开 `%APPDATA%/SlayTheSpire2/logs/godot.log`。

QQ 交流群：**1121932992**。

## 工程结构

此仓库根目录就是 Godot/C# 工程目录，可直接打开 `project.godot` 或 `arale_mod.sln`。

```text
Scripts/                     C# 代码与模组入口
  Cards/                     卡牌及卡池逻辑
  Mechanics/                 角色机制
  Quests/                    任务与团魂
  Boss/                      薇欧拉 Boss
  Combat/                    战斗表现
arale_mod/
  images/                    卡面、角色、图标等成品资源
  audio/                     音效与音乐
  localization/zhs/          简体中文文本
  localization/eng/          英文文本
  scenes/                    Godot 场景
  scripts/                   资源侧脚本
  materials/ shaders/ vfx/    材质、着色器与特效
images/ scenes/              兼容原生资源路径的资源与场景
arale_mod.csproj             C# 依赖、编译与自动部署配置
arale_mod.json               模组清单
export_presets.cfg          Godot 导出配置
```

`images/` 和 `scenes/` 并非重复目录，请一起保留。Godot 的 `.import` 与 `.uid` 文件也属于工程资源配置；`.godot/` 缓存不提交。

## 开发环境

当前工程配置：

- .NET SDK 9，C# 13。
- Godot **4.5.1 .NET（Mono）** 与对应的 Windows Desktop 导出模板。
- 本地安装的《杀戮尖塔 2》，提供 `sts2.dll` 和 `0Harmony.dll`。
- **STS2-RitsuLib 0.5.20**；工程清单的最低游戏版本为 `0.111.0`。这不是对后续所有游戏版本的兼容保证。

游戏程序集、RitsuLib 和 Godot 工具不包含在此仓库中。运行游戏时也需安装 RitsuLib。

### 配置依赖

```powershell
git clone https://github.com/vrrakln/SlaytheSpire2-AraleMod.git arale-mod
cd arale-mod
```

当前 `arale_mod.csproj` 使用以下默认目录布局：

```text
工作目录/
  arale-mod/                 本仓库
  tools/
    STS2.RitsuLib.0.5.20.github/
      STS2-RitsuLib.dll
    Godot_v4.5.1-stable_mono_win64/
      Godot_v4.5.1-stable_mono_win64_console.exe
```

请在 `arale_mod.csproj` 中将 `Sts2Dir` 改为自己的游戏目录，将 `GodotExe` 改为 Godot 控制台程序的绝对路径。若使用其他依赖目录，同时修改 RitsuLib 的 `HintPath`。`Sts2DataDir` 默认指向游戏目录下的 `data_sts2_windows_x86_64`。

### 仅编译检查

以下命令只编译，不导出 PCK、不向游戏目录安装：

```powershell
dotnet build arale_mod.csproj '-t:Compile;CopyFilesToOutputDirectory' -v:minimal
```

### 完整构建并安装

先关闭游戏，然后执行：

```powershell
dotnet build arale_mod.sln -v:minimal
```

完整构建会自动编译 DLL、导入 Godot 资源、导出 PCK，并向游戏目录写入：

```text
mods/arale_mod/
  arale_mod.dll
  arale_mod.pck
  arale_mod.json
```

请同时检查 Godot 导入和导出日志及上述产物；当前工程将部分导出失败作为警告处理，仅看到 C# 编译成功并不代表 PCK 已更新。修改代码或资源后需要重启游戏。

本地开发时避免同时加载创意工坊版和本地版的同一模组。

## 修改文本

中文与英文分别位于 `arale_mod/localization/zhs/` 和 `arale_mod/localization/eng/`。例如 `cards.json` 为卡牌文本、`powers.json` 为能力与状态文本、`relics.json` 为遗物文本、`ancients.json` 为先古之民对话。修改时保持两种语言的键名一致，保留动态数值占位符和格式标签，并重新导出 PCK。

## English overview

**Nakamachi Arale** is a RitsuLib-based character mod for **Slay the Spire 2**, featuring a custom card pool, multiplayer cards, relics, three potions, quests and band soul rewards, and the Act 3 boss Viola. Simplified Chinese and English localization are included.

Subscribe through the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3803538264) and install RitsuLib. Development currently targets the game's beta branch. Settings include darker card text, attack sounds, Viola encounter mode (Disabled / Random / Locked), and whether vanilla characters can generate Arale cards through cross-character card effects.

For source builds, use .NET 9, Godot 4.5.1 .NET with Windows Desktop export templates, your local game assemblies, and RitsuLib 0.5.20. Configure the paths in `arale_mod.csproj` before building. A normal build **installs the mod into your game directory**; use the compile-only command above for validation without deployment. English text is under `arale_mod/localization/eng/`.

When reporting bugs, include reproduction steps, game/mod versions, multiplayer details, and `godot.log` from `%APPDATA%/SlayTheSpire2/logs/`.
