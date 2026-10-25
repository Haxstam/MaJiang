# 项目文档

> 更新：2026-10-25。P1–P4及P5后端清理/恢复检查已实现；现有牌桌与单人四座调试获得作者初步测试反馈“没有问题”。后续以 [当前开发起点](CURRENT_BASELINE.md) 继续，网络78项通过，跨机器与完整专项人工验收尚未完成。

## 1. 当前基准

- Unity 6000.0.64f1，C#，Unity生成工程目标为netstandard2.1。
- 首版为局域网四人日麻东南战，房主内置服务端，手动输入IP和端口。
- 规则以用户已确认的雀魂参照预设为准，保留参数化能力。
- TCP、手写二进制、固定1024字节帧。当前协议v5，拒绝旧版本。
- 正常使用可见裁定日志，检查点用于初始化与恢复；客户端计算操作菜单、服务端复用共享规则最终校验。

先读 [当前开发起点](CURRENT_BASELINE.md) 和 [单人调试](SOLO_DEBUG.md)，再按需读 [开发规划](DEVELOPMENT_PLAN.md)、[规则基准](RULES_AND_SCORING.md)、[网络设计](NETWORK_DESIGN.md)、[状态机](STATE_MACHINE.md) 和 [实现记录](IMPLEMENTATION_LOG.md)。旧资料见 [历史项目文档](archive/PROJECT_DOCUMENTATION_2026-07.md)，旧风险列表必须结合实现记录核对。

## 2. 开发约定

- 使用Allman大括号，名称清晰，避免过度缩写。
- Visual Studio为主要IDE；成员和声明说明使用XML文档注释，行内注释解释不明显的逻辑。
- 修改围绕当前功能，不混入无关重构；已授权的旧网络清理按规划推进。
- 小项目无需深层命名空间；共享游戏代码优先沿用MaJiangLib及GlobalFunction。
- 规则或设计存在无法由代码、已确认决定解决的歧义时，给出具体位置与最小牌例向作者确认。
- 有意义的实现或设计变更同步记录目的、文件、行为、布局、验证结果、剩余限制和Unity操作。
- 作者明确Unity操作验证由本人完成；代理不控制桌面代为验证。需要场景、Inspector、预制体或视觉验收时列出编辑器步骤；不能把CLI编译当作Unity运行验证。

## 3. 目录与职责

| 位置 | 内容 |
| --- | --- |
| `MaJiangUnity/Assets/Scripts/Back-end Scripts/` | 原领域模型、规则、计分与MainMatchControl；两个Unity网络入口保留原GUID |
| `MaJiangUnity/Assets/Scripts/Networking/` | 新协议、TCP连接、纯房间状态机、客户端/服务端会话 |
| `MaJiangUnity/Assets/Scripts/Presentation/` | 可见牌桌、独立预览、单人四座调试控制器 |
| `MaJiangUnity/Assets/Scripts/Interfaces/` | 核心数据与序列化接口 |
| `MaJiangUnity/Assets/Scripts/Network Scripts/` | 旧场景引用的两个空组件，待编辑器清理 |
| `tests/` | 直接编译真实核心源文件的独立检查、真实TCP回环检查、Unity CLI构建适配 |
| `docs/` | 当前规划、已确认规则、协议、验收与实施记录 |
| `docs/archive/` | 历史设计，不能作为当前行为的依据 |

MainMatchControl接收已按座位排序的玩家，不接收Socket、Pack或ClientData。AuthoritativeMatchMachine是唯一操作推进入口，MatchSession管理一次结算与换局。MatchActionRules通过HandAnalysis调用统一RiichiScoring判断役；RoundSettlement负责纯支付和终局判定。旧静态计分器已删除，详见 [计分与整场](SCORING_AND_MATCH.md)。

## 4. 当前能做什么

