> 历史归档：此文件保留此前文档与阶段性说明，不代表当前实现。当前入口见 [项目文档](../PROJECT_DOCUMENTATION.md)。

# MaJiang Project Documentation

> 2026-10-24：请先阅读当前 [项目完善规划](../DEVELOPMENT_PLAN.md)、[规则与计分基准](../RULES_AND_SCORING.md) 和 [实现记录](../IMPLEMENTATION_LOG.md)。首轮规则已确认；P0 恢复基线与 P1 基础核心解耦及 20 项检查已完成，Unity 项目编译通过。新的房间、快照网络与完整规则状态机仍待实现。下文 2026-07 的架构、风险与开放问题属于历史记录，使用前须核对当前代码与实现记录。

> Last updated: 2026-07-12
>
> Current status: work in progress. Core Mahjong logic exists. The first rewritten network framework compiles, but Unity Editor multi-client playtesting is still required.

## 1. Project Basics

MaJiang is a Unity Mahjong game project written in C#.

- Engine: Unity 6000.0.64f1, according to the existing README.
- Main IDE: Visual Studio.
- Main Unity project directory: `MaJiangUnity/`.
- Main code directory: `MaJiangUnity/Assets/Scripts/`.
- Current implementation focus: core game logic and rewritten room-host network synchronization.
- UI and art content are not yet complete.

The project is currently small enough that code does not need a deep namespace hierarchy. Prefer using one project namespace for shared game code. Utility methods may live in the static `GlobalFunction` class when that matches the existing style.

## 2. Development Conventions

These conventions are project preferences and should be followed in future changes.

### 2.1 Code Style

- Use Allman braces.
- Avoid excessive abbreviations in type, method, property, and variable names.
- Prefer clear domain names over short local shorthand, especially in Mahjong rule code.
- Keep changes scoped to the feature or bug being worked on.
- Do not mix unrelated refactors into functional changes.

### 2.2 Comments And Documentation

- Visual Studio is the primary IDE, so member and declaration comments should use XML documentation comments.
- Use normal inline comments only when they explain non-obvious logic.
- After making a meaningful implementation or design change, update project documentation to record:
  - what changed,
  - why it changed,
  - the important implementation details,
  - any remaining limitations or assumptions.
- If a behavior is unclear or ambiguous, pause implementation and ask for a design decision before coding.

### 2.3 Unity Workflow

Some Unity operations may be difficult to perform safely outside the Unity Editor, especially scene wiring, inspector references, prefab setup, and visual verification.

When a change requires Unity Editor action, document what needs to be done and ask the project owner to perform or confirm it in Unity.

## 3. Current Architecture

The current code is organized into three main areas.

### 3.1 Back-end Scripts

Directory: `MaJiangUnity/Assets/Scripts/Back-end Scripts/`

This is the real core of the project. It contains Mahjong data models, rule logic, score calculation, match state control, packet structures, and network control.

### 3.2 Interfaces

Directory: `MaJiangUnity/Assets/Scripts/Interfaces/`

These files describe shared contracts for serializable objects, match state, player state, and network identity.

### 3.3 Network Scripts

Directory: `MaJiangUnity/Assets/Scripts/Network Scripts/`

These appear to be early placeholders or older experiments. Most active network implementation currently lives in `Back-end Scripts/NetworkControl.cs`, `Back-end Scripts/MatchServer.cs`, and `Back-end Scripts/Pack.cs`.

## 4. File Inventory

### 4.1 Interfaces

| File | Current purpose |
| --- | --- |
| `EnumDefine.cs` | Empty placeholder class. Can probably be removed later or replaced by actual shared enum definitions if desired. |
| `IByteable.cs` | Interface for objects that can serialize to and deserialize from byte arrays. Used by custom packet serialization. |
| `IMatchInformation.cs` | Public match state contract: discard piles, dora indicators, round, honba, player points, riichi flags, current stage, open sets, current tile, match settings, and riichi sticks. |
| `INetworkInformation.cs` | Contract for network sender identity: IP address, user profile, and task ID generation. |
| `IPlayerAction.cs` | Tiny action contract exposing a `PlayerAction` property. |
| `IPlayerInformation.cs` | Public player information contract: profile, player number, and current stage. |

### 4.2 Basic Domain Models

