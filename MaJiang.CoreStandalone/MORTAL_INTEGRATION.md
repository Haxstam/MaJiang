# Mortal-S42 接入

更新：2026-10-25。已接通本机 Mortal-S42 的事件输入、动作输出和真实对局闭环，没有修改外部模型部署目录或原 Unity 项目。

## 模块边界

```text
外部宿主明确请求一步
    → MaJiang.Mortal 向模型传入玩家可见的 mjai 事件
    → Mortal-S42 返回建议
    → 适配器将建议匹配为本地当前合法 MatchCommand
    → 宿主明确提交该建议
    → MaJiang.Core 执行并裁定
    → 适配器向所有已连接模型回灌实际结果
    → 宿主读取新的场况、核心日志和操作反馈
```

`MaJiang.Core` 仍为 `netstandard2.1` 纯同步逻辑，不依赖 Python、JSON、进程或网络，不增加计时、自动打牌和自动换局。

新增的 `MaJiang.Mortal` 是独立的 .NET 10 适配库，使用 .NET 自带的 JSON 和子进程 API，无第三方 NuGet 包。Python 与模型只在显式连接真实模型时才需要；自动化适配检查使用模拟模型会话，不加载权重。

## 本机模型配置

默认读取用户指定的现有部署：

- Python：`D:\miniconda3\envs\mortal\python.exe`
- 模型部署目录：`G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42`
- 调用入口：该目录的 `mjai_bridge.py`
- 设备：CPU，默认每个桥接进程 2 个推理线程。

每个控制座位使用一个长期存活的桥接进程，模型只在该进程启动时加载一次。进程通过参数列表启动，路径不经过 shell 拼接；没有可见终端窗口。stdin/stdout 使用 UTF-8 JSONL，一行事件对应一行 `ok/reaction` 封装，stderr 单独排空并保留有界错误信息。

`MortalBridgeOptions` 可以修改 Python、部署路径、设备、线程数和通信等待上限。通信超时只会使模型会话失效，不会让麻将核心自动舍牌、跳过或换局。启动时使用 `-B` 和 `PYTHONDONTWRITEBYTECODE=1`，不在模型代码目录写入 Python 字节码。

协议依据用户提供的 [INPUT_PROTOCOL.zh-CN.md](G:/Documents/Codex/2026-10-24/qing-n/outputs/Mortal-S42/INPUT_PROTOCOL.zh-CN.md)，并核对了本机 `mjai_bridge.py`、`inference.py` 和 libriichi 的事件/状态更新代码。

## 运行真实对局

在本文件夹执行：

```powershell
# 构建全部项目并运行不需要真实模型的检查
./run-checks.ps1

# 仅座位0使用模型，其他座位由示例宿主明确选择合法摸切/跳过
dotnet run --no-build --configuration Release --project examples/MaJiang.MortalConsole -- --seats 0 --rounds 1

# 四家都是 Mortal，运行至整场结束，最多允许100个本局
dotnet run --no-build --configuration Release --project examples/MaJiang.MortalConsole -- --seats 0,1,2,3 --rounds 100

# 可选：指定路径并导出各自可见的协议事件
dotnet run --no-build --configuration Release --project examples/MaJiang.MortalConsole -- --seats 0 --rounds 1 --python 'D:\miniconda3\envs\mortal\python.exe' --model-dir 'G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42' --output .utmp/mortal-demo
```

示例中的循环属于宿主；每次循环明确请求、检查并提交一次操作，局末明确调用换局。模型无建议、建议不合法或同步失败时，示例报告原因并停止，不使用隐含的默认策略替代模型。

控制台默认记录到当前工作目录的 `experiments/`，`--output` 改变输出根目录；每个半庄创建唯一子目录。主文件逐步保存成功动作，结束时保存最终结果及各座位可见协议。`--purpose benchmark/validation` 可标记测速或验证，避免混入正式完成样本。格式、异常保留与CSV汇总见 [EXPERIMENT_RECORDING.md](EXPERIMENT_RECORDING.md)。

