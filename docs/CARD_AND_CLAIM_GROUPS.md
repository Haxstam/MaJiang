# Card与鸣牌组接入

2026-11-19。按作者提供的Prefab统一显示牌。操作与视觉由作者在Unity验证。

## 资源与引用

- `Assets/Prefabs/Card.prefab`为统一单牌资源，附加MahjongTileWidget适配Image、Back和BlueMask。保留作者的牌体、面图和遮罩结构，Back及BlueMask改为随牌体尺寸伸展。
- LeftClaimGroup、TopClaimGroup和RightClaimGroup内部的Card实例沿用同一Card资源，保留作者的四个位置名称FirstCard、SecondCard、ClaimedCard和KanCard作为绑定契约。
- TableDebug、TablePreview和`Table Canvas.prefab`的旧静态牌已替换成关联Card的实例；各102张。牌桌TilePrefab引用已更新，后续生成器也只加载Card，不再生成旧MahjongTile资源。
- 旧`UI/MahjongTile.prefab`保留以免损坏额外旧引用，当前上述牌桌已不再引用它。作者已删除的旧Panel资源不恢复。

## 副露映射

2026-11-20编辑预览补充：三个组均直接保存默认示例牌面，First/Second/Claimed为普通五筒，Kan为赤五筒；四张均启用、背面与蓝色遮罩关闭，方便未进入Play时对齐完整组。组内Card实例默认尺寸统一30×40，修正旧宽度0覆盖与当前Card“宽度控制高度”模式冲突导致空白的问题。位置、旋转、缩放和锚点保留。运行时继续由以下映射替换示例、控制KanCard与背面，不将示例作为真实副露数据。

相对来源为上家/对家/下家时，分别使用Left/Top/Right组；以鸣牌者本人的座位计算，不以当前屏幕视角计算。暗杠固定Top组。

| 类型 | FirstCard | SecondCard | ClaimedCard | KanCard |
| --- | --- | --- | --- | --- |
| 吃 | 自己手中较小的牌 | 自己手中较大的牌 | 鸣来的牌 | 隐藏 |
| 碰 | 自己手中的第一张，赤五优先 | 自己手中的另一张 | 鸣来的牌 | 隐藏 |
| 明杠，鸣来的不是赤五 | 自己手中的第一张，赤五优先 | 自己手中的第二张 | 鸣来的牌 | 自己手中的第三张 |
| 明杠，鸣来赤五 | 自己的普通五 | 自己的普通五 | 自己的普通五 | 鸣来的赤五 |
| 加杠 | 本人牌中赤五优先 | 本人另一张牌 | 原先碰来的牌，保留原身份 | 本人剩余一张 |
| 暗杠 | 背面 | 背面 | 普通牌正面 | 赤五优先，否则普通牌 |

例如24s吃3s，First为2s、Second为4s；555p杠0p，Kan为0p；55p碰0p后加杠，0p始终留在Claimed。加杠新摸入赤五时，赤五移到First，本人其余普通牌填入Second与Kan。

MahjongMeldPresentation仅从现有Group生成显示映射，不改变Group.Tiles、SingleTile、FuluSource或操作机。组内各牌的RectTransform与位置沿用作者Prefab。整组按对应玩家的手牌牌宽等比缩放，以原30×40单牌为基准；组的布局占位同步缩放，副露区HorizontalLayoutGroup的Spacing固定0，不另加牌间或组间间隔。

## 牌河Grid

作者已确认六列Grid要求指牌河。2026-11-20进一步明确必须紧密排列，立直所在行可向外凸出。当前使用继承GridLayoutGroup的MahjongRiverLayout：Upper Left起点、Horizontal方向、Upper Left对齐、Fixed Column Count=6。

牌河直接包含Card实例，没有“牌布局单元”或行包装。Cell Size为正常牌的宽高（由RiverTileWidth及3:4比例确定），Spacing固定0；普通牌边缘紧贴。立直牌保持同一牌体尺寸、中心旋转90度并底边齐平，同行后续牌按横牌的实际宽度顺延，整行凸出长短边差；下一行仍从左侧重新起排。布局不为每格预留正方形，也不要求内容缩进牌河区域内。Padding可用于整区的外边距，不能作为牌间间隙。原RiverColumns字段只为旧序列化兼容保留，不作为列数入口。

例如30×40牌体：普通六张宽180，包含一张立直横牌的行宽190，下一行重新从0开始。代理没有添加遮罩裁剪牌河的外溢部分。

## 验证与边界

- 核心检查28/28通过：新加六项覆盖吃的顺序、碰的赤牌位置、明杠的赤牌位置、加杠保留原来源牌、暗杠背面与上层赤牌、全部座位下的相对来源方向。检查还确认显示不改变原数据、不丢牌或重复牌。
- Unity主程序集与Editor程序集CLI编译0错误0警告。
- 两个场景、牌桌Prefab与Card的本地fileID引用检查通过，无重复ID或悬空引用；三个鸣牌组Prefab与本轮开始前逐字节一致。
- 字符集重新导出为379个汉字、485个完整字符；使用静态字体图集时按需更新。
- 本轮未进行Unity交互验收，也未重新运行传输/规则的全部网络检查。仅改显示映射、资源与布局，没有改变网络协议或规则裁定。

本轮开始前的资源/代码局部备份位于`.utmp/baselines/2026-11-19-before-card-groups/`，包含作者已有的未提交Prefab修改。