| File | Current purpose |
| --- | --- |
| `Tile.cs` | Mahjong tile model. Stores color, number, red dora flag, comparison, equality, string conversion, and 2-byte serialization. |
| `Group.cs` | Meld/group model. Represents sequences, triplets, pairs, kans, kita, source player, called tile, open/closed status, and 14-byte serialization. |
| `HandTile.cs` | Player hand model. Stores concealed hand tiles, current drawn tile, open groups, kita count, closed-hand status, cloning, and 96-byte serialization. |
| `Player.cs` | Player model. Stores profile, player number, hand, current stage, and 216-byte serialization. `DoAction` is currently a stub. |
| `UserProfile.cs` | User identity data. Currently stores user name and serializes to 96 bytes. |
| `MatchSettingData.cs` | Match rule settings: match type, thinking time, base time, starting point, finish point, minimum yaku, open tanyao, red dora, bankrupt, and local yaku flags. |
| `ClientStatu.cs` | Client status payload for network sync. Stores room number, network state, IP address, profile, and assigned player number. |
| `PlayerActionData.cs` | Serializable player action record. Stores acting player, target player, involved tiles, target tile, and action type. |
| `RonData.cs` | Win-calculation input data. Stores winner, winning tile, tsumo/ron status, riichi status, dora lists, kita count, closed-hand status, and winning groups. |
| `RonPoint.cs` | Win result data. Stores total fan, fu, base point, and yaku list. Also calculates yaku-only fan excluding dora. |

### 4.3 Rules And Calculation

| File | Current purpose |
| --- | --- |
| `GlobalFunction.cs` | Shared enums and utility methods. Includes wall generation, draw judgement, available action judgement, riichi judgement, furiten judgement, tenpai judgement, kokushi/seven-pair judgement, DFS grouping helpers, and byte-list conversion helpers. |
| `FanCalculator.cs` | Yaku, fan, fu, dora, yakuman, and base-point calculation. Contains many individual yaku detection functions. |
| `Xoshiro256StarStar.cs` | Random number generator using Xoshiro256**. Used for deterministic-style shuffling from a generated seed. |

### 4.4 Match And Network Control

| File | Current purpose |
| --- | --- |
| `MainMatchControl.cs` | Main match state container and local state transition engine. Handles match initialization, honba initialization, tile drawing, player action judgement, player action deduction, public match-state serialization, and honba progression. |
| `MatchServer.cs` | Unity-side authoritative server controller. Handles TCP listening, client data, room readiness, match start flow, init packets, player actions, claim resolution, heartbeat sync, reconnect sync, round end, and new honba start. This file is large and currently mixes network transport, game authority, state transitions, and scoring. |
| `NetworkControl.cs` | Unity-side client network controller. Handles local TCP client, packet receive loop, packet dispatch, init handling, action log replay, available action handling, status heartbeat, sync request, and player action sending. |
| `Pack.cs` | Fixed-size 1024-byte packet type. Defines packet header/footer, packet payload encoders/decoders, packet types, signal types, heartbeat ack, sync request, and sync response. |
| `NetworkException.cs` | Simple custom exception type for network errors. |

### 4.5 Trigger Helpers

| File | Current purpose |
| --- | --- |
| `TriggerBase.cs` | Generic trigger base with ID, target action, return result, running state, and single-use behavior. |
| `CountTriggerBase.cs` | Generic count-based trigger abstraction. |
| `FloatTriggerBase.cs` | Float counter trigger implementation. |
| `WatchTriggerBase.cs` | Watcher trigger that invokes a callback when a getter value satisfies a condition. |

### 4.6 Placeholder Network Scripts

| File | Current purpose |
| --- | --- |
| `Network Scripts/Client.cs` | Empty `MonoBehaviour` placeholder. |
| `Network Scripts/Server.cs` | Empty `MonoBehaviour` placeholder. Imports `UnityEditor`, so it should not be used in runtime builds as-is. |
| `Network Scripts/PackCoder.cs` | Mostly documentation comments and a `Ping` property. Active packet encoding currently lives in `Pack.cs`. |

## 5. Current Functional Overview

### 5.1 Implemented Or Partially Implemented

- Tile, group, hand, player, profile, action, match setting, and client status models.
- Custom byte serialization for core data structures.
- Basic wall generation and shuffling.
- Tenpai judgement for normal hands, seven pairs, and kokushi.
- Available self-action and claim-action judgement.
- Yaku and point calculation framework.
- Four-player match initialization.
- Honba/round progression logic.
- Basic TCP server/client connection flow.
- Fixed-size packet protocol.
- Server-authoritative action record broadcast.
- Heartbeat and reconnect synchronization concept.
- Early round-end scoring and draw handling.

### 5.2 Known Incomplete Areas

- Network module is incomplete and experimental.
- UI workflow is not implemented as a complete player experience.
- Scene wiring and inspector setup may not match final architecture.
- Room/lobby separation is not complete.
- Three-player Mahjong appears planned but not fully implemented.
- Some scoring and draw rules are incomplete or likely incorrect.
- Reconnect and action-log replay design needs tightening.
- `ProcessRoundEnd` exists but still needs rule validation.
- Some old placeholder files remain.

