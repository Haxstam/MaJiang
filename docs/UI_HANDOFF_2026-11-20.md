# UI开发交接：2026-11-20

本文是下一对话继续完善UI的阅读起点，整理当前工作区的实际实现及作者已经确认的约束。后续仍以作者最新反馈为准。本文不代表当前所有视觉效果已经人工验收，也不代表工作区已经提交Git。

结算窗口接入后，TableDebug 的牌桌来自 `Assets/Prefabs/牌桌画布.prefab`。新窗口用 `MahjongSettlementView` 绑定。四座调试沿用既有场景入口，确认方式见 [单人四座调试](SOLO_DEBUG.md)，数据和换局逻辑见 [计分与整场](SCORING_AND_MATCH.md)。

## 1. 当前目标与协作方式

项目是Unity四人立直麻将，现阶段重点为作者自行重新排版的正方形牌桌UI。作者认可其作为最终对局场景基底，并持续自行调整场景、Prefab、素材和字体。下一步应沿用现有资源逐项完善。

- 仓库根目录：`G:/Documents/GitHub/MaJiang`。
- Unity项目：`MaJiangUnity/`，当前项目版本6000.0.64f1，uGUI/TMP。
- 当前UI入口：`MaJiangUnity/Assets/Scenes/TableDebug.unity`。
- Game参考比例1:1，CanvasScaler参考尺寸1080×1080。
- 代理负责代码、文档、CLI构建和适当的自动化检查。Unity场景操作、Play点击和视觉验收由作者执行；不要自行控制桌面代为验收。
- 作者可能同时编辑项目。已有大量未提交修改及未跟踪资源，包含作者的场景、Prefab、字体、图片与先前实现；不要整体回滚、重置、删除或重新生成场景覆盖这些内容。
- `MaJiang.CoreStandalone/`也是当前工作区已有内容，当前Unity UI链路不以它为入口；不要顺手清理。

## 2. 建议阅读顺序

1. 本文：UI当前行为、文件入口、注意事项与下一步。
2. [正方形UI绑定](SQUARE_UI_BINDINGS.md)：场景字段、方形布局与近期修正。
3. [Card与鸣牌组](CARD_AND_CLAIM_GROUPS.md)：Prefab内部契约、赤牌及副露牌位、紧密牌河。
4. [单人四座调试](SOLO_DEBUG.md)：启动、控制席、计时与真实网络输入。
5. [当前开发起点](CURRENT_BASELINE.md)：全项目范围与模块职责。

规则、状态和同步细节按需查阅 [规则与计分](RULES_AND_SCORING.md)、[状态机](STATE_MACHINE.md)、[计分与整场](SCORING_AND_MATCH.md)、[同步](MATCH_SYNCHRONIZATION.md)、[协议](NETWORK_PROTOCOL.md)。旧实现过程见[实现记录](IMPLEMENTATION_LOG.md)；历史记录中的较早设计不能覆盖本文的当前UI要求。

## 3. 入口场景与运行方式

| 资源 | 用途与修改边界 |
| --- | --- |
| `Assets/Scenes/TableDebug.unity` | 当前正方形UI与真实四座调试入口，后续在此基础上调整 |
| `Assets/Scenes/TableDebug 1.unity` | 作者保留的备份场景，不作为自动同步或覆盖目标 |
| `Assets/Scenes/TablePreview.unity` | 旧演示牌桌，Play后为固定演示；F1／右上角按钮可切入真实四座调试 |
| `Assets/Scenes/SampleScene.unity` | 作者原场景，保留 |
| `Assets/Prefabs/Table Canvas.prefab` | 旧牌桌资源，并未自动迁移为作者当前方形场景 |
| `Assets/Editor/TablePreviewBuilder.cs` | 旧场景生成器；不要为了刷新新UI直接运行生成流程 |

本节及下文未另说明的Assets路径相对于`MaJiangUnity/`。

TableDebug进入Play后，会在127.0.0.1临时端口自动创建一个服务端及四个真实TCP客户端，完成入房、准备和开局。不需要四个人、四个进程或填写IP。

F1或右上角“调试”按钮展开工具面板；数字键1–4或面板按钮切换控制席。默认跟随待操作席、暂停牌局计时。工具提供推进1秒／30秒、下一局和重开整场。需要按键时先让Game窗口获得焦点。

