# 牌桌布局与自动理牌

2026-11-19。TableDebug继续作为对局场景基底。本轮替换列表内部的定位方式，原各区域RectTransform的位置、尺寸与朝向保留。

后续更新：牌河已按作者确认改用固定六列、左上角起排的GridLayoutGroup，副露改用作者整组Prefab、间距0，单牌统一Card。下文的行容器/内部逐张排副露为本轮早期实现记录，当前行为以 [Card与鸣牌组](CARD_AND_CLAIM_GROUPS.md) 为准。

## Layout结构

| 区域 | 布局组件 |
| --- | --- |
| 本家与别家手牌 | HorizontalLayoutGroup；摸牌前加入单独LayoutElement间隔 |
| 副露 | 区域HorizontalLayoutGroup，每组面子也是HorizontalLayoutGroup |
| 牌河 | VerticalLayoutGroup管理行，每行HorizontalLayoutGroup；默认每行6枚 |
| 操作按钮 | HorizontalLayoutGroup靠右排列，仅启用当前可用按钮 |
| 宝牌指示牌 | HorizontalLayoutGroup |
| 结算公开手牌 | HorizontalLayoutGroup |

两个场景已直接添加这些组件，原静态牌也放入布局单元/牌河行，可在Inspector查看。生成器和运行时使用同样的布局路径，不需重新生成整张场景。已有布局组件的间距、对齐和Padding保留，以便作者继续修改。

每张牌占据一个带LayoutElement的“牌布局单元”，内部是可交互的牌预制体。Layout只控制外层单元，选中/悬停抬起只移动内部牌，避免布局刷新把抬起效果复位。立直横牌的单元按横置后的宽高占位。

## 尺寸与调整

- MahjongTableView的SelfTileWidth决定本家手牌和本家副露牌宽度，OtherTileWidth决定别家手牌和对应副露牌宽度；牌高统一为宽度的4/3。副露不再另用缩小尺寸。
- RiverTileWidth控制牌河牌宽，RiverColumns控制每行数量，DrawGap控制摸牌前间隔。
- TileGap用于新建布局的初始间距、牌河行内和副露面子内的间距。已有区域Layout组件可独立调整Spacing、Child Alignment与Padding。
- 操作按钮的大小由其LayoutElement设置，已有设置保留；动态牌单元的LayoutElement由当前牌宽生成。
- 不再按牌河行数自动缩小牌，也不把多组副露强行压缩进原区域。副露变为和手牌同尺寸后占地会增大，作者可据实际画面拓宽/移动副露区，为完整副露预留空间。

## 自动排序与点击

本家暗手按万、筒、索、字牌以及牌号自动排序，同种普通五在赤五前，完全相同的牌维持原先相对顺序。摸牌继续单独显示在最后，舍牌后并入暗手的牌会在下一次渲染时参与理牌。切换到其他座位时，该座位作为本家同样排序；其他人的暗手只显示牌背。

排序通过MahjongHandOrder产生显示索引，不修改收到的HandTileList。每个按钮/牌的HandIndex仍指向原始手牌索引，摸切与手切、赤牌与普通牌保持正确身份。不提供拖拽/手动排序。

## 检查

- 两个场景各15个区域布局组件；原区域几何数据对比保留，新增层级无重复fileID或悬空本地引用。
- 核心检查22/22通过，新增显示排序对赤五/普通五、重复牌稳定顺序、原数组不变及点击映射的检查。
- Unity主程序集与Editor程序集CLI编译0错误0警告。
- 更新字符导出：374个汉字、480个完整字符。字体图集用法见 [TMP与命名](UI_TMP_AND_NAMES.md)。

实际分辨率下的间距、抬起动画、横牌占位及多组副露由作者在Unity验证；代理未操作桌面。
