# DutyTeleporter

基于 **卫月（Dalamud）国服版** 的插件骨架，由 **蓝月（XIVLauncher CN）** 加载运行。

## 环境要求

| 项目 | 版本 |
| --- | --- |
| 国服卫月（Dalamud CN） | 15.0.3.8，API level **15** |
| .NET SDK | **10.0.0** 或更高（`net10.0-windows`） |
| Dalamud.NET.Sdk | 15.0.0（NuGet 自动还原） |
| 运行/测试 | Windows + 国服客户端 + 蓝月 |

## 构建

插件编译时需要引用本机卫月 dev 目录里的 `Dalamud.dll` 等文件，所以**先装好蓝月并至少启动过一次游戏**，让卫月把 dev 文件铺开。

`Dalamud.NET.Sdk` 会按平台自动找这个目录：

- Windows：`%appdata%\XIVLauncher\addon\Hooks\dev\`
- Linux：`~/.xlcore/dalamud/Hooks/dev/`
- macOS：`~/Library/Application Support/XIV on Mac/dalamud/Hooks/dev/`

> ⚠️ **国服下上面这个 Windows 路径是错的。** 国服启动器把配置根目录写成
> `%appdata%\XIVLauncherCN`（见 `ottercorp/FFXIVQuickLauncher` 的 `src/XIVLauncher.Common/Paths.cs`），
> 所以卫月的 dev 目录实际在 `%appdata%\XIVLauncherCN\addon\Hooks\dev\`。
> SDK 只认 `XIVLauncher`，找不到就直接让构建失败。**国服开发必须用 `DALAMUD_HOME` 指过去：**
>
> ```powershell
> $env:DALAMUD_HOME = "$env:appdata\XIVLauncherCN\addon\Hooks\dev\"
> ```
>
> 参考项目 TeleportEX 也是因为这个才手写了 `DalamudLibPath`。

路径不一致时用环境变量覆盖（`DALAMUD_HOME` 优先级最高）：

```bash
export DALAMUD_HOME="/path/to/dalamud/Hooks/dev/"
```

> 这个目录**必须存在**，否则构建会直接失败：
> `Dalamud.NET.Sdk: Dalamud installation not found at <路径>`。
> 插件编译时要引用该目录下的 `Dalamud.dll`、`FFXIVClientStructs.dll` 等，
> SDK 已经自动配好了全部引用，不需要手写 `<Reference>`。

### 在 macOS 上编译

卫月注入的是 Windows 游戏客户端，所以 macOS 上**跑不了也测不了**，但**可以编译**，用来查语法和类型错误。
做法是把 Windows 上那份 dev 目录整个拷过来：

```powershell
# Windows（PowerShell）：打包
Compress-Archive "$env:appdata\XIVLauncherCN\addon\Hooks\dev" dalamud-dev.zip
```

```bash
# macOS：解压后指向它
export DALAMUD_HOME="$HOME/dalamud-dev/"
dotnet build -c Release -p:EnableWindowsTargeting=true
```

`-p:EnableWindowsTargeting=true` 是必须的，因为目标框架是 `net10.0-windows`。


## 本地调试

卫月**没有**默认的 `devPlugins` 目录，开发版插件要手动注册：

1. 在游戏内 `/xlsettings` → **Experimental** → **Dev Plugin Locations**，添加你的构建输出目录
   （指向文件夹会尝试加载里面所有 DLL，也可以直接指向 `DutyTeleporter.dll`）。
2. 该位置对应的开关必须是启用状态，同时确保 **Dev Mode** 已打开。

注意：从某个版本起卫月要求 dev 插件**必须带 manifest**，也就是 `DutyTeleporter.json` 要和 DLL 放在一起。
`Dalamud.NET.Sdk` 在构建时会自动生成它，所以直接把 `bin/x64/Release/` 作为加载目录即可。

改完代码重新 `dotnet build`，在游戏内 `/xldev` → 插件管理里重新加载，不必重开游戏。

### 打断点

游戏默认有反调试保护。先用 `/xldev` → **Dalamud → Enable AntiDebug** 关掉（设置会持久化），
再用 IDE 附加到 FF14 进程即可。

### 想调用游戏内部结构

不用手动加引用。`Dalamud.NET.Sdk` 已经从卫月 dev 目录自动引入了
`FFXIVClientStructs`、`Lumina`、`Lumina.Excel`、`Newtonsoft.Json`、`Serilog` 等，
直接 `using FFXIVClientStructs.FFXIV.Client.Game;` 就能用。

网上不少教程还在教你在 csproj 里手写 `<HintPath>`，那是 SDK 出现之前的做法，现在加了反而可能冲突。



## 发布到国服插件库

1. 把插件推到一个公开可克隆的 Git 仓库（GitHub / GitLab / 自建都行）。
   第一次 `dotnet build` 后会生成 `packages.lock.json`（SDK 已开启 `RestorePackagesWithLockFile`），
   **必须和 csproj 一起提交**，国服审核要求两者同步。同时记得把 csproj 里的 `RepoUrl` 改成真实仓库地址。
2. 在 `ottercorp/DalamudPluginsD17` 提 PR，新建 `testing/live/DutyTeleporter/manifest.toml`：

   ```toml
   [plugin]
   repository = "https://github.com/bakadia/DutyTeleporter.git"
   commit = "<完整的 40 位 commit hash>"
   owners = ["bakadia"]
   project_path = "DutyTeleporter"
   changelog = "首次提交"
   ```

3. 同目录放 `images/icon.png`（64x64 ~ 512x512，本仓库已生成一张 128x128 占位图）。
4. Plogon 会自动构建，通过审核后发布到 `ottercorp/PluginDistD17` 的 `stable/` 频道。

新插件建议先进 `testing/live`，稳定后再转 `stable`。

## 目录结构

```
DutyTeleporter.slnx                  # 解决方案
DutyTeleporter/                      # 插件项目
├── DutyTeleporter.csproj            # 项目 + 插件元数据（自动生成 plugin.json）
├── Plugin.cs                        # 入口：IDalamudPlugin 的构造与 Dispose
├── Configuration.cs                 # 存档，实现 IPluginConfiguration
├── Windows/MainWindow.cs            # 主窗口
├── Windows/ConfigWindow.cs          # 设置窗口
├── Windows/ExperimentWindow.cs      # 实验用：强制 IsExplorerMode（有结论后可删）
└── images/icon.png                  # 插件图标
docs/design.md                       # 设计文档（目标、参考项目分析、技术路线、合规）
docs/experiment-explorer-mode.md     # 自由探索服务端行为验证协议
docs/explorer-mode-duties.md         # 103 个开放自由探索的副本清单
```

## 参考

- 国服卫月：https://github.com/ottercorp/Dalamud
- 蓝月启动器：https://github.com/ottercorp/FFXIVQuickLauncher
- 开发 FAQ（中文）：https://ottercorp.github.io/faq/development
- API 文档：https://goatcorp.github.io/Dalamud/api/index.html
- 官方模板：https://github.com/goatcorp/SamplePlugin
