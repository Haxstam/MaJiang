# 独立后台批次

`manage-background.ps1` 由Windows任务计划程序启动一个独立后台进程，不依赖Codex对话、当前终端或Codex应用保持打开。它不是新的Codex对话，也没有设置周期触发器。

## 启动1000场

在独立项目目录运行：

```powershell
./manage-background.ps1 -Action Start
```

默认仍使用原始V1。新版诅咒3起手无重复用 `./manage-background.ps1 -Action Start -CurseRuleVersion v2`，其余条件相同。版本写入批次和每场记录，不能在续跑时切换；必须创建新批次。已有后台冻结副本不会因本次源代码更新而改变。

默认1000个半庄、两场并发、种子101—1100，输出到 `G:\Documents\Test\batch-1000-background-<北京时间>/`。每24场覆盖全部24种诅咒排列，每四场组成一组座位均衡的循环排列。因此1000场中，每种诅咒在每个初始座位恰好250次；种子不与之前100场重复。

每场使用四家当前CUDA模型，开启完整成功事实和详细调牌审计。国士抢暗杠仍采用用户指定的合法跳过策略。按此前约180半庄/小时估算，1000场约需5—6小时，实际速度会受电脑负载影响。

## 冻结副本

每个新批次会复制本地运行脚本、.NET产物及Python原生扩展到该批次的 `runtime/`，并记录SHA256。后续在原项目中改代码、构建或继续聊天，不会改变这批正在使用的副本。Python环境和原模型部署、权重仍使用现有路径，不会重装或复制整个venv；运行期间请保持这些环境及模型文件可用。

任务仍使用本机GPU和内存。电脑需要保持开机，不能注销、重启或手动睡眠；进程运行期间会申请阻止系统自动睡眠，允许屏幕关闭，不修改永久电源计划。此任务以当前用户的有限权限运行，不保存账户密码。关闭Codex不会结束它。

## 查看、停止和继续

每个批次目录自带三个脚本，普通PowerShell中执行即可：

```powershell
# 在该批次目录运行
./status.ps1
./stop.ps1
./resume.ps1
```

`stop.ps1` 请求平稳停止：当前正在运行的半庄会完成，不再启动新场次。状态变为 `stopped` 后可以 `resume.ps1` 继续剩余种子，已完成记录不会覆盖。请使用这个停止入口，而不是直接结束Python进程或在任务计划程序中强制终止。

也可以从项目目录指定批次路径：

```powershell
./manage-background.ps1 -Action Status -BatchDirectory 'G:\Documents\Test\批次目录'
./manage-background.ps1 -Action Stop -BatchDirectory 'G:\Documents\Test\批次目录'
./manage-background.ps1 -Action Resume -BatchDirectory 'G:\Documents\Test\批次目录'
```

同一任务不允许同时启动两个实例。遇到失败、截断或异常进程中断时会保留现场并停止继续调度，状态为 `needs_attention` 等；需要检查原因后处理，不能静默丢弃或换种子补齐。正常停止的续跑会检查配置和运行文件散列一致。

## 文件

- `background.json`：系统任务名、冻结文件散列、总状态、启动/结束时间。
- `worker.stdout.log`、`worker.stderr.log`：后台监督进程及调度器日志。
- `runtime/`：本批运行副本，运行时不要修改。
- `records/batch.json`：逐场计划、完成/运行/失败数量、最新进度和资源余量，约每10秒或场次变化时更新。
- `records/matches/`：每半庄独立目录，完整成功动作、结算、场况、隐藏审计和可见协议。
- `records/runs/`：每场宿主的stdout/stderr。
- `records/summary/`：批次停止或完成后生成CSV、完整22项玩家统计及全量校验；完整实验批次完成时还生成国士/染手/打点分析。

主记录每步刷新，异常时已有数据仍在磁盘；结束元数据及完整派生分析需要进程正常收尾。所有终局统计均保留来源、用途与完成状态。1000场按之前样本约需1.2GB逐场记录，加上派生表及冻结副本。

无需在对话里等待五小时。以后可直接运行 `status.ps1` 查看，或让助手读取该目录的进度。这里没有另行设置通知或自动重启任务。
