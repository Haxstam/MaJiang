# 半庄记录与测速

## 当前实测速度

2026-10-25，RTX 4050 Laptop 6GB、用户现有 Python 3.10/PyTorch 2.12.0+cu126、四家 Mortal-S42、诅咒按座位 `1,2,3,4`。开启成功动作逐步落盘、详细调牌审计及协议导出。

使用相同的种子2、3，分别串行和并发各运行一次。每组共2个完整半庄、29局、1325次模型决策；各次都正常完成。两种运行方式的成功动作、结算与诅咒审计在忽略场/局随机ID后完全一致。

| 指标 | 串行（同时1场） | 同时2场 |
| --- | --- | --- |
| 完成相同2个半庄的总耗时，含模型加载及退出 | 65.95秒 | 40.59秒 |
| 总决策/秒，含加载和退出 | 20.09 | 32.64 |
| 对局阶段合计决策/秒 | 31.26 | 48.05 |
| 全机显存占用峰值 | 3200MiB | 3859MiB |
| 全机可用内存最低值 | 9383MiB | 5278MiB |

串行两场分别为29.41秒、36.54秒；模型加载约11秒/场，对局本身17.48秒、24.91秒。两场并发时加载约12.7秒/场，对局分别22.60秒、27.57秒。当前每个半庄重新启动4个模型进程，两场并发即8个。总吞吐提升约1.62倍，单场延迟有所增加。

显存数字包含其他应用，运行前基线约2500MiB。内存余量比显存更值得注意，目前建议先使用2场并发。没有测试3场并发，不能据显存余量直接认为可线性扩展。后续若需要更高吞吐，可以让跨半庄的模型进程常驻，或共享模型并合并推理批次；这些优化尚未实现。

这只是两个种子的短测，温度、其他应用及每个半庄长度都会影响长期速度。按本次总耗时粗估，两场并发约177半庄/小时，并非长期运行保证；没有测量CPU/GPU加速比。结构化数据见 [validation/Mortal-Performance.json](validation/Mortal-Performance.json)。四个真实模型运行样本均标为 `benchmark`，正式实验样本数为0。

## 默认记录方式

在本目录执行：

```powershell
# 四家CUDA、四种诅咒，保存完整成功动作和详细审计
./run-mortal-cuda.ps1 -Seed 10

# 指定输出根目录；每场仍建立独立目录，不覆盖前一场
./run-mortal-cuda.ps1 -Seed 11 -Output experiments/run-01

# 少量验证或测速必须明确标记，避免混入正式样本
./run-mortal-cuda.ps1 -Seed 12 -Rounds 2 -Purpose validation
```

GPU脚本默认输出到本项目 `experiments/`，默认开启详细审计。`-NoAudit` 可关闭逐条调牌审计，累计计数仍保存。直接运行控制台也默认输出到当前工作目录的 `experiments/`；其详细审计需指定 `--audit`。输出文件夹已加入Git忽略规则。

每场目录名为 `match-<唯一ID>`，内部文件为：

| 文件 | 用途 |
| --- | --- |
| `match.json` | 状态、用途、种子编号和编码、座位/诅咒分配、规则、模型身份、程序/桥接器/原生扩展散列、实际CUDA运行环境、耗时、最终点数/名次、累计调牌统计和异常 |
| `events.jsonl` | 每局初始完整场况检查点，以及有连续局内序号的成功动作、宝牌揭示、局末结算 |
| `rounds.jsonl` | 每局一次的结算，包括和牌人、放铳人、役/番/符、支付、点数变化、连庄及流局原因等 |
| `curse-audit.jsonl` | 隐藏调牌审计，记录原牌、结果、交换来源、无法避免的失效；关闭详细审计时为空 |
| `final-state.json` | 正常结束、截断或已捕获异常时的完整最终场况 |
| `seat-0.mjai.jsonl` 等 | 各已接入模型自己的可见事件流，结束或捕获异常时导出，用于排查协议问题 |

主动作文件只含最终事实，不记录“想碰”、未裁定的响应、落败的吃碰或模型建议。协议流因模型同步需要可能包含杠宣言，统计成立的杠应以主动作文件为准。

每局 `sequence` 从0开始，用 `(matchId, roundId, sequence)` 标识成功记录。`round_start` 是含庄家首摸的检查点，`checkpointSequence` 指明已包含的事实序号；它本身不代表又摸了一张牌，做动作计数只取 `type=applied` 的行。

完整场况及审计供可信宿主统计使用，含其他玩家暗手、隐藏牌山等，不得发送给模型。成功事实不是完整的输入请求历史，不能直接当作 `Submit` 指令重放。

## 完成状态和异常保留

