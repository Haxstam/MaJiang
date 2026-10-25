# 麻将项目网络模块实现记录

> 2026-10-24 阅读说明：本文件是早期实现存档。当前范围与重构建议见 [项目完善规划](docs/DEVELOPMENT_PLAN.md)，规则核实见 [规则与计分基准](docs/RULES_AND_SCORING.md)。历史完成记录不等于当前代码已经通过功能验收。

> 存档日期: 2026-05-01
> 项目路径: G:\Documents\GitHub\MaJiang\MaJiangUnity

---

## 一、项目概况

开源联网麻将项目，Unity 6000.0.64f1 + C# (.NET)，基本麻将逻辑已实现，网络模块施工中。

开发进度：基本逻辑 ✅ → 网络交互 🔄 → 界面实现 ⬜ → 美术内容 ⬜

---

## 二、整体架构

```
MatchServer (服务端权威)
  ├── TCP Listener (最多4客户端)
  ├── ServerStateUpdate() ─ 每帧：心跳定时 + ServerStageUpdate()
  └── MainMatchControl ─ 权威对局状态
        └── PlayerActionRecordList ─ 操作日志(唯一真相源)

NetworkControl (客户端)
  ├── PackSolve() ─ 接收包分发
  ├── ActionDeduce() ─ 本地重放日志保持同步
  └── SendPlayerAction() ─ 发送操作给服务端(不本地预演)
```

### 核心设计原则

- **操作日志驱动同步**: `PlayerActionRecordList` 是对局唯一真相源
- **服务端权威**: 客户端发送操作→服务端校验→广播→客户端重放
- **固定包协议**: 1024字节固定大小包，STX/ETX标记，MD5校验

---

## 三、网络同步方案

### 对局外 (Lobby/RoomWait)
```
Server ◄──ClientStatuPack(每秒)──► Client
       (RoomNumber, NetworkStatu, IPAddress, PlayerNumber)
```

### 对局内 (ServerInitWait ~ ServerEndWait)
```
实时: Server ──BroadcastPlayerAction()──► Client ──重放→SendHeartbeatAck()──► Server
补偿: Server ──HeartBeatPack(每秒差值)──► Client ──重放→SendHeartbeatAck()──► Server
```

- **心跳 = 差值同步**: 服务端每秒检查 `clientData.RecordStamp`，发送缺失的操作
- **ack 即确认**: 客户端发送 `RecordIndex-1`(最后确认的0-based索引)
- **RecordStamp 初始值 = -1** (无记录已确认)

---

## 四、修改的文件清单

### 1. `Pack.cs`
| 新增方法 | 说明 |
|----------|------|
| `HeartbeatAckPack(int lastConfirmedIndex)` | 编码: PackType=Acknowledge + SignalType=HeartbeatAck + RecordIndex(4 bytes at 108) |
| `HeartbeatAckPackDecode()` | 解码: 返回客户端已确认的最后日志序号 |

### 2. `NetworkControl.cs`
| 变更 | 位置 | 说明 |
|------|------|------|
| `SignalType.HeartbeatAck` | 枚举 | 新增信号类型 |
| `clientStatuTimer` + `ClientStatuInterval` | 字段 | 对局外心跳定时器(1秒) |
| `LastReceiveTime += Time.deltaTime` | Update() | 修复: 累积断线计时 |
| `SendClientStatu()` | 新方法 | 对局外发送ClientStatuPack |
| `SendHeartbeatAck()` | 新方法 | 发送心跳确认(携带RecordIndex-1) |
| PlayerAction/HeartBeatPack 处理后自动ack | PackSolve() | 接收操作后立即确认 |

### 3. `MatchServer.cs`
| 变更 | 位置 | 说明 |
|------|------|------|
| `heartbeatTimer` + `HeartbeatInterval` | 字段 | 心跳定时器(1秒) |
| `ServerStateUpdate()` | 新方法 | 每帧: 心跳定时 + 状态机更新 |
| `ServerHeartbeat()` | 新方法 | 对局外发ClientStatu / 对局内发差值日志 |
| HeartbeatAck处理 | PackSolve() | 更新客户端RecordStamp |
| `RecordStamp = -1` | ClientData | 初始值改为-1(无记录确认) |
| 日志清空 | StartMatchFlow() | 新对局清零PlayerActionRecordList和所有RecordStamp |
| `HandleSyncRequest` 索引修正 | | firstUnconfirmed = lastConfirmedIndex + 1 |

---

## 五、包协议结构 (1024 bytes fixed)

| 偏移 | 大小 | 内容 |
|------|------|------|
| 0 | 8 | 包头: `\x02HaxMajP` |
| 8 | 8 | Unix毫秒时间戳 |
| 16 | 4 | 来源IPv4地址 |
| 20 | 48 | 来源用户名(UTF-8, max 12 chars) |
| 68 | 4 | 版本号(暂空) |
| 72 | 16 | MD5校验 |
| 88 | 4 | 包类型(PackType) |
| 92 | 4 | TaskID(已废弃) |
| 96 | 8 | 留空 |
| 104 | 912 | 数据负载 |
| 1016 | 8 | 包尾: `\x03EndPack` |

### HeartbeatAck包 (客户端→服务端)
- PackType: `Acknowledge` (5)
- [104]: `SignalType.HeartbeatAck`
- [108-111]: `int lastConfirmedIndex` (客户端最后确认的0-based日志索引)

---

## 六、状态机

### 服务端状态
```
Offline → RoomWait → ServerStartWait → ServerInit → ServerInitWait
    → ServerActionWait → ServerClaimWait → ServerEndWait → (循环)
```

### 客户端状态
```
Offline → Free → Connecting → InRoom → Ready → StartWait
    → RoundWait / DiscardStage / ActionStage / EndStage
```

---

## 七、待完成事项

1. `ProcessRoundEnd()` - 点数结算、连庄/下庄判断、终局判断
2. 房间/大厅分离
3. 对局类型由房主通过 `MatchSettingData` 配置
4. 击飞(HaveBankrupt)判断
5. UI界面实现
6. 美术内容
7. `EnumDefine.cs` - 当前为空占位类

---

## 八、关键数据类序列化大小

| 类 | 大小(bytes) |
|----|-------------|
| Tile | 2 |
| Group | 14 |
| PlayerActionData | 16 |
| HandTile | 96 |
| UserProfile | 96 |
| ClientStatu | 128 |
| Player | 216 |
| MainMatchControl(public) | 512 |
| MatchSettingData | 16 |
