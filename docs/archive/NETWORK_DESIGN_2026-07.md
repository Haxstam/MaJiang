> 历史归档：此文件保留此前文档与阶段性说明，不代表当前实现。当前入口见 [项目文档](../PROJECT_DOCUMENTATION.md)。

# 网络框架设计

> 2026-10-24 阅读说明：本文保留此前网络设计和实施记录。当前路线见 [项目完善规划](../DEVELOPMENT_PLAN.md)，已确认规则见 [规则与计分](../RULES_AND_SCORING.md)，落地进度见 [实现记录](../IMPLEMENTATION_LOG.md)。P1 基础核心解耦已完成，新网络尚未接入。目标采用客户端本地计算操作、服务端共享规则校验及可见快照；下文旧方案不自动作为本次重构要求。

> 最后更新: 2026-07-11
>
> 当前阶段目标: 重写并稳定四人日麻的房主内置服务器网络框架。

## 1. 设计目标

当前网络框架以“服务端权威、客户端本地推演”为核心。

目标如下:

- 第一版只支持四人日麻。
- 当前阶段采用房主客户端内置服务器。
- 保留固定 1024 字节包结构，暂不改为变长包。
- 保留手动序列化和反序列化。
- 对局内操作通过日志同步。
- 普通房间交互通过明确消息提交，并通过周期性状态同步修正。
- 客户端只保存可见信息和自身私有信息。
- 服务端保存完整权威信息。
- 当日志差同步不足以恢复状态时，使用快照同步兜底。

## 2. 运行模式

当前阶段使用房主客户端内置服务器模式。

### 2.1 房主端

房主端同时拥有:

- 一个 `NetworkControl`，作为房主玩家自己的客户端网络控制器。
- 一个 `MatchServer`，作为当前房间的权威服务器。
- 一个客户端视角的 `MainMatchControl`，只保存房主玩家可见的信息。
- 一个服务端视角的 `MainMatchControl`，由 `MatchServer` 持有，保存完整对局信息。

### 2.2 普通客户端

普通客户端拥有:

- 一个 `NetworkControl`。
- 一个客户端视角的 `MainMatchControl`。

普通客户端不持有 `MatchServer`，也不持有完整牌山或其他玩家手牌。

### 2.3 后续可扩展方向

未来如果需要独立服务器，可以逐步将不依赖 Unity 的规则逻辑、序列化逻辑、网络协议逻辑从 `MonoBehaviour` 中拆出来。

当前阶段不做独立服务器拆分。

## 3. 类职责划分

### 3.1 `NetworkControl`

每个端都必须有一个 `NetworkControl`。

职责:

- 管理本机 TCP 客户端连接。
- 接收网络包并分发给对应处理逻辑。
- 发送普通房间请求，例如准备、取消准备、聊天。
- 发送对局内玩家操作。
- 保存客户端本地日志状态。
- 维护客户端可见的 `MainMatchControl`。
- 根据服务端下发的日志或快照更新本地状态。
- 向服务端请求日志差同步或快照同步。

`NetworkControl` 不应拥有服务端权威判断权。

### 3.2 `MatchServer`

只有房主端或未来独立服务器端拥有 `MatchServer`。

职责:

- 监听客户端连接。
- 管理房间内玩家状态。
- 分配玩家座位。
- 保存完整权威 `MainMatchControl`。
- 保存完整权威操作日志。
- 接收客户端操作并进行合法性判断。
- 对合法操作写入权威日志并广播。
- 对非法操作执行默认操作。
- 维护每个客户端的日志同步进度。
- 根据接收者生成可见日志或私有日志。
- 在日志差过大或客户端状态缺失时下发快照。

`MatchServer` 是唯一权威来源。

### 3.3 `MainMatchControl`

`MainMatchControl` 既可以作为服务端完整状态，也可以作为客户端可见状态。

二者内容不同。

服务端 `MainMatchControl`:

- 保存完整牌山。
- 保存所有玩家手牌。
- 保存全部公开状态。
- 保存当前等待操作上下文。
- 用于服务端合法性判断和结算。

客户端 `MainMatchControl`:

- 保存全部公共信息。
- 保存自身手牌。
- 保存其他玩家公开信息。
- 不保存其他玩家手牌。
- 不保存完整牌山。
- 根据接收到的可见日志进行本地推演。

## 4. 信息可见性

麻将对局中的隐藏信息主要包括:

- 牌山。
- 其他玩家手牌。

客户端不得通过普通日志、快照或状态同步获得其他玩家隐藏信息。

### 4.1 日志可见性

服务端保存完整权威日志。

发送日志时，服务端根据接收玩家生成对应版本。

示例: 玩家 A 摸到 `5p`。

- A 收到: 玩家 A 执行摸牌，摸到 `5p`。
- B/C/D 收到: 玩家 A 执行摸牌，但摸到的牌为空或占位。

其他公开操作，例如打牌、吃、碰、明杠、立直、荣和、流局，可以向所有玩家发送公开信息。

暗杠等操作如果后续需要隐藏部分牌面，应按规则单独设计可见内容。

### 4.2 快照可见性

快照也必须区分接收者。

服务端完整快照不得直接发给客户端。

客户端快照应包含:

- 公共对局信息。
- 自己的手牌。
- 自己当前摸牌。
- 其他玩家公开信息。
- 其他玩家手牌张数，如果 UI 或判断需要。
- 当前自己是否可操作，以及可操作列表。

## 5. 日志同步模型

对局内操作通过日志同步。

### 5.1 日志序号

日志序号统一使用 0-based action index。

- 第 1 条日志的序号是 0。
- 客户端保存“最后已应用日志序号”。
- 客户端初始值为 `-1`。
- 服务端为每个客户端保存该客户端最后确认的日志序号。

推荐命名:

- `LastAppliedActionIndex`: 客户端最后已应用日志序号。
- `LastConfirmedActionIndex`: 服务端记录的某客户端最后确认日志序号。
- `NextActionIndex`: 下一条要写入的日志序号，等于日志列表 `Count`。

### 5.2 正常同步流程

1. 当前玩家在客户端选择操作。
2. 客户端将操作发送给服务端。
3. 服务端判断操作是否合法。
4. 如果合法，服务端应用操作并写入权威日志。
5. 如果非法，服务端执行该阶段默认操作，并写入权威日志。
6. 服务端根据接收者生成可见日志。
7. 服务端将日志下发给客户端。
8. 客户端按序应用日志。
9. 客户端返回已应用日志序号。

### 5.3 日志差同步流程

当客户端发现自己缺失日志时:

1. 客户端向服务端发送同步请求，携带 `LastAppliedActionIndex`。
2. 服务端计算缺失范围。
3. 如果缺失范围可以通过日志补齐，服务端下发差值日志。
4. 客户端按序应用差值日志。
5. 客户端更新 `LastAppliedActionIndex`。

示例:

- 服务端已有日志 0 到 66。
- 客户端只应用到 62。
- 客户端请求从 63 开始的日志。
- 服务端发送 63 到 66。
- 客户端应用后更新为 66。

### 5.4 快照兜底流程

以下情况应使用快照同步:

- 客户端日志完全丢失。
- 客户端日志序号落后太多。
- 服务端已经丢弃旧日志。
- 客户端本地状态与服务端无法通过日志补齐。
- 客户端主动请求完整状态恢复。

快照同步流程:

1. 客户端发送快照同步请求。
2. 服务端根据该玩家生成客户端可见快照。
3. 服务端发送快照。
4. 客户端重建本地 `MainMatchControl`。
5. 客户端将 `LastAppliedActionIndex` 设置为快照对应的最新日志序号。

## 6. 普通房间交互

普通房间交互包括:

- 加入房间。
- 准备。
- 取消准备。
- 聊天。
- 房间状态同步。

这些操作不要求像对局操作一样严格逐条日志重放，但仍应使用明确消息提交给服务端。

推荐流程:

1. 客户端发送普通请求。
2. 服务端更新房间状态。
3. 服务端周期性广播房间状态。
4. 客户端用最新房间状态覆盖本地显示。

