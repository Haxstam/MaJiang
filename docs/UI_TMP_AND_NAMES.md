# TMP字体与中文对象命名

2026-11-19。TableDebug作为后续对局场景基底，继续沿用作者调整过的布局。

## 修改内容

- TableDebug、TablePreview的原版Text替换为TextMeshProUGUI，MahjongTableView中的文字引用改为TMP_Text。保留原组件fileID、RectTransform和按钮/视图引用。
- 四座调试面板由IMGUI迁移为uGUI与TMP，沿用切座、自动跟随、计时控制、合法操作、重开和换局功能。
- 两个牌桌场景和单牌Prefab中的GameObject改用中文名；牌桌生成器与运行时创建的牌、面板、按钮也使用中文名。脚本类名、资源路径和组件类型名保持其程序用途，不属于场景对象命名。
- 本次不重建牌桌布局，也不操作作者的Unity编辑器。重新导入、实际字体效果与交互由作者验证。

## 生成与指定字体

字符文件位于Unity项目的`Assets/Presentation/`，均为UTF-8文本：

| 文件 | 用途 |
| --- | --- |
| `界面汉字.txt` | 当前源码字符串与牌桌场景中的去重汉字，含简体、已有繁体/日文汉字 |
| `界面完整字符集.txt` | 上述汉字加数字、英文字母、标点与界面符号；推荐用它生成字体图集 |

本次导出为373个汉字、479个完整字符；重新导出后以脚本输出为准。

后续Layout迁移新增对象名后，清单已更新为374个汉字、480个完整字符。

在TMP Font Asset Creator中，Source Font File选择已导入的`SourceHanSansSC-Heavy.otf`；Character Set选择Characters from File，Character File选择`界面完整字符集.txt`，生成并保存字体资产。

选中`Assets/Presentation/MahjongTileArt.asset`，将新生成的TMP字体资产拖到`Font`字段，即可统一用于牌桌及调试面板。`Source Font`保留原OTF引用，在未指定TMP资产时为中文提供动态字体回退。已有单独指定字体的TMP文字在中央Font留空时保留自己的字体；中央Font明确指定后，以中央设置为准。

字体图集尺寸、采样字号由实际清晰度和打包结果决定；Font Creator如果报告未打包字符，需要调整后重新生成。这里提供的是当前固定文案字符集，任意玩家名字或未来新增文字不保证已被静态图集覆盖；需要时使用动态填充或追加字符。

修改文案后，从仓库根目录运行：

```powershell
python tests/export-ui-characters.py
```

脚本只用Python标准库，从实际源码字符串和两个牌桌场景重新导出，排除代码注释，并保持已有TextAsset的meta GUID。

## 后续编辑注意

- 当前可继续直接调整TableDebug，生成器不是日常调整入口；不要为了获得TMP组件重新生成整张桌面。
- 原“Footer Preview Mode”对象现在名为“底部模式说明”，运行时控制器的查找同步更新。
- 调试面板由MahjongDebugPanel在运行时创建，所有文字使用TMP；它独立显示在牌桌上层，不改变牌桌布局。
- 旧SampleScene及无关工程未纳入此次牌桌迁移。

代理检查组件引用、布局数据及程序集编译；作者负责Unity重新导入后的字体、换行、视觉与操作验证。

本次结构检查：两个牌桌场景各56个Text迁移为TMP、639个GameObject中文命名，单牌Prefab的5个GameObject中文命名；所有原有fileID、GameObject组件列表和RectTransform数据对比通过。Unity主程序集与Editor程序集CLI编译0错误0警告；未执行桌面操作验收。
