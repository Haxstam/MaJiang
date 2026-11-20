# 核心检查

在仓库根目录执行：

```powershell
./tests/run-core-checks.ps1
./tests/run-scoring-checks.ps1
python tests/check-table-ui.py
```

需要.NET 10 SDK，CLI缓存位于`.utmp/dotnet`。核心检查引用实际NetworkRuntime验证现用操作机/可见编码，但不打开Socket；计分检查直接编译共享核心。二者不加载Unity或场景，不下载第三方依赖，失败返回非零退出码。

当前38项检查验证基础状态与序列化回归，包括完整摸切牌山守恒、面子空值比较与哈希一致性；另覆盖自动理牌的显示索引，吃碰明杠/加杠/暗杠的赤牌牌位、隐藏标记和各座位来源方向，以及紧密六列牌河的相邻边贴合、横牌位于每一列时的凸出与换行复位。方形界面覆盖固定手牌条的边界、摸牌间隔、缩放，234506筒吃4筒的五种精确组合，真实四席摸切循环中的13+1同步／身份保密，以及所有庄家与观察者的逆时针座次风位。显示映射直接编译当前Presentation中的纯数据代码，不加载Unity。它们不替代完整规则、计分和Unity联机验收。范围、恢复基线与剩余问题见 [实现记录](../docs/IMPLEMENTATION_LOG.md)。

`check-table-ui.py`仅用Python标准库检查独立摸牌布局保存的正牌宽、比例模式和四席场景绑定，另检查直接宝牌子节点、框内关闭按钮、四席灯及下方手牌调试文字绑定。核心检查还覆盖滤镜仅允许牌河／副露，排除所有手牌、暗牌、宝牌等非目标区域、灯的响应阶段及固定公共座次。

`MaJiangUnity.sln` 保留 Unity 自动生成配置，不因测试工程更改目标框架。

## 网络与Unity构建

```powershell
./tests/run-network-checks.ps1
./tests/build-unity.ps1
```

网络检查78项：原17项传输/房间、16项日志、28项操作、8项结算/整场，另加6项恢复边界和3项单人四座调试检查。调试新增随机分座映射与暗手隔离、输入确认锁定、暂停与手动超时、完整一局及换局、启动中关闭和重建。仅使用127.0.0.1临时端口，受限沙箱可能需要允许回环连接。

计分检查42项，无需Socket或Unity：普通役/食下、平和20/30符、七对25符、双风雀头、边坎/两面、荣和刻子、最高得点拆分、宝牌与役分离、四类双倍与复合役满、累计封顶、无切上、多人支付跨座位0、包牌混合役满/大四喜双倍、暂时负分最终恢复、听牌罚符与流满、和止/听止、西入/西4及尾供托。结果与输入纯度分别验证，源文件与实际项目共用。

`MaJiang.NetworkRuntime`以netstandard2.1编译实际核心和网络源码，不引用Unity。`CoreSources.props`供运行程序集和计分检查共享核心源列表。旧ActionDeduce/DFS/512字节测试已迁至现用入口，见 [清理与恢复](../docs/CLEANUP_AND_RECOVERY.md)。

Unity生成工程暂未刷新时，`build-unity.ps1`通过`UnitySources.targets`加入新增网络源并移除已删除旧源，不修改生成的csproj；在Unity重新生成工程后同样可以使用。

Inspector与按键见 [局域网验收步骤](../docs/LAN_TEST_PLAN.md)，消息偏移见 [当前协议v5](../docs/NETWORK_PROTOCOL.md)，模型见 [状态机](../docs/STATE_MACHINE.md)、[计分整场](../docs/SCORING_AND_MATCH.md) 和 [同步设计](../docs/MATCH_SYNCHRONIZATION.md)。当前牌桌与四座调试已获作者初步人工测试反馈，常规房间UI及完整专项/跨机器验收仍待完成。已有结果的范围与后续起点见 [当前开发起点](../docs/CURRENT_BASELINE.md)。