这仍是开发工具入口。正式创建／加入局域网房间、IP输入、准备和房主开局的产品UI尚未完整接入。

## 4. 固定公共桌面与控制席：必须分清两套座位

当前四座调试固定以绝对0号座位在下方作为公共桌面基准。公共座次依次为：下方自家、右侧下家、上方对家、左侧上家；按逆时针行动。

风位按当前庄家推算，庄家为东，下一个绝对座位为南，随后西、北。自家是东时，右侧南、对面西、左侧北。绝对0号座位并非整场永远东家。

切换控制席时：

- 只替换下方显示的暗手、独立摸牌与对应操作菜单。
- 公共牌河、姓名、点数、风位、立直棒和呼吸灯保持原来的固定座位，其他三席的副露继续显示在各自位置。
- 其他位置的暗手仍按公开张数显示牌背，不因为当前客户端恰好持有该席私有数据而显示第二份明手。
- 下方“调试文字”显示正在查看自家／下家／对家／上家的手牌，以及该玩家风位、姓名。
- 查看下家、对家或上家时，下方只显示该席的暗手和摸牌，自家副露暂时隐藏。切回自家时恢复当前自家副露。

实现入口：`MahjongTableDebug.Start`将`View.FixedTableSeat`设为0；`MahjongSeatOrder.PublicSeat`计算公共座位；下方暗手仍读取`VisibleMatchState.SelfSeat`。提交命令也使用真实控制席，不能将固定桌面基准当作命令身份。

普通单客户端保留`FixedTableSeat=-1`时的自身视角映射能力，但正式联机单玩家UI仍需后续接入。

## 5. 场景绑定和运行时覆盖

`MahjongTableView`集中保存显示引用。四席数组都按自家、下家、对家、上家排列。

| 字段 | 对象／数据 |
| --- | --- |
| `HandStrips[4]` | 四个手牌区Prefab实例的MahjongHandStripLayout |
| `Hands[4]`、`Melds[4]` | 各手牌区内的暗手／副露容器，SquareTable启用时从HandStrips更新 |
| `Rivers[4]` | 四席牌河，MahjongRiverLayout |
| `Players.Name` | 四个信息区的名称；等级和评级暂为作者的静态展示 |
| `Players.Points`、`Players.Wind` | 桌心各家点数、各家风位 |
| `Players.Riichi` | 各家立直棒对象 |
| `TurnLights[4]` | 桌心“当前玩家指示”下的自家／下家／对家／上家指示Graphic |
| `Round`、`Counters`、`Remaining` | 局目、本场数、剩余牌数 |
| `Deposits` | 顶部供托文字，棒数×1000点 |
| `Indicators` | 顶部五个宝牌指示牌，直接Card子节点 |
| `Actions`、`ActionLabels` | 七个操作按钮及文字，顺序见第8节 |
| `ChoicePanel` | 鸣牌选择面板，含排序列表、选择组Prefab、取消按钮 |
| `DebugHandLabel` | 作者新增的下方“调试文字” |
| `RoomButton` | 顶部“调试”按钮，当前用于展开工具面板 |
| `SettlementView` | 牌桌画布 Prefab 下的“结算窗口”，绑定和牌、番种、四席点数与确认按钮 |

新设计没有旧的Timer、Hint、Status、HeaderState、ResetButton和TurnBorder对象，相关字段允许为空。连接、计时、重开与详细阶段信息主要在工具面板。不要为了消除空引用把旧UI强行加回来。

`EnsureLayouts`会在启用及渲染时设置部分布局参数：手牌横排、副露组间隔0且向左增长、牌河固定六列紧排、按钮靠右间隔10、宝牌靠左。因此某些Inspector设置会在Play时被覆盖。改这类行为需同步修改代码，不能只调整被驱动的子物体坐标。

## 6. 手牌、摸牌与尺寸

统一使用`Assets/Prefabs/Card.prefab`，通常牌宽高比3:4。