普通交互可以不做强确认，但服务端状态是最终结果。

## 7. 默认操作规则

当客户端超时或发送非法操作时，服务端执行默认操作。

### 7.1 鸣牌阶段

默认操作: 跳过。

示例:

- 当前玩家可以吃但不能碰。
- 玩家发送碰。
- 服务端视为跳过。

超时也视为跳过。

### 7.2 摸牌后操作阶段

默认操作: 摸切。

即打出刚摸到的牌。

如果玩家发送非法操作或超时，服务端生成摸切日志。

### 7.3 鸣牌后操作阶段

默认操作: 打出最右端手牌。

“最右端手牌”的具体含义后续实现时需要统一为一个明确规则，例如当前手牌列表最后一张。

如果玩家鸣牌后超时或发送非法切牌，服务端生成默认切牌日志。

## 8. 包协议约束

当前阶段继续使用固定 1024 字节包。

暂不改动整体解包方式。

后续网络重写可以整理包类型和 payload，但应保持:

- 固定包长 1024。
- 包头和包尾用于定位。
- 手动序列化。
- 对不同包类型使用明确 payload 布局。

需要注意:

- 所有 payload offset 必须记录。
- 编码和解码必须成对维护。
- 字符串写入必须限制长度。
- IPv4/IPv6 需要明确规则。
- 校验字段如果保留，生成和验证规则必须稳定。

## 9. 身份识别

当前阶段先使用 `UserProfile.Name` 作为玩家身份标识。

用途:

- 重连匹配。
- 房间内玩家识别。

限制:

- 同名玩家会冲突。
- 后续如果需要公网或正式联机，应引入唯一用户 ID。

## 10. 第一阶段实现边界

第一阶段只实现四人日麻网络框架。

包括:

- 房主启动服务器。
- 玩家连接房间。
- 准备/取消准备。
- 服务端开始对局。
- 服务端发送初始化信息。
- 客户端建立可见 `MainMatchControl`。
- 对局内操作提交。
- 服务端合法性判断。
- 服务端默认操作。
- 权威日志记录。
- 按玩家可见性下发日志。
- 客户端按日志推演。
- 日志差同步。
- 快照同步兜底。

暂不包括:

- 三人麻将。
- 独立服务器。
- 公网 NAT 穿透。
- 复杂账号系统。
- 观战。
- 回放系统。
- 反作弊。
- 完整 UI。

## 11. 建议重写顺序

推荐按以下顺序重写网络相关代码。

1. 整理网络枚举和包类型。
2. 修正基础序列化问题。
3. 明确 `PlayerActionData` 的公开版本和私有版本。
4. 实现日志序号模型。
5. 重写 `Pack` 中网络包编码/解码。
6. 重写 `NetworkControl` 的收包、发包、日志应用、同步请求。
7. 重写 `MatchServer` 的连接管理、状态广播、日志广播、默认操作。
8. 为 `MainMatchControl` 增加服务端完整快照和客户端可见快照。
9. 增加最小联机测试流程。
10. 更新本文档和实现日志。

## 12. 实现记录规则

每次修改网络框架后，需要更新文档。

至少记录:

- 修改日期。
- 修改文件。
- 修改目标。
- 包类型或序列化布局变化。
- 日志同步行为变化。
- 是否影响客户端可见信息边界。
- 是否需要 Unity Editor 操作。
- 已执行的验证方式。

## 13. 2026-07-12 网络框架实现记录

本次按当前设计完成第一版可编译网络框架重写。

修改文件:

- `MaJiangUnity/Assets/Scripts/Back-end Scripts/NetworkControl.cs`
- `MaJiangUnity/Assets/Scripts/Back-end Scripts/MatchServer.cs`
- `MaJiangUnity/Assets/Scripts/Back-end Scripts/Pack.cs`
- `MaJiangUnity/Assets/Scripts/Back-end Scripts/MainMatchControl.cs`
- `MaJiangUnity/Assets/Scripts/Back-end Scripts/PlayerActionData.cs`
- `MaJiangUnity/Assets/Scripts/Back-end Scripts/ClientStatu.cs`

