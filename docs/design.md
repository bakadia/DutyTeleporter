# DutyTeleporter 设计文档

面向国服卫月（Dalamud CN，API level 15）的地图传送插件，用途是**进入地图浏览风景**。

参考项目：[Dragonw233/XTeleport](https://github.com/Dragonw233/XTeleport)（清单内名称 `XTeleportOnline`）

---

## 1. 目标与边界

### 目标

1. 快速到达已解锁的以太之光 / 大水晶
2. 在当前地图内传送到指定坐标（观光点、隐藏机位）
3. 保存和管理观光点：按地图分组、命名、可导出分享
4. 以「自由探索」模式进入副本，无怪观光

### 明确的非目标

以下几点是刻意不做的，也是本项目和参考项目的根本区别：

- 不改移动速度 / 技能速度 / 咏唱速度
- 不做地下移动、穿墙
- 不做副本内战斗辅助、自动寻路
- 不做传送到队友

理由见 §8。

---

## 2. 参考项目分析

### 2.1 仓库构成

| 目录 | 内容 | 是否开源 |
| --- | --- | --- |
| `Teleport/` | 插件主体，约 30 个 `.cs` | 是 |
| `Teleport.Protected/` | 指针扫描、传送包构造 | **否**，只发布了 csproj |
| `CodeCreater/` | WinForms 激活码生成器 | 是（授权工具） |

作者署名 Uiharu / NiGuangOwO / Tabris，描述自称"基于初春佬的 XTP 项目更新修复版"，即上游是「初春」的 XTP 项目。

### 2.2 它的传送机制

`Teleport/StaticUtils.cs` 里能读出的部分，一共三条路径：

1. **地面** — 扫描客户端的 setPosition 函数指针（`TeleportProtected.ScanSetPositionPointer`），
   做成 delegate 调用：`SetPos(player.Address, x, y, z)`
2. **飞行 / 潜水** — 另一套指针（`ScanFlyDivePointer`）
3. **潜水传送包** — 构造并发送带 opcode 的数据包（`TeleportProtected.CreateDiveTpPacket`），用于水下区域

调度由 `TeleportSmartInZone` 负责，按当前区域决定走哪条；`DiveTpTerritoryHelper.ShouldUseDivePacket` 判断是否需要走潜水包。

两个值得学的工程细节：

- **落点修正**（`TryGetNearestVerticalCollision`）— 在目标点向上、向下各打一次射线，找可站立的地面，
  避免传进地面里或者悬在半空。这个不做的话体验会非常差。
- **状态闸门**（`CanTeleportInCurrentState`）— 执行前检查是否在战斗中、当前区域类型是否允许，
  不允许就直接拒绝。既是安全考虑也是合规考虑。

### 2.3 只能参考，不能抄

- 仓库**没有 LICENSE**，默认保留所有权利，直接抄代码会有授权问题。
- 核心实现（指针扫描、传送包构造）在 `Teleport.Protected`，**只发布了 csproj 没有源码**。
  也就是说最关键的低层部分你本来也拿不到。
- 工程配了 `dotNET_Reactor`、`NekoPara.OOD` 加壳，以及 `MachineCodeGenerator` + `ActivationForm`
  的机器码授权。这是商业插件。
- 它的功能面比"观光"宽得多，`Configuration.cs` 里有 `UseSkillSpeed` / `UseSpellSpeed`（默认 400）、
  `SpeedRefreshInterval`、`UseUndergroundMove`、`PartyListTeleportButtons` 等。
  **照搬功能集会直接把插件变成作弊工具**，也会让它过不了审核。

结论：参考它的**架构分层和状态判断思路**，实现自己写。

---

## 3. 传送技术路线

### 3.1 以太之光传送（先做这个）

公开且稳定，`FFXIVClientStructs` 已经提供入口：

```csharp
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

var agent = AgentTelepotTown.Instance();
agent->TeleportToAetheryte(index);
```

注意参数是**索引不是 ID**：`AgentTelepotTownData._entries`（最多 64 条 `AetheryteEntry`）里每条含
`AetheryteId`、`TerritoryTypeId`、`PlaceNameId`、`MapId`、`IsLocked`、`IsUnusable`、`IsCurrent`。
流程是：先枚举 entries 过滤出可用的，拿到索引再调用。

这条路走的是游戏自身的传送流程，最安全也最容易过审。

### 3.2 当前地图内坐标传送

需要写玩家位置，属于内存操作：

- 用 `FFXIVClientStructs` 拿玩家对象的地址
- setPosition 入口需要自己找；公开项目 vnavmesh 有位置写入的实现，可以对照阅读
- 必须做落点修正（射线找地面）和战斗状态拦截

风险明显上升，建议做成**默认关闭**的开关。

---

## 4. 副本进入与自由探索

这一节是重点，因为它决定了插件的天花板在哪。

### 4.1 自由探索是什么，限制写在哪里

「自由探索」（Explorer Mode）是官方提供的**无怪进入副本**的功能，用来观光和截图。

它的资格不是服务端的临时策略，而是**客户端数据里的逐副本布尔标志**：
`ContentFinderCondition` 表的 `AllowExplorerMode` 列。

对国服客户端数据（`thewakingsands/ffxiv-datamining-cn`）实际统计的结果：

| 指标 | 数值 |
| --- | --- |
| `ContentFinderCondition` 条目总数 | 1119 |
| `AllowExplorerMode = True` | **103**（9.2%） |
| 其中 `ContentType = 2`（四人副本） | **103 / 103** |
| 其中 `ContentMemberType = 2`（轻锐小队） | **103 / 103** |

也就是说：**开放自由探索的 103 个副本，全部是四人副本；反过来，全部四人副本都开放。**
两个集合完全相等（103 == 103），不是人工挑出来的子集。

再按名称核对大型内容，结果一律是 0：

| 内容类型 | 条目数 | 开放自由探索 |
| --- | --- | --- |
| 零式 | 68 | 0 |
| 绝境战 | 7 | 0 |
| 万魔殿 | 24 | 0 |
| 巴哈姆特 | 20 | 0 |
| 亚历山大 | 25 | 0 |
| 欧米茄 | 25 | 0 |
| 伊甸 | 24 | 0 |
| 讨伐 / 歼殛 / 歼灭 | 16 / 26 / 50 | 0 |

等级覆盖是完整的 15–100（`1-50`: 31 个，`51-60`: 18，`61-70`: 15，`71-80`: 13，`81-90`: 13，`91-100`: 13），
所以四人副本这条线是齐全的，缺的是**非四人副本**。

### 4.2 客户端怎么表达

好消息：自由探索在客户端是**一个可写的开关**，和"解除人数限制"（unsync）在同一个结构体里：

```csharp
// FFXIVClientStructs.FFXIV.Client.Game.UI.ContentsFinder
[FieldOffset(0x19)] public bool IsUnrestrictedParty;
[FieldOffset(0x1C)] public bool IsExplorerMode;   // ← 自由探索
```

AutoDuty 就是这么用的——它写 `IsUnrestrictedParty`，然后驱动游戏自己的 UI agent 完成排队：

```csharp
ContentsFinder.Instance()->IsUnrestrictedParty = true;   // AutoDuty 的写法
AgentDawn.Instance()->RegisterForDuty();                 // 亲信战友
AgentDawnStory.Instance()->RegisterForDuty();            // 剧情模式
AddonHelper.FireCallBack(addonContentsFinderConfirm, true, 8);  // 排本确认
```

所以我们的实现思路是：**设 `IsExplorerMode = true`，再走同样的 DF agent 流程排队**。
全程不碰指针、不发自定义包，只是替玩家点官方按钮。这条路的实现难度不高。

### 4.3 为什么"全副本地图"做不到

这里要泼一盆冷水，因为这是整个需求的天花板：

- 自由探索**不是**一个通用的"无怪进本"机制，它是四人副本专有的实例类型。零式、绝境、讨伐、
  大型任务在服务端根本没有对应的无怪实例。
- 进本是**服务端权威**的：客户端发出请求，服务端创建实例并把你放进去。
  客户端**无法自己造一个实例**——这正是 AutoDuty 全部走 UI agent 而不是自己造房间的原因。
- 所以"动指针自己启动"这条路走不通：不是客户端不给启动，而是没有本地可启动的东西。
  强行绕过只会让你和服务端状态不一致，结果是掉线或者进到一个空壳。

结论：**四人副本 = 103 个，这个数字就是天花板。** 想进零式/绝境/大型任务观光，
客户端侧没有合法路径。

### 4.4 活动地图是另一个问题

活动地图（季节活动、限时活动的地图）**根本不在 `ContentFinderCondition` 里**——它们不是副本，
是独立区域。自由探索对它们完全不适用。

进入这类区域需要触发**区域切换**（zone change），这比副本进入更难也更危险：
区域切换由服务端下发，客户端自己切会和同步状态脱节。

建议：这一类明确列为**不做**。

### 4.5 值得做的实验

上面 4.3 的结论里有一个点只能实测才能确认，而且成本很低：

> 给一个 `AllowExplorerMode = False` 的副本（比如某个讨伐战）设 `IsExplorerMode = true`，
> 然后正常排队，看服务端什么反应。

三种可能：
1. 直接报错拒绝 —— 墙是硬的，4.3 的结论成立，需求到此为止。
2. 排队成功但仍然带怪 —— 说明标志位被忽略，等于没做。
3. 排队成功且无怪 —— 那就有意外收获，天花板比数据看起来高。

**这个实验值得先做**，因为它只改一行标志位、走官方排队流程，风险极低，
但结论直接决定后面要不要投入。在没跑过之前，不要按第 3 种可能做规划。

---

## 5. 数据来源

| Sheet | 用途 |
| --- | --- |
| `ContentFinderCondition` | 副本列表、`AllowExplorerMode` 资格、等级、`TerritoryType` |
| `TerritoryType` | 地图（区域），传送目标的基本单位 |
| `Aetheryte` | 以太之光，含 `Territory`、`Map`、`AethernetName` |
| `Map` | 地图，含 `PlaceName`、尺寸和偏移 |
| `PlaceName` | 地名显示 |

坐标统一存**世界坐标**（X/Y/Z），显示时再换算成地图坐标——换算要用 `Map` 里的尺寸和偏移，
Dalamud 和 ECommons 都有现成工具。

参考项目的 `TPList.cs` 数据模型可以借鉴思路：

```
TPList { MapId, MapName, List<TP> TPs, UseQuickWindow }
TP     { Name, X, Y, Z }
```

整体序列化成 JSON 存进配置。注意它的 `MapId` 字段实际存的是 `TerritoryType` 的 row id，
取名有点误导，我们自己写的时候注意区分 `Map` 和 `TerritoryType` 这两张表。

---

## 6. 依赖

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| `Dalamud.NET.Sdk` | 15.0.0 | 已自动引入 Dalamud / FFXIVClientStructs / Lumina / Newtonsoft.Json |
| `ECommons` | 3.2.1.12 | 可选。参考项目和 AutoDuty 都在用，提供大量便利封装 |

---

## 7. 国服环境注意事项

- **dev 目录是 `%appdata%\XIVLauncherCN\addon\Hooks\dev\`，不是 `XIVLauncher`。**
  国服启动器的 `RoamingPath` 写死了 `XIVLauncherCN`，而官方 `Dalamud.NET.Sdk` 的默认 Windows
  路径是 `XIVLauncher`。所以国服下必须用 `DALAMUD_HOME` 覆盖，否则构建直接报
  `Dalamud installation not found`。参考项目也是因此手写了 `DalamudLibPath`。
- API level 15，目标框架 `net10.0-windows`。

---

## 8. 合规与风险

按风险从低到高排列：

| 做法 | 风险 | 说明 |
| --- | --- | --- |
| 以太之光传送 | 低 | 走游戏自身流程 |
| 自由探索进四人副本 | 低 | 官方功能，只是替玩家点按钮 |
| 当前地图内坐标传送 | 中 | 内存写位置，ToS 灰色地带 |
| 向服务端发包强制进本 | **高** | 包伪造，封号风险，审核必拒 |
| 客户端强制区域切换 | **高** | 状态脱节，可能掉线/崩客户端 |

国服插件库审核会看是否含战斗相关功能。本插件**不含**战斗功能，这是有利的一点；
但"传送"这个类别本身容易被误判，建议：

- 只做以太之光 + 自由探索 + 当前地图内坐标传送
- 战斗状态下禁止传送（`CanTeleportInCurrentState` 的闸门思路要保留）
- 在插件描述里写清楚用途是观光

§4.4 的活动地图和 §4.3 的非四人副本，建议直接不做——收益不确定，风险确定。

---

## 9. 里程碑

1. **先跑 §4.5 的实验** —— 一行标志位，决定后面所有规划
2. 骨架跑通，能在游戏里开窗口
3. 以太之光列表 + 传送（§3.1）
4. 自由探索进四人副本（§4.2）
5. 观光点保存 / 命名 / 按地图分组
6. 当前地图内坐标传送（§3.2，默认关闭）
7. 落点修正 + 战斗状态拦截
8. 提交到国服插件库 `testing/live`

---

## 10. 开放问题

- §4.5 的实验结果如何？这是决定性的。
- 只靠四人副本 103 张图 + 以太之光，够不够满足观光需求？如果不够，是不是该换个思路
  （比如接受带怪进入，用无敌/隐身绕过）？注意后者会显著改变插件的风险定位。
- 观光点数据要不要做成可分享的 JSON / 在线仓库？
- 水下区域要不要支持？参考项目为它单独做了传送包，工作量不小。