- 门清通常为13张暗手，自己摸牌后增加1张独立摸牌。有副露时暗手数量相应减少。
- `HandTileList`为暗手；`SingleTile`为刚摸到的牌。独立摸牌不参加暗手排序。
- 手牌条整体长度固定。暗手从左排，副露固定右侧向左延伸，摸牌在暗手最右侧之后，留10个局部UI单位间距。
- 当前方形场景手牌基础宽度为40，手牌间距1；实际屏幕大小还受作者各条RectTransform缩放影响。10局部单位不等于任何分辨率下都固定10物理像素。
- 超宽时，暗手、摸牌、副露整体等比缩小以避免重叠；局部10单位摸牌间隔保留。副露普通牌与暗手底边对齐，上层杠牌允许向上溢出。
- 暗手排序只产生显示索引，不改变接收到的手牌列表。点击用原HandIndex和精确Tile身份，不能仅凭排序后的显示序号提交。
- 手切后，保留的摸牌进入暗手；摸切后独立摸牌消失；吃碰后的舍牌阶段没有新摸牌，独立Card应隐藏。
- 单击选中，再次点击确认出牌。当前鼠标悬停未选中手牌上移7单位，选中牌上移15单位；已经选中的牌不会另叠加悬停位移。

重点陷阱：Unity可能将布局驱动的Rect尺寸、缩放保存为0。`MahjongHandStripLayout.TileWidth`独立保存正常牌宽（默认40），排布不能从`Drawn.sizeDelta.x`反推正常牌宽。运行时View按对应牌宽更新TileWidth，停用独立摸牌自身AspectRatioFitter并显式设置尺寸；条布局再确定位置和统一缩放。

原先把比例模式枚举1判断为HeightControlsWidth是错误排查结论；项目实际uGUI中1为WidthControlsHeight，2为HeightControlsWidth。不要根据旧说明重新改反模式，也不要只凭YAML的0高度断定牌面不可见。最终显示效果仍以作者Play验收为准。

## 7. 最新蓝色遮罩要求

这是本轮最后确认的规则，替代此前“所有可见同牌都变蓝”的实现。

| 区域 | 可以触发悬停查询 | 可以显示匹配的蓝色遮罩 |
| --- | --- | --- |
| 下方当前显示的暗手、独立摸牌 | 可以 | 不可以，仅保留位移／选中反馈 |
| 其他手牌、牌背 | 不可以 | 不可以 |
| 任意玩家牌河的可见牌 | 不可以 | 可以 |
| 任意玩家副露中的可见牌 | 不可以 | 可以 |
| 暗杠的背面牌 | 不可以 | 不可以 |
| 宝牌指示牌、鸣牌选项牌、结算牌 | 不可以 | 不可以 |

匹配只看花色和点数：赤五与普通五属于相同牌面。鼠标移走、提交操作、切换控制席、禁用视图会清除遮罩。

`MahjongTableView.Hover`以牌是否位于`Rivers`或`Melds`容器中作为正向允许条件；`MahjongTableEffects.Highlight`还检查FaceVisible及同牌面。不要把它改回“只排除宝牌”的策略，否则手牌、选项和结算牌会再次被染蓝。

`MahjongTileWidget.Select`控制选中和抬牌，不负责打开BlueMask。`Bind`重置遮罩，`Highlight`控制遮罩。BlueMask和牌面图的RaycastTarget关闭，避免遮罩吞掉牌面点击。

## 8. 操作按钮与鸣牌选择

Actions顺序：0立直、1吃、2碰、3杠、4和牌、5跳过、6九种九牌。和牌文字按合法动作显示自摸或荣和；无合法动作时隐藏。

没有摸切按钮。点击独立摸牌确认打出提交TakeCut，点击暗手确认打出提交HandCut；先选立直则提交Riichi。跳过按钮只提交Skip，不能用来代替舍牌。底层TakeCut仍必须保留，工具列表也不再显示摸切按钮。

按钮使用`操作按钮.prefab`，固定80×80，从右起排，间隔10。多组合选择使用`鸣牌选择组.prefab`，固定两格总宽80、高约53.333，选择组之间间隔10。

- 唯一合法组合直接执行；多个组合才展开选择面板。
- 吃／碰显示实际使用的两张手牌，不显示被鸣来的目标牌。
- 234506筒吃4筒有23、35、30、56、06五种具体组合，不能合并赤五和普通五。
- 暗杠／加杠用“杠”文字加一张目标牌，隐藏另一张牌，保留两格总宽。
- 面板宽度取列表实际宽度加HorizontalPadding留白。关闭按钮锚点及轴心均为右上角，向内留6单位，不能固定在原始宽度的中心坐标上。
- 取消只关闭面板，不提交跳过。
- 选择组内Card Widget禁用指针处理，点击交给父级Button，避免子牌吞掉选择按钮。
- 必须提交原菜单中的具体MatchCommand／PlayerActionData对象，保留赤牌和来源身份；不要根据显示文字重新拼一个动作。

