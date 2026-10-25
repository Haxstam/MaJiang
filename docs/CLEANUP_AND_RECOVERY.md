# 后端清理与恢复边界

> 2026-10-24：已完成本轮旧入口清理和恢复检查。核心21项、计分42项、网络75项及Unity CLI编译通过。作者将提供展示素材，前端设计明确推迟；现有调试入口保留。

## 1. 清理范围

开始前保存并逐文件验证247文件基线：`.utmp/baselines/2026-10-24-before-cleanup/`。包含原有未提交修改，不能用Git当前提交替代该工作区基线。

引用检查确认旧规则/推进代码只剩历史测试使用，已将测试迁至现用入口并删除旧实现：

| 已删除入口/状态 | 当前入口 |
| --- | --- |
| MainMatchControl.ActionDeduce、PlayerActionJudge、动作候选字典/ActionMark | AuthoritativeMatchMachine.Submit/Tick，MatchActionRules纯查询 |
| FourNewHonba、HonbaEndMark | RoundSettlement纯结算与MatchSession换局 |
| GlobalFunction旧鸣牌/立直/流局/振听判定 | MatchActionRules、HandAnalysis、RoundSettlement |
| TenpaiJudge、CutTenpaiJudge、旧顺刻优先DFS及全局缓存 | HandAnalysis真实34牌种与全部分解 |
| GetPublicBytes/PublicBytesTo固定512字节编码 | MatchSyncCodec可见检查点及有序增量 |
| MainMatchControl.IsPlayerFuriten旧布尔副本 | MatchJournal.FuritenFlags及本人VisibleMatchState.FuritenFlags |
| 公共状态UraDoraPointerList空槽位 | 只在Settlement.UraIndicators公开已允许查看的里牌 |
| 四人状态的KitaCount及旧DrawType | 四人预设不使用拔北，结束原因使用RoundOutcomeKind |

GlobalFunction现在只保留洗牌/牌山生成与基础二进制数据工具，共享枚举移至MatchEnums.cs，命名空间与协议枚举数值不变。MainMatchControl保留完整状态、发牌重建、取牌和公共标记原语；没有平行的旧推进入口。

GetNextTile现负责唯一扣减余牌数，正常摸牌移动牌山指针，岭上摸牌取得固定岭上位置并保留海底。调用者只把返回牌放入玩家手牌，不再自行减余牌。非法取牌不会先扣数，逐步136牌守恒与四次岭上检查通过。

显式HandCut即使与摸牌牌值相同也消耗暗手中的所选牌，把原摸牌并入暗手；不再只用牌值相等推断摸切。已在现用操作机回归中覆盖。

核心检查不再为了保留旧实现而调用旧入口。它通过实际netstandard2.1运行程序集运行操作机/可见编码检查，不打开Socket；计分检查仍直接编译共享核心源。日志层单元检查允许在测试夹具中布置状态，真实操作/恢复检查始终通过当前命令路径。

## 2. 客户端恢复门槛

新增`LanRoomClient.IsMatchSynchronized`：已入房、已收到当前检查点/日志且没有待补齐时才为true。`AvailableCommands`和`SendCommand`以此为门槛。

重新建立TCP并收到Joined不等于牌局已恢复。旧可见状态可以暂时保留供查看，但在检查点安装前不再提供旧菜单或发送选择。主动恢复/缺号补齐期间同样暂停菜单；完成后按最新视图重新计算。实际身份、阶段、去重及合法性仍由服务端最终校验。

客户端默认30秒收不到服务端任何完整消息则关闭连接，报告heartbeat timeout并保留玩家ID/恢复令牌。正常每秒心跳不会误触发。`LanRoomClient(TimeSpan?)`允许在检查中缩短静默期限，不改变默认设置；这与底层半帧/半消息30秒期限分别负责空闲失联和消息截断。

`LanRoomServer(Func<long>)`默认使用Stopwatch单调毫秒时钟，也允许注入同一非负单调时钟确定性验证期限/换局。该时钟同时用于房间保留期、对局、日志及心跳采样，Socket读写期限仍由传输层实际经过时间决定。

## 3. 本轮新增验证

六项真实TCP恢复检查：

1. 申请恢复后菜单立即关闭；重新Joined的回调中仍无旧菜单，直到检查点安装才恢复。
2. 自己断线达到期限后仅默认舍牌一次，重连不退还长考；旧选择不消费新窗口。
3. 已提交的吃牌在等待碰牌者期间断线，恢复后仍已答复、长考冻结；重发原命令不重复执行，碰牌者跳过后保留的吃正确成立。
4. 加杠抢杠窗口内重连，保持原阶段、目标牌、期限和余量；随后荣和不提前完成杠或摸岭上。
5. 断线期间服务端已换局，恢复直接安装新RoundId/手牌、保留分数和上局公开结果，长考按新局重置。
6. 正常心跳保持活性；停止服务端业务消息后客户端主动识别静默，关闭连接但保留恢复身份。

原69项网络检查、42项计分及21项核心检查继续通过。客户端门槛、内部状态清理和测试时钟不改变消息布局，协议仍为v5。

## 4. 前端推迟与保留项

没有修改场景、预制体或美术资源，也没有制作新的牌桌或正式结算界面。保留NetworkControl/MatchServer原GUID与调试输入。作者提供素材后再接表现层。

旧`Network Scripts/Client`与`Server`是空MonoBehaviour，仍被Majhong.unity引用，因此保留兼容壳，仅清除无用引用并注明替代入口。素材和场景整理时在Editor移除这些组件，再删除源/meta；不在未处理场景引用时直接制造Missing Script。

未来表现层可读取`Client.Match.Current`、`Settlement`及`IsMatchSynchronized`，从`AvailableCommands`选取原命令并调用`SendCommand`；不直接修改服务端核心或自行推演网络操作。Inspector、实机与跨机器验收见`LAN_TEST_PLAN.md`，CLI编译不等于Editor运行验证。