### 13.1 已实现内容

- `NetworkControl` 重写为客户端网络控制器:
  - 连接服务端并发送入房信息。
  - 发送准备、取消准备、玩家动作、日志同步请求和快照请求。
  - 接收 `ClientStatuPack`、`MatchData`、`Init`、`AvailableAction`、日志包和快照包。
  - 维护 `LastConfirmedRecordIndex`。
  - 按服务端日志下标顺序应用可见日志。
  - 发现日志缺口时请求差量同步。
  - 无法通过日志恢复时请求完整可见快照。

- `MatchServer` 重写为房主权威服务端:
  - 监听 TCP 客户端连接。
  - 管理房间内玩家入房、准备和取消准备。
  - 四名玩家准备后启动四人南风战。
  - 服务端持有完整 `MainMatchControl`。
  - 服务端持有完整 `PlayerActionRecordList`。
  - 对玩家动作进行服务端合法性判断。
  - 非法动作或超时时执行默认动作。
  - 服务端写入权威日志后,按接收玩家生成可见日志。
  - 周期性发送心跳差量日志。
  - 按客户端确认下标处理同步请求。
  - 差量过大时发送完整可见快照。

- `Pack` 新增包能力:
  - `ActionLogPack`: 日志差量包。
  - `ActionLogPackDecode`: 日志差量解码。
  - `SnapshotRequestPack`: 客户端请求完整可见快照。
  - `SnapshotResponsePack`: 服务端发送完整可见快照。
  - `SnapshotRequestPackDecode` / `SnapshotResponsePackDecode`。
  - `PackType.SnapshotRequest`。
  - `PackType.SnapshotResponse`。

- `MainMatchControl` 网络关键修正:
  - 服务端开局时 `PlayerList` 按玩家编号下标放置玩家,避免座位编号和列表下标错位。
  - 服务端开局时写入 `MatchType`、东一局、0 本场、庄家和当前玩家。
  - `GetNextTile` 不再直接扣减剩余牌数,剩余牌数统一由 `Take` 日志演算扣减。
  - `ActionDeduce(Take)` 会写入当前玩家 `SingleTile`。如果该玩家不可见,服务端发出的可见日志中目标牌为空,客户端只推进公开状态。
  - 切牌、摸切后会清空 `SingleTile`。
  - 鸣牌和杠牌后同步更新 `HandTile.OpenTileList`。

- `PlayerActionData` 序列化修正:
  - 构造器改为根据传入的 `playerAction` 判断荣和/自摸。
  - `Tiles` 允许为空列表。
  - `TargetTile` 允许为空,用于其他玩家不可见的摸牌日志。
  - 反序列化时正确使用切出的 16 字节片段读取动作类型。

- `ClientStatu` 修正:
  - `StaticBytesTo` 现在会遵守传入的 `index`。

### 13.2 当前日志同步约定

- 服务端完整日志使用 0-based action index。
- 客户端使用 `LastConfirmedRecordIndex` 记录最后已应用日志。
- 客户端请求同步时发送 `LastConfirmedRecordIndex`。
- 服务端从 `LastConfirmedRecordIndex + 1` 开始发送差量。
- 单包最多发送 50 条差量日志。
- 差量超过单包上限时,服务端发送可见快照。
- 即时动作广播使用旧 `PlayerAction` payload 布局以保持兼容; 心跳和同步响应使用新增 `ActionLogPack` 布局。

### 13.3 当前可见性处理

- 服务端完整日志保留摸牌牌面。
- 摸牌动作发送给摸牌玩家本人时保留 `TargetTile`。
- 摸牌动作发送给其他玩家时将 `TargetTile` 置空。
- 快照中发送公共 `MainMatchControl.GetPublicBytes()` 和接收玩家自己的 `HandTile`。
- 当前快照不发送其他玩家隐藏手牌。

### 13.4 默认动作

- 摸牌后超时或非法动作: 摸切当前 `SingleTile`。
- 鸣牌响应阶段超时或非法动作: 跳过。
- 鸣牌后需要切牌但超时或非法动作: 打出手牌列表最后一张牌。