`--seed` 选择示例的固定牌山种子，默认1；现使用 `sha256-v1` 将64位编号映射为32字节，避免旧版每256个编号重复。`--rounds` 是本局数量上限，不是自动结束整场的规则；已触发终局时立即停止。`--threads` 控制每个模型进程的推理线程数，`--device cpu/cuda` 选择现有环境支持的设备。导出的各座位 JSONL 均遮蔽他家暗手和摸牌，并附 `can_act:false`，用于更新状态与排查，不会在每个历史节点重新请求建议。

可通过 `--curses 1,2,3,4` 启用四种诅咒；配置、王牌顺序、审计和实验样本解释见 [CURSES.md](CURSES.md)。下面的13局验证是初次接入时普通模式和旧示例种子编码的历史记录，不是诅咒模式统计。

## C# 调用

```csharp
using MaJiangLib;
using MaJiang.Mortal;

var game = new MahjongEngine(
    new MatchSettingData(MatchType.FourMahjongSouth),
    new[] { new UserProfile("A"), new UserProfile("B"),
            new UserProfile("C"), new UserProfile("D") });

// 必须在当前本局的任何玩家操作之前建立适配器。
await using var adapter = new MortalMatchAdapter(game);
await adapter.AttachModelAsync(0, new MortalBridgeClient(0));

// 请求建议不改变麻将核心。立直时可能需要与模型完成两次协议交互。
MortalDecision decision = await adapter.RecommendAsync(0);
if (decision.Available)
{
    // 这是明确推进麻将核心的一次输入。
    MortalStepResult step = await adapter.SubmitModelAsync(decision);
    var feedback = step.Feedback;
    var state = adapter.GetPlayerView(0);
    // 检查 step.ModelErrors；同步失败时 feedback 仍可能表示动作已执行。
}
```

人工或其他算法的操作通过 `adapter.SubmitAsync(seat, command)` 传入，对应菜单通过 `adapter.GetAvailableCommands(seat)` 取得。本局结算后的下一局使用 `adapter.StartNextRoundAsync()`。连接适配器后不要绕过它直接调用原 `game.Submit` 或 `game.StartNextRound`，否则公开事件历史会缺失；适配器会检测到窗口、日志位置或待操作座位不一致并拒绝继续。

## 转换约定

| 本地数据 | mjai 数据 |
| --- | --- |
| 固定绝对座位0–3 | 所有 actor、target、oya 与数组下标保持同一编号 |
| 实际点数、本场、供托根数 | scores、honba、kyotaku，不缩小点数、不把供托根数乘1000 |
| 万/筒/索、字牌、三赤 | `m/p/s`、`E/S/W/N/P/F/C`、`5mr/5pr/5sr` |
| 起手与庄家单独摸牌 | `start_kyoku` 每家13张，庄家第14张单独 `tsumo` |
| 手切/摸切 | `dahai`，保留精确牌面与 tsumogiri |
| 吃、碰、大明杠 | `chi/pon/daiminkan`，consumed 只包含本人消耗的2/2/3张 |
| 暗杠/加杠宣告 | `ankan` 的4张 consumed；`kakan` 的新增 pai 与原碰3张 consumed |
| 成立的立直 | `reach_accepted`，不重复扣1000点 |
| 和牌、流局及局末 | `hora/ryukyoku`，真实支付/里牌，随后 `end_kyoku` |
| 整场终局 | `end_game` |

模型输出严格匹配当前本地菜单，包含座位、来源、牌面、赤牌组合和摸切标记。不会因为模型返回了不合法建议就向核心提交“试试看”，因而不会意外触发核心的非法操作默认舍牌。原生 `ryukyoku` 和 `none` 可以没有 actor；`reaction:null` 是没有建议，`type:none` 才表示明确选择跳过。

## 立直、抢杠和多人响应

**立直两步合并。** 模型先建议 `reach`，适配器仅在该模型私有会话内预演宣言，取得后续 `dahai`，再匹配为本地一次“立直并舍指定牌”命令。这期间核心与公开历史不变。宿主明确提交后才广播真实 `reach` 和 `dahai`；已预演的模型不会再收到一次重复宣言。若宿主改选其他动作，则先用实际历史重置该模型会话。宣言牌被荣和时不发送 `reach_accepted`。