## 9. 副露、牌河与宝牌

副露组使用LeftClaimGroup、TopClaimGroup、RightClaimGroup。组内保留作者定义的FirstCard、SecondCard、ClaimedCard、KanCard名称，代码按这些名称查找。不要因场景中文命名要求破坏该Prefab绑定契约。

- 鸣牌来源相对拥有者的上家／对家／下家分别使用Left／Top／Right组；暗杠固定Top组。
- 吃：两张自牌按顺序放First／Second，鸣来的牌放Claimed，隐藏Kan。
- 碰：本人赤五优先First，鸣来的牌放Claimed，隐藏Kan。
- 大明杠：本人赤五优先First；若鸣来的为赤五则放Kan。
- 加杠：原碰的Claimed保留原身份，不能把原来鸣来的赤五移走；本人赤五仍优先First。
- 暗杠：First／Second牌背，赤五优先Kan。
- 整组按对应手牌牌宽缩放，组之间Spacing=0。作者三个组有默认示例牌面，用于编辑对齐；运行时按真实数据替换。

牌河使用继承GridLayoutGroup的MahjongRiverLayout，六列、左上起排，Card直接作为子节点，间隔0。立直牌横置后与同排牌底边对齐，同行后续牌按真实宽度顺延；所在行允许凸出容器，下一行从左边重置。不能恢复正方形格或牌布局单元预留，也不要加遮罩裁掉凸出的行。

宝牌列表直接五个Card，从左往右显示，未翻开格子显示牌背，不用牌布局单元。宝牌指示牌不参与蓝色遮罩。供托显示点数：棒数×1000，例如供托+1000，不是供托+1。

## 10. 呼吸灯

保留作者的指示灯颜色，以非缩放时间2秒周期在25%至100%透明度之间呼吸。

- 刚摸牌、等待舍牌、吃碰后的舍牌：当前行动者亮。
- 舍牌仍在响应窗口：原行动者继续亮。
- 无人可响应时，状态机可能直接进入下一家摸牌，不一定停留在响应阶段。
- 鸣牌被裁定后：原行动者灭，鸣牌者亮；普通跳过后转移给下一家。
- 加杠／暗杠抢杠响应窗口仍对应当前行动者。
- 和牌／流局等RoundEnded状态全部熄灭。

`MahjongTableEffects.ActiveSeat`决定绝对行动席，View按公共桌面基准映射TurnLights。不要按正在代操作的玩家点灯。呼吸周期和透明度范围当前写在View.Update中，尚未做成Inspector参数。

## 11. 代码职责与修改入口

以下脚本位于`Assets/Scripts/Presentation/`。

| 文件 | 职责 |
| --- | --- |
| MahjongTableView | 可见牌局渲染、区域绑定、公共座位、遮罩目标筛选、灯动画、结算展示 |
| MahjongTableDebug | 四座会话生命周期、控制席、点击提交、操作菜单、计时和跟随 |
| MahjongDebugPanel | 运行时独立uGUI/TMP工具面板，Canvas排序30000 |
| MahjongTablePreview | 旧演示及其本地对手逻辑，切入调试时解除监听并停用 |
| MahjongTileWidget | Card绑定、牌背／遮罩、悬停事件、抬牌、点击与精确牌身份 |
| MahjongTileArt | 牌面纹理、Sprite缓存、中央字体和OTF运行时回退 |
| MahjongHandStripLayout／Metrics | 固定长度牌条布局及纯数据几何计算 |
| MahjongHandOrder | 自动排序显示索引，保留原始数据及点击索引 |
| MahjongMeldPresentation | 面子到四牌位的纯显示映射，不修改Group |
| MahjongRiverLayout／Packing | 紧密六列牌河和纯数据排布 |
| MahjongChoicePanel | 多组合选项生成、外框宽度和取消 |
| MahjongTableLayouts | 标准Layout组件和必要的占位尺寸帮助方法 |
| MahjongSeatOrder／TableEffects | 座次、匹配遮罩和亮灯阶段的纯数据规则 |