### 13.5 暂未完成或仍需后续确认

- 终局结算目前不在本次网络框架重写范围内,服务端会广播终局动作并进入结束状态。
- 断线重连身份目前仍以 `UserProfile.Name` 作为设计目标,但本次代码还没有实现同名冲突处理。
- 当前快照没有打包所有玩家档案,UI 如果需要显示所有玩家名字,后续应增加压缩后的公开玩家信息包。
- `AvailableActionPack` 仍沿用旧格式,后续如果动作数据增长,需要重新设计 payload。
- 房主端也应通过自身 `NetworkControl` 连接本地 `MatchServer`,Unity 场景中需要确保两者都挂载并配置。
- 未在 Unity Editor 内进行实际四客户端联机操作验证。

### 13.6 验证

已执行:

```powershell
dotnet build MaJiangUnity.sln --no-restore
```

结果:

- 0 个错误。
- 0 个警告。

## 14. 2026-07-12 调试输入修正记录

本次修正 `NetworkControl.HandleDebugInput()` 相关调试行为:

- 保持原设计: 按住 `clientKeyCode` 不松开,再按 `Z` 连接本地服务端。
- 保持原设计: 按住 `clientKeyCode` 不松开,再按 `X` 发送准备信号。
- `Z` 和 `X` 调试路径会输出 `Debug.Log`。
- `ConnectToServer()` 会输出连接目标、入房包发送成功或连接失败信息。
- `SendReadySignal()` / `SendUnReadySignal()` 会输出发送状态和未连接时的警告。

验证:

```powershell
dotnet build MaJiangUnity.sln
```

结果:

- 0 个错误。
- 0 个警告。

## 15. 2026-07-12 连接空值保护记录

本次修正连接阶段可能出现的空引用:

- `Pack` 构造函数现在会在 `SelfIPAddress` 为空时使用 `IPAddress.Loopback`。
- `Pack` 构造函数现在会在 `SelfProfile` 或 `SelfProfile.Name` 为空时使用 `"Unknown"`。
- `MatchServer.Awake()` 中 `testIPAddress` 无效时会退回 `127.0.0.1`, `testName` 为空时使用 `"Server"`。
- `NetworkControl.Awake()` 中 `testIPAddress` 无效时会退回 `127.0.0.1`, `testName` 为空时使用 `"Client"`。
- `MatchServer` 读包异常日志改为输出完整异常对象,便于定位真实调用栈。
- `UserProfile` 序列化允许空名称,反序列化会裁剪定长字符串中的 `\0`。

建议 Unity Inspector 调试配置:

- 房主端 `MatchServer.testIPAddress`: `127.0.0.1`。
- 房主端 `MatchServer.testName`: 任意非空名称,例如 `Server`。
- 每个 `NetworkControl.testIPAddress`: 本机调试可填 `127.0.0.1`。
- 每个 `NetworkControl.testName`: 四个客户端应使用不同名称。
- 每个 `NetworkControl.clientKeyCode`: 调试时每个客户端建议不同,避免输入冲突。

验证:

```powershell
dotnet build MaJiangUnity.sln
```

结果:

- 0 个错误。
- 0 个警告。

## 16. 2026-07-12 `UserProfile` 等号空值修正记录

连接后发送准备信号时,服务端在筛选 `ReadyClients` 的过程中会执行:

```csharp
client.UserProfile != null
```

由于 `UserProfile` 重载了 `==` / `!=`,该表达式会进入 `UserProfile.op_Inequality()`。
旧实现中 `op_Equality()` 直接访问 `a.Name` 和 `b.Name`,当其中一侧为 `null` 时会抛出 `NullReferenceException`。

本次修正:

- `UserProfile.operator ==` 先使用 `ReferenceEquals(a, b)` 判断同引用。
- 任意一侧为 `null` 时直接返回 `false`。
- 双方都非空时再比较 `Name`。

验证:

```powershell
dotnet build MaJiangUnity.sln
```

结果:

- 0 个错误。
- 0 个警告。

## 17. 2026-07-12 房间准备状态修正记录

问题:

- 服务端通过 `SignalType.Ready` 将玩家状态设置为 `Ready`。
- 客户端每秒发送 `ClientStatuPack` 心跳。
- 客户端在收到 `ReadyAck` 前可能仍处于 `ReadyWait`。
- 服务端旧实现会在房间阶段使用 `ClientStatuPack` 覆盖 `ClientNetworkStatu`。
- 因此已准备玩家可能被心跳降级回 `ReadyWait`,导致四人准备后没有触发开局。

本次修正:

- 房间阶段的准备/取消准备只由 `SignalType.Ready` 和 `SignalType.UnReady` 控制。
- `MatchServer.HandleClientStatusPack()` 在 `RoomWait` 阶段只记录调试日志,不再覆盖房间内准备状态。
- `TryStartMatchFromRoom()` 增加准备检查日志,会打印当前客户端数量和每个玩家的房间状态。
- 四名玩家均为 `Ready` 时输出 `"All players ready. Starting match."` 并调用 `StartMatch()`。

验证:

```powershell
dotnet build MaJiangUnity.sln
```

结果:

- 0 个错误。
- 0 个警告。

## 18. 2026-07-12 网络包收发/分发底座记录

本次将网络范围收窄为传输层底座:

- 连接。
- 发送指定 `Pack`。
- 接收字节流。
- 从接收缓存中提取固定 1024 字节包。
- 校验包头和包尾。
- 解析为 `Pack`。
- 按 `PackType` 分发给注册的处理方法。

### 18.1 新增/调整入口

`NetworkControl` 客户端侧:

```csharp
networkControl.ConnectToServer("127.0.0.1", 11111);
networkControl.SendPack(pack);
networkControl.RegisterPackHandler(PackType.Signal, HandleSignalPack);
networkControl.UnregisterPackHandler(PackType.Signal, HandleSignalPack);
networkControl.PackDispatcher.AnyPackReceived += HandleAnyClientPack;
```

客户端处理器签名:

```csharp
private void HandleSignalPack(Pack pack, NetworkControl networkControl)
{
    SignalType signalType = pack.SignalPackDecode();
}
```

`MatchServer` 服务端侧:

```csharp
matchServer.StartServer("127.0.0.1", 11111);
matchServer.SendPackToClient(clientData, pack);
matchServer.BroadcastPack(pack);
matchServer.RegisterPackHandler(PackType.UserProfile, HandleUserProfilePack);
matchServer.UnregisterPackHandler(PackType.UserProfile, HandleUserProfilePack);
matchServer.PackDispatcher.AnyPackReceived += HandleAnyServerPack;
```

服务端处理器签名:

```csharp
private void HandleUserProfilePack(Pack pack, MatchServer.ClientData clientData)
{
    UserProfile userProfile = pack.ConnectRoomPackDecode(out int roomNumber);
}
```

`Pack`:

```csharp
if (Pack.TryCreate(bytes, out Pack pack))
{
    Debug.Log(pack.PackType);
}
```

### 18.2 分发行为

- `AnyPackReceived` 只用于观察包,不会阻止内置处理。
- `RegisterPackHandler()` 注册的指定类型处理器会优先处理包。
- 如果指定类型处理器存在,分发后不会继续执行当前类中的内置 `switch` 处理。
- 如果没有注册指定类型处理器,会继续走现有内置处理。

这个规则允许后续逐步替换麻将对局逻辑:

1. 先注册某个 `PackType` 的新处理器。
2. 新处理器验证稳定后,再删除旧内置处理。
3. 未替换的包类型仍走旧逻辑。

### 18.3 接收缓存工具

`NetworkPackStreamUtility.ExtractPacks()` 负责:

- 查找 `Pack.PackEndBytes`。
- 根据包尾向前截取 1024 字节。
- 调用 `Pack.IsLegalPack()`。
- 对合法包回调 `onPackBytes`。
- 自动移动缓存中剩余未处理字节。

客户端和服务端现在都使用同一个拆包工具。

### 18.4 验证

```powershell
dotnet build MaJiangUnity.sln
```

结果:

- 0 个错误。
- 0 个警告。