**杠宣告与成功杠分开。** 为让模型判断抢杠，协议在本地接受公开的暗杠/加杠宣告后发送 `ankan/kakan`。核心最终成功动作日志仍不记录尚未成立的杠；协议事件历史与核心日志是两个不同用途的序列。无人抢杠后核心记录成功杠，适配器不重复发送；被抢时只继续发送实际荣和与结算，不发送本次岭上摸牌或宝牌。

**吃碰竞争不提前回灌。** 模型建议的吃碰可以在核心中等待优先级裁定；没有真实执行结果时，不向任何模型广播该意向。最终碰胜过吃或荣和胜过碰时，只回灌实际成立的动作。

## 恢复和失败处理

`AttachModelAsync` 可以在对局中替换故障模型。适配器先发送 `start_game` 并重放自己保留的实际协议历史，过去节点使用 `can_act:false`，仅当前仍待操作的触发事件允许模型生成建议。替换不改麻将核心。初始化失败时会释放传入的客户端，避免残留等待 stdin 的进程。

模型进程在错误 JSON、错误封装、EOF、通信取消或超时时标记失效，不能在可能部分更新的状态上继续使用。用新的 `MortalBridgeClient` 替换并重放。`SubmitAsync` / `SubmitModelAsync` 的 `Feedback` 与 `ModelErrors` 分开：模型同步失败时，核心操作可能已经成功执行，不能把同步错误当作“核心尚未操作”。

适配器从当前局开头接管后，可以凭自身历史恢复模型；仅有一个中途快照而没有事件历史，不能凭空重建 Mortal 的巡目、见逃与立直时序，因此明确拒绝这种初始化方式。当前协议历史在内存中，示例可导出 JSONL，尚未提供整个宿主进程的存盘恢复入口。

## 已知兼容性边界

本机 libriichi 的 `state/update.rs::ankan` 对他家暗杠只登记公开牌，不开放国士抢暗杠反应；本地核心的已确认规则允许国士抢暗杠。按用户指定策略，模型座位遇到该合法选项时，`RecommendAsync` 返回可提交的合法 `Skip`，原因为 `KokushiRobClosedKanSkippedByPolicy`。这来自适配器策略，`ReactionJson` 为空，不冒充模型返回；宿主照常调用 `SubmitModelAsync` 后推进。跳过依核心规则产生见逃振听，暗杠无人抢时成立并继续岭上摸牌。

请求建议本身不改变核心，也不会把跳过写成成功动作。核心仍允许人工通过 `SubmitAsync` 选择合法的国士抢暗杠荣和；普通荣和、抢加杠及其他无建议/故障处理保持原策略。每场 `match.json` 的 `modelPolicies.kokushiRobClosedKan=skip` 记录此实验约定。

其他规则差异若使模型建议不在本地菜单中，返回 `ModelActionNotLegalLocally`；无建议时返回 `ModelProducedNoReaction`。这些都不会推进核心。当前接入验证针对现有四人东南预设，不能据此认定任意自定义规则都与 Mortal 相容。

## 本次实际验证

- 18项领域、41项动作/核心、46项计分、17项适配检查，共 **122项全部通过**；完整解决方案 **0错误、0警告**。
- 单个真实 Mortal 控制座位0，其他座位由宿主摸切/跳过：完成1局，25次模型输入、60次其他输入。
- 四个真实 Mortal 完成一整场：**13局、747次模型输入、9次立直选择、26次吃碰杠选择**，最终点数 `19300/18300/19300/43100`，正常发送整场结束。
- 整场没有调用非法输入的默认替代动作；包括荣和、自摸、荒牌、九种九牌、鸣牌、暗杠、杠宝牌、立直及手动换局。
- 四家各1231条协议事件，公共事件顺序一致；逐条验证暗手/摸牌遮蔽、13张起手、真实分数单位和 consumed 数量。

结构化验证结果见 [validation/Mortal-S42.json](validation/Mortal-S42.json)。具体接入代码位于 `src/MaJiang.Mortal/`，真实调用示例位于 `examples/MaJiang.MortalConsole/`。