普通暗手内部仍有用于Layout和悬停位移隔离的牌布局单元，这本身不属于待清理错误。禁止该单元的区域是牌河和宝牌列表；副露使用整组占位，不是逐张正方形格。

## 12. 字体、文字与资源编辑

文字使用TMP，不恢复Unity原版Text。场景及运行时新对象使用中文名称，保留作者已有Prefab内部绑定名。图片在`Assets/Images/`，字体位于`Assets/TextMesh Pro/Fonts/`。

牌桌和结算文字使用 `SourceHanSansSC-Heavy SDF.asset`。资产保留已有图集，通过思源黑体 OTF 动态补字，`MahjongTileArt.Font` 指向同一资产。

字符导出命令：`python tests/export-ui-characters.py`。输出位于`Assets/Presentation/`：

- `界面汉字.txt`：汉字集合。
- `界面完整字符集.txt`：生成字体图集时使用的完整字符集合，含数字、拉丁字母、符号。
- `界面全部文案.txt`：实际文案／动态模板，供作者拼UI查阅。

最近导出为406汉字、515完整字符、823条文案。新增玩家可见文案后重新导出；数量是导出时记录，不是未来固定规格。

## 13. 后端边界

UI读取VisibleMatchState，通过IMatchDecisionInput提交选择；不要从UI直接修改服务端手牌、点数或回合。

当前权威链为 LocalDebugMatch → LanRoomServer／LanRoomClient → NetworkDecisionInput → 原 MatchCommand。AuthoritativeMatchMachine 推进规则，MatchJournal／MatchSyncCodec 处理可见日志和检查点，RoundSettlement 处理结算。协议 v6 同步四席确认状态，继续使用 TCP、手写二进制及可分片的 1024 字节物理帧。

四座调试持有四个客户端，所以可以轮流看四个人的暗手；每个客户端本身仍只能收到自己的私有手牌。不能为方便UI将全员暗手放入公共快照，也不要用另一个玩家的状态替代当前命令身份。

作者已经逐项确认过规则边界，UI工作不应重新设计这些规则。若涉及流满、杠宝牌翻开时机、抢杠、包牌、连庄或终局，先查专项文档和实际规则代码；不确定再问作者。

## 14. 验证状态与下一步

本轮修改蓝色遮罩范围后，核心38/38通过，覆盖牌面匹配、赤五、仅牌河／副露可亮、暗杠背面排除，及原有状态／布局回归。资源检查通过；Unity主／Editor程序集CLI构建通过，0错误、0警告。没有代作者操作Unity，也没有完成逐项Play或视觉验收。

历史记录中网络78项、计分42项曾通过；本轮没有改协议或麻将裁定，没有重新跑全部网络／计分检查。不要把历史结果描述成当前全部重新执行。

仓库根目录常用命令：

```powershell
./tests/run-core-checks.ps1
python tests/check-table-ui.py
./tests/build-unity.ps1
python tests/export-ui-characters.py
```

若Unity生成工程缓存失效，先按`tests/README.md`处理restore；不为了CLI测试改Unity工程目标框架。测试不加载Unity，不替代真实场景布局、鼠标交互和动画验收。

作者下一轮可优先验证：悬停手牌只上移、牌河／副露同牌亮蓝但所有手牌不亮；移走／打出／切换席后清除；暗杠背面和宝牌不亮；多组合取消按钮留在框内；固定公共座次不随控制席改变；呼吸灯响应期间保持、裁定后转移；13+1及四副露后的摸牌仍可见。

待完善的界面范围包括作者反馈的具体错位、分辨率与溢出表现、调试界面信息层次、等级／评级真实数据、常规局域网房间 UI、动效和音效。

## 15. 下一对话可直接使用的说明

> 请先读docs/UI_HANDOFF_2026-11-20.md和docs/SQUARE_UI_BINDINGS.md，再查看当前TableDebug场景及Prefab。沿用作者的1:1场景继续完善UI，不重新生成覆盖布局。只有牌河与副露的可见牌可因手牌悬停显示蓝色遮罩，所有手牌仅位移。四座调试固定公共桌面，下方显示控制席的暗手和摸牌，查看其他席时隐藏自家副露。代码和CLI检查由你负责，Unity操作及视觉验收由我完成。