- 无Unity场景执行发牌、摸切、岭上取牌及基础数据回归检查。
- 四个真实TCP客户端入房、准备、取消准备，由房主手动开局。
- 同名玩家独立识别，恢复原座位并使旧连接失效。
- 房间公开状态同步、心跳、超时、异常连接隔离和停止后重用端口。
- 纯服务端开局后发送本人起手牌及庄家首摸；客户端可见日志按序应用、补齐及检查点恢复。
- 玩家命令的会话认证、本局/窗口校验、精确赤牌选择、去重与非法选择默认操作。
- 吃碰杠、立直、自摸/多人荣和、国士抢暗杠、加杠抢杠及杠宝牌时点。
- 每步5秒与本局共享20秒长考，断线继续默认操作；振听、已答复与个人期限按日志/检查点恢复。
- 荒牌、九种九牌、四风、四家立直及四杠结束；和牌番符、支付、责任役满、流满、罚符及供托结算。
- 展示5秒后自动下一局，保留点数并重置本局长考/私有标记；南4和止/听止、西入、西4及最终排名。
- 结算日志与公开手牌/里牌，重连恢复结果和原换局期限；虚拟时钟可推进整个半庄。
- 以作者认可的牌桌轮流代选四个真实客户端，切换视角、精确选择组合、暂停/推进时间、换局与重开。TablePreview可直接按F1/点击“四座调试”进入，TableDebug则直接开局。

## 5. 验证入口

```powershell
./tests/run-core-checks.ps1
./tests/run-scoring-checks.ps1
./tests/run-network-checks.ps1
./tests/build-unity.ps1
```

最近记录：核心21/21、计分42/42、网络/同步/整场78/78检查通过，Unity与Editor程序集编译0错误0警告。作者已反馈UI修复后初步测试没有问题，未提供逐项人工覆盖清单。检查启动器需要.NET 10 SDK，网络运行源码同时以netstandard2.1编译，无第三方NuGet依赖。网络测试只使用127.0.0.1临时端口。

Unity生成的csproj不手工编辑。构建脚本通过额外targets适配尚未刷新生成工程时的新/删文件；Unity重新生成工程后也会更新源列表。

当前牌桌入口见 [单人四座调试](SOLO_DEBUG.md)。旧网络入口的Inspector、按键和跨机器验收见 [局域网测试步骤](LAN_TEST_PLAN.md)，常规局域网房间UI尚未接通。

## 6. 仍需完成

- P3a可见日志/检查点已完成，见 [同步设计](MATCH_SYNCHRONIZATION.md)。
- P3b操作状态机已完成本阶段实现与自动化验证，见 [状态机](STATE_MACHINE.md)；少见场景仍需专项回归和人工验收。
- P4首版预设计分、结算及整场推进已完成自动化验证，见 [计分与整场](SCORING_AND_MATCH.md)。
- P5后端旧入口清理、恢复门槛及六项TCP恢复边界已完成；有场景引用的两个空壳待Editor整理。
- P6已有牌桌与四座调试并获初步人工测试反馈；常规房间/单玩家UI、展示细节、整桌Prefab封装及跨机器验收仍需后续完成。建议顺序见 [当前开发起点](CURRENT_BASELINE.md)。

旧动作推演、候选字典、旧听牌DFS和512字节公共编码已删除，测试迁至现用链路；MainMatchControl只保留状态与发牌/取牌原语，GlobalFunction仅保留随机与基础字节工具。固定牌例不替代Unity运行验证。

## 7. 恢复与协作

修改前基线位于本地忽略目录`.utmp/baselines/`，保存P1、P2、P3a、P3b及P4开始前的文件、未提交修改及逐文件SHA-256。不得用Git当前提交覆盖作者原有未提交工作。

P5清理前另有247文件恢复基线，含原有未提交修改。前端阶段另保存了UI创建、四座调试及输入修复前的局部基线。后续保留当前牌桌布局与接口；场景空壳仍保留至作者在Editor处理引用。开发起点文档不等于Git提交或全量备份。
