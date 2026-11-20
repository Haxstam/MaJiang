# 独立麻将逻辑模块

纯 C#、同步、由外部操作驱动的四人立直麻将引擎。这个文件夹可以整体复制到其他位置，独立构建和运行，不需要 Unity、前端、房间或网络服务。

源码从 2026-10-25 的现有项目复制后抽离。原 Unity 项目、文档和测试文件保持原样；本项目不通过链接或编译路径引用原项目源码。

已增加可选的 Mortal-S42 转换层，支持本地场况/事件→模型建议→本地合法操作→新场况的闭环，见 [MORTAL_INTEGRATION.md](MORTAL_INTEGRATION.md)。核心仍不依赖模型；模型适配库使用 .NET 10，真实推理使用用户已有的 Python 和模型部署。

四种诅咒的初始配牌、动态摸牌与指示牌调整也已实现，按需启用，见 [CURSES.md](CURSES.md)。模型不接收诅咒信息；未启用时仍为普通模式。

诅咒规则保留两个版本：默认V1为原始实现；选择V2时，诅咒3的起手13张也不允许重复牌种。可用 `-CurseRuleVersion v2` 显式选择，旧版实现、种子结果与已有记录保留，统计按版本区分。详见 [V1与V2说明](CURSES.md#v1与v2并存)。

已接入用户现有venv的CUDA PyTorch，GPU运行入口为 `./run-mortal-cuda.ps1`；环境、Python 3.10兼容处理和实测结果见 [MORTAL_CUDA.md](MORTAL_CUDA.md)。

现已支持每个半庄独立保存成功动作、逐局结算、最终结果和调牌审计，并导出CSV。相同两个种子的实测为串行65.95秒、两场并发40.59秒；记录结构、统计资格与测速细节见 [EXPERIMENT_RECORDING.md](EXPERIMENT_RECORDING.md)。

通用玩家统计入口为 `python/player_statistics.py`，覆盖和牌/放铳、自摸、立直、默胡、流局、副露、巡目、打点、名次及效率/净收支等22项指标，可按名字、座位、诅咒或模型筛选。定义、命令和口径参数见 [PLAYER_STATISTICS.zh-CN.md](PLAYER_STATISTICS.zh-CN.md)。

长时间批次可用 `./manage-background.ps1 -Action Start`：默认1000场、两场并发，由Windows独立后台任务执行，冻结运行副本并提供查看、平稳停止及续跑入口；无需保持Codex对话打开。说明见 [BACKGROUND_RUNS.zh-CN.md](BACKGROUND_RUNS.zh-CN.md)。

## 已确认的边界

1. **不计时。** 没有时钟、期限、长考、Tick、计时器或后台线程。时间经过和读取场况都不会推动对局。
2. **只记录已执行事实。** 未裁定的吃碰杠、被更高优先级覆盖的选择、提交请求和回执不进入对局日志。
3. **不自动换局。** 本局结束时作为当前操作的后果完成结算，然后保持状态，直到宿主明确调用 `StartNextRound()`。

这里的“静态”指被动推进，并不是 C# 的 static 类。每个 `MahjongEngine` 实例持有一场独立的状态，调用者负责串行访问。

## 构建和验证

类库目标为 `netstandard2.1`；随附检查与控制台示例使用本机已配置的 .NET 10 SDK。不依赖第三方 NuGet 包。

```powershell
# 在本文件夹执行
dotnet build MaJiang.CoreStandalone.sln --configuration Release
./run-checks.ps1
dotnet run --project examples/MaJiang.Console --configuration Release
```

也可以用 Visual Studio 打开 `MaJiang.CoreStandalone.sln`。示例里的循环代表外部宿主逐次提交操作；引擎内部没有驱动循环。

## 最小调用示例

```csharp
using System.Linq;
using MaJiangLib;

var engine = new MahjongEngine(
    new MatchSettingData(MatchType.FourMahjongSouth),
    new[] { new UserProfile("A"), new UserProfile("B"),
            new UserProfile("C"), new UserProfile("D") });

// 构造表示显式开局：发牌、公开首张指示牌、完成庄家首摸。
var view = engine.GetPlayerView(0);
var commands = engine.GetAvailableCommands(0);
var discard = commands.First(c => c.Action.PlayerAction == PlayerAction.TakeCut);
var feedback = engine.Submit(0, discard);

// 当前场况、本局已执行记录、这次调用新产生的记录。
var current = engine.GetState();
var records = engine.GetRecords();
var newRecords = feedback.Records;

// 本局结束且整场未结束时，由宿主明确调用。
if (current.OperationStage == MatchOperationStage.RoundEnded && !current.MatchEnded)
{
    engine.StartNextRound();
}
```

`GetAvailableCommands` 返回绑定当前整场、本局和操作窗口的完整命令。宿主也可以按同一数据结构自行构造命令。重试同一命令时保留 `CommandId`；改变内容后必须使用新 ID。

## 接口

| 方法/属性 | 用途 |
| --- | --- |
| `GetState()` | 可信宿主的完整场况副本，包含四家暗手、牌山、待操作座位、裁定状态、结算 |
| `GetPlayerView(seat)` | 指定玩家的场况副本：公共信息、本人暗手及本人振听/已答复状态 |
| `GetAvailableCommands(seat)` | 当前座位可以提交的具体操作；没有操作机会时为空 |
| `Submit(seat, command)` | 处理一次外部输入，并返回反馈及新执行事实 |
| `GetRecords(afterSequence)` | 本局完整已执行记录，支持按序号增量读取 |
| `GetPlayerRecords(seat, afterSequence)` | 遮蔽其他玩家摸牌牌面的本局记录 |
| `StartNextRound()` | 明确开始下一局；保留点数和供托，重建牌山、清理本局标记 |
| `RoundIds` | 当前实例已经开始的全部本局 ID |
| `GetRoundRecords(roundId, seat, afterSequence)` | 查询已结束本局的记录；换局不会丢弃历史 |

所有返回的可变状态都与内部状态隔离。记录载荷的每次读取也返回副本。修改返回对象不能改变对局。

完整场况、完整记录和 `feedback.Records` 面向可信宿主，包含私有摸牌信息。给某位玩家显示或发送数据时，使用玩家视图和该玩家的记录。身份认证、座位授权与传输由宿主负责。

## 一次输入如何推进

一次输入会完成它已经确定的规则后果。例如，舍牌后无人有可选响应时，直接进入下家的摸牌回合；存在响应者时，停在响应窗口，等待外部输入。无需选择的玩家在这次调用内完成规则上的略过，不等待虚构的期限。

多人响应需要私下暂存选择，才能正确裁定优先级。先收到碰、后收到荣和时，日志最终只记成立的荣和；收到碰之后始终没有其余响应时，模块会无限期等待。宿主如需超时策略，应自行选择并传入对应玩家菜单中的 `Skip` 或合法舍牌。

`feedback.Accepted` 表示选择被接收处理，不承诺竞争中的鸣牌已经成立。`AwaitingResponses` 表示当前仍在收集响应；只有 `Records` 中出现的动作才是已执行结果。多个选择收集期间，反馈可以没有新记录。

## 记录的含义

每局序号从 0 开始，按实际发生顺序记录：

- `RoundStarted`：本局开始。
- `DoraRevealed`：某张宝牌指示牌已经公开。
- `ActionApplied`：实际摸牌、舍牌、吃、碰、成立的杠、成立的立直、和牌或流局。
- `RoundSettled`：支付、公开手牌、里牌和下一局参数已确定。

立直的舍牌先记录为手切/摸切，确认成立后另记立直；宣言牌被荣和时保留真实舍牌，不记立直成功。被抢的杠不记为成立。单纯提交响应、失去优先级的选择、跳过响应以及阶段等待不生成动作日志；振听等当前裁定状态通过场况查询取得。

核心日志保存在当前实例的内存中，换局后仍可查询。Mortal宿主通过独立的 `ExperimentMatchRecorder` 将成功事实逐步保存到文件，详情见 [EXPERIMENT_RECORDING.md](EXPERIMENT_RECORDING.md)。核心仍不依赖文件系统。确定动作日志不等同于全部输入历史，不能将其直接当作 `Submit` 指令逐条重放。

## 规则与兼容性

沿用原项目已确认的四人日麻东南战规则、东风配置、役符计算、多人荣和、振听、抢杠、杠宝牌时点、支付、包牌及整场终局判断。本次只改变驱动和模块边界，没有重新实现计分规则。

已确认的新边界覆盖原项目中的操作期限、超时自动动作和 5 秒自动换局。原有非法选择策略继续保留：当前有效窗口中的规则非法响应按跳过处理，规则非法自身操作按合法默认舍牌处理，反馈为 `Accepted=false, UsedDefault=true`。过期窗口、错误座位、重复变更 ID 和结构损坏的请求只拒绝，不消费机会。日志只记真正执行的默认动作。

当前公开入口支持四人东风/东南、一番缚、无古役；三麻、古役和更高番缚不在本轮实现范围。`Tile`、`MainMatchControl` 等部分模型名称沿用原项目以便核对迁移；领域模型中的固定字节编码和 Unity 特性已移除。

## 目录与维护

```text
src/MaJiang.Core/Domain/     牌、手牌、玩家、规则配置、牌山与状态原语
src/MaJiang.Core/Rules/      原共享规则、牌型分析、计分和结算
src/MaJiang.Core/Engine/     被动操作机、最终事实日志、手动换局、公开门面
src/MaJiang.Core/Dealing/    四种诅咒、条件配牌、隐藏牌置换与宿主审计
tests/MaJiang.CoreChecks/   领域、动作与独立模块契约检查
tests/MaJiang.ScoringChecks/ 46 项计分固定牌例（原 42 项及新增拆分牌例）
examples/MaJiang.Console/   无 Unity、无 Socket 的调用示例
src/MaJiang.Mortal/         mjai 事件转换、模型建议匹配与子进程通信
tests/MaJiang.MortalChecks/ 不加载权重的适配契约检查
tests/MaJiang.CurseChecks/  配牌、王牌优先级、失效、里牌与整场库存检查
examples/MaJiang.MortalConsole/ 单座/四座真实模型对局示例
```

迁移说明、校验范围和后续接入约束见 [EXTRACTION.md](EXTRACTION.md)。

和牌拆分、实际得点选优及抢杠验证见 [WIN_SCORING.md](WIN_SCORING.md)。当前检查为18项领域、41项动作/模块、46项计分、24项模型适配/记录、30项配牌，总计159项通过；Python统计和后台脚本另有19项检查。

此前普通模式的真实四座模型已完成13局、747次输入的一整场；CUDA诅咒模式已完成4局、201次接入验证，并完成两个种子各串行/并发一次的4个完整测速半庄。大规模夺一率实验及CPU/GPU吞吐对比尚未开展。运行前阅读接入说明和诅咒说明中的兼容性边界。

后续100场小批量试运行已全部完成：两场并发共1618局、71058次决策，约33分21秒，无失败或截断，记录位于用户指定的 `G:/Documents/Test/batch-100-20261025-175727/`。每种诅咒在每个初始座位各25次，国士抢暗杠按用户策略默认跳过。全量事实序号、结算、点数与CUDA配置校验通过。可追溯摘要见 [validation/Mortal-100Matches.json](validation/Mortal-100Matches.json)，小样本结果不作为稳定强弱结论。