## 6. Important Design Notes

### 6.1 Network Design Direction

Current intent appears to be server-authoritative synchronization:

1. Client sends intended player action.
2. Server validates action.
3. Server applies action to authoritative match state.
4. Server broadcasts action record.
5. Clients replay action records locally.
6. Heartbeat/sync packets repair missed records.

This is a good direction for a Mahjong game, but the implementation currently needs cleanup around indexing, ack semantics, reconnect behavior, and state reset.

### 6.2 Packet Protocol

Packets are currently fixed at 1024 bytes.

Current layout in `Pack.cs`:

| Offset | Size | Meaning |
| --- | ---: | --- |
| 0 | 8 | Packet head: `0x02 HaxMajP` |
| 8 | 8 | Unix millisecond timestamp |
| 16 | 4 | IPv4 source address |
| 20 | 48 | Source user name |
| 68 | 4 | Version placeholder |
| 72 | 16 | MD5 placeholder/checksum |
| 88 | 4 | Packet type area, although only byte 88 is actively used |
| 92 | 4 | Task ID |
| 96 | 8 | Reserved |
| 104 | 912 | Payload |
| 1016 | 8 | Packet end: `0x03 EndPack` |

Important: checksum validation is not currently enforced by `IsLegalPack`; it only checks packet size and head/footer.

### 6.3 Serialization Direction

The project currently favors hand-written compact binary serialization instead of JSON.

This can work, but future changes should keep these rules:

- Every serialized type needs a stable byte size.
- Offsets must be documented when changed.
- List encoding must distinguish between valid zero/false values and end-of-list markers.
- Decode methods should honor the supplied `index`.
- Encoding and decoding tests should be added for each packet and serializable model.

## 7. Known Risks From Current Review

These are not yet fixed in this documentation step. They are recorded so future implementation can prioritize them.

### 7.1 Highest Priority

- `NetworkControl.Awake()` can null-reference because `SelfStatu` is initialized after using properties that dereference it.
- Player seating in `MainMatchControl.ServerFourMatchInit` may make `PlayerList` index and `PlayerNumber` disagree.
- Remaining tile count appears incorrect after initial dealing and may be double-decremented during draw replay.
- Low-fan score calculation uses `fu` before calculating `fu`.
- `RonData` constructor does not set `PlayerNumber`.
- Win legality currently checks total fan, including dora, rather than yaku-only fan.
- Draw judgement contains assignment inside predicate: `player => player = false`.
- `Group.StaticBytesTo` may duplicate called tile through the constructor.
- `Tile.ToString()` mutates red dora tile state.
- Client heartbeat/action-log update has off-by-one risks.

### 7.2 High Priority

- Boolean list serialization writes one byte but decode advances by four bytes and stops on false.
- `PlayerActionData` constructors and serialization are not null-safe in all paths.
- `ClientStatu.StaticBytesTo` ignores its `index` argument.
- Open groups are updated in `PlayerOpenSetList` but not always in `HandTile.OpenTileList`.
- Dead-wall draw noten penalty currently appears to scale incorrectly.
- Sanshoku doujun detection uses an impossible condition.
- Several equality operators do not handle null safely.
- Packet string and IP writes need length and address-family guards.
- `AddMD5` hashes the packet while the previous MD5 bytes are still present.
- Some async network methods use `async void` and rethrow exceptions in a way that can crash Unity.

## 8. Documentation Maintenance Rule

When a future implementation changes behavior, update this document or add a focused document under `docs/`.

Recommended additions:

- `docs/NETWORK_DESIGN.md` for the packet/session/reconnect model. This document now exists and should be kept in sync with network changes.
- `docs/RULES_AND_SCORING.md` for Mahjong rule decisions and local-rule choices.
- `docs/IMPLEMENTATION_LOG.md` for chronological implementation notes.
- `docs/TEST_PLAN.md` for replay cases and serialization tests.

For each meaningful change, record:

1. Date.
2. Files changed.
3. User-facing or gameplay behavior changed.
4. Internal design changed.
5. Tests or manual checks performed.
6. Remaining TODOs.

## 9. Open Questions Before Implementation

These should be clarified before large rewrites.

1. Should the first fully supported mode be four-player Japanese Riichi Mahjong only?
2. Should three-player Mahjong be postponed until four-player networking is stable?
3. Should packet serialization remain fixed-size binary, or is a simpler typed message format acceptable during early development?
4. Should reconnect restore full match state from authoritative snapshots, or replay action logs from the beginning of the current honba?
5. Should the server run inside one Unity client for now, or eventually become a separate headless/server process?
6. Which rule set should be the baseline: common Japanese online Riichi rules, or a custom local rule set?
