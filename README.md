开源的联网麻将项目，正在施工中
使用.NET和Unity(6000.0.64f1)

后续开发先读 [当前开发起点](docs/CURRENT_BASELINE.md)。开发路线见 [项目完善规划](docs/DEVELOPMENT_PLAN.md)，规则范围与核实状态见 [规则与计分基准](docs/RULES_AND_SCORING.md)。

2026-10-25：已接通局域网房间、日志、操作与计分/整场，并增加使用现有牌桌的单人四座调试。打开 `Assets/Scenes/TableDebug.unity` 进入 Play，或在TablePreview中按F1/点击“四座调试”，即可在一台电脑上代选四名玩家的操作。UI修复后作者初步测试反馈没有问题，以此作为下一阶段起点；不代表完整规则或跨机器验收。最近网络78/78及Unity/Editor编译通过。见 [单人调试](docs/SOLO_DEBUG.md)、[检查入口](tests/README.md) 和 [Unity验收](docs/LAN_TEST_PLAN.md)。

进度:
1.基本逻辑（现用规则、操作、计分与整场推进已有回归检查）
2.网络交互（局域网房间、同步与恢复已实现，跨机器待验收）
3.界面实现（牌桌与四座调试初步可用，常规房间UI待接入）
4.美术内容（牌面素材已接入，布局和展示继续完善）