- `running`：尚未写入结束标记；进程被直接关闭时也会保留这个状态。
- `completed`：核心明确判定整个半庄结束。
- `truncated`：已运行到局数上限，但整个半庄尚未结束。
- `failed`：模型无建议、建议非法、同步失败等异常导致停止，保留错误详情和已执行事实。

只有 `purpose=experiment`、`status=completed` 且 `matchEnded=true` 才会标记 `eligibleForStatistics=true`。测速和验证、截断及失败均不计入正式完成样本，统计报告仍须披露失败/未完成数量及原因。

主动作、结算和审计在每次外部操作之后刷新到文件，启动时先写入 `running` 元数据；模型同步失败后也先保存已执行的核心事实。正常终结时原子替换元数据文件。突然终止可能留下最后一行不完整，但此前刷新的行仍可排查；这里不保证断电后的磁盘持久性。mjai和最终场况是结束时导出，不承诺强制关闭时存在。.NET实时读取仍在写入的文件时需用 `FileShare.ReadWrite`。

本次两个半庄每场记录分别约0.97MB和1.22MB，包含四家协议和详细审计，平均约1.1MB。按此样本粗估1万场约11GB，实际取决于局数和行为；暂未启用压缩。

## 有界批次运行

构建当前Release版本后，可以启动固定数量、两场并发的完整半庄：

```powershell
& 'D:\PycharmProjects\Envirment1\venv\Scripts\python.exe' -B python/run_matches.py --count 100 --concurrency 2 --seed 1 --purpose experiment --output 'G:\Documents\Test\新批次目录'
```

输出目录必须不存在。每24场覆盖全部诅咒排列，余下优先使用循环排列；100场恰好让每个座位分别承担每种诅咒25次。每场使用不同种子。每场仍开启完整成功事实和详细审计。

根目录 `batch.json` 保存不可变配置、代码散列、计划和逐场运行状态；`runs/` 保存每次运行的stdout/stderr，`matches/` 保存完整半庄文件，`resources.jsonl` 保存资源采样，批次停止后 `summary/` 生成CSV表和状态计数。发生第一个失败或截断时停止启动新场次，已启动的场次正常收尾，不悄悄换种子补齐。

`--resume` 仅允许配置和程序散列一致、没有待检查的失败或中断进程时继续未启动的场次；不会覆盖已有记录。宿主对每场有独立的进程等待上限，达到上限时保存失败状态，不向核心伪造任何游戏操作。该上限不属于核心计时规则。

## 导出统计表

```powershell
& 'D:\PycharmProjects\Envirment1\venv\Scripts\python.exe' -B python/summarize_matches.py --input experiments/run-01 --output experiments/run-01-summary
```

输出 `matches.csv`、`seat-results.csv` 和 `summary.json`。前者每半庄一行，后者每座位一行，带诅咒、最终名次/点数和调牌计数；两表均保留用途、状态及统计资格列。运行中、失败和截断记录不会被静默丢弃，重复的场ID会报错。CSV为UTF-8 BOM，便于Excel读取。

完整22项玩家指标（和牌/放铳/自摸、立直/默胡、副露、流听、巡目、打点、收支等）由 `python/player_statistics.py` 提供，支持选择任一玩家及切换口径，见 [PLAYER_STATISTICS.zh-CN.md](PLAYER_STATISTICS.zh-CN.md)。

逐役种次数和比例可用 `python/yaku_statistics.py --input <记录根目录> --output <输出目录>`。同一份输出同时提供按诅咒角色及固定玩家名字分组的CSV/JSON，普通役与宝牌类分开，记录出现手数、累计番数及役满单位；不混入未完成场次。同手多役各自计数，普通立直和两立直、普通国士和十三面分别列出。

正式研究仍需平衡24种诅咒/初始座位分配，并按相同规则、模型身份和程序版本分组，不能直接混合不同配置。按用户已确认策略，模型遇到国士抢暗杠选项时默认合法跳过，元数据记录 `modelPolicies.kokushiRobClosedKan=skip`，此情形不再使样本失败；具体见 [CURSES.md](CURSES.md)。其他错误仍按失败保留。测试夹具与测速记录位于临时目录，不能混入正式实验输入目录。

如需复测并发吞吐，先构建Release，再运行标准库脚本（输出目录必须不存在）：

```powershell
& 'D:\PycharmProjects\Envirment1\venv\Scripts\python.exe' -B python/benchmark_mortal.py --concurrency 2 --seed 2 --output .utmp/benchmark-new
```

它显式使用 `purpose=benchmark`，记录宿主完整生命周期及每0.5秒加上采样开销的GPU/可用内存数据，不会生成正式统计样本。
