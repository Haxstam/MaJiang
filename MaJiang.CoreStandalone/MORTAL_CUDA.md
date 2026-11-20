# 使用现有 venv 的 CUDA 推理

2026-10-25 已在本机验证，当前项目可以直接使用 GPU 运行 Mortal-S42。

## 已确认的环境

| 项目 | 实测值 |
| --- | --- |
| Python | `D:\PycharmProjects\Envirment1\venv\Scripts\python.exe` |
| Python版本 | 3.10.10，64位 |
| PyTorch | `2.12.0+cu126` |
| PyTorch CUDA | 12.6，`torch.cuda.is_available()` 为 true |
| GPU | NVIDIA GeForce RTX 4050 Laptop GPU，6GB显存 |
| 实际模型参数 | Brain和DQN均在 `cuda:0` |

没有重装 CUDA、驱动、PyTorch或NumPy，也没有修改该venv中的包、原Unity项目、原Mortal源码或权重。

## 直接启动

在本文件夹执行：

```powershell
# 检查模型实际在GPU推理，运行2组张量及8个合成决策场景
./run-mortal-cuda.ps1 -Smoke

# 四家CUDA模型，各承担一种诅咒，明确推进至整场结束或100局上限
./run-mortal-cuda.ps1

# 指定种子、局数和输出目录
./run-mortal-cuda.ps1 -Seed 2 -Rounds 4 -Output .utmp/cuda-case2

# 不启用诅咒的普通模式
./run-mortal-cuda.ps1 -Curses '' -Rounds 4
```

默认使用已确认的venv、当前模型部署目录、四个座位、`cuda`和诅咒分配`1,2,3,4`。通过 `-Python`、`-ModelDirectory`、`-Seats`、`-Curses` 修改对应参数。`-NoBuild` 跳过.NET构建，使用现有Release产物。

默认记录到 `experiments/match-<唯一ID>/`，开启详细调牌审计。验证时使用 `-Purpose validation`，测速使用 `-Purpose benchmark`，可用 `-NoAudit` 关闭详细审计。完整成功动作逐步落盘、每局及整场结果、CSV导出与两场并发实测见 [EXPERIMENT_RECORDING.md](EXPERIMENT_RECORDING.md)。当前两个相同半庄串行共65.95秒、两场并发40.59秒，吞吐约提升1.62倍；不是大规模统计结果。

脚本明确要求CUDA可用，不会静默退回CPU。各座位进程加载一次模型，并在对局期间保留；脚本结束时释放进程。`matchEnded:false` 的有限局数验证不能当作完整半庄样本计入夺一率。

## Python 3.10兼容处理

旧部署使用Python 3.12，直接换成这个venv会遇到两处兼容问题：

1. 原 `libriichi.pyd` 不能由Python 3.10加载。已用现有Rust源码及缓存依赖离线编译与当前Python匹配的扩展，放在本项目 `.utmp/python-native/cp310-win_amd64/libriichi.pyd`。
2. 原模型加载器使用Python 3.11起提供的 `hashlib.file_digest`。项目本地桥接器只在该加载器命名空间提供兼容实现，仍按原SHA-256身份校验权重；不替换标准库，也不改原部署文件。

兼容桥接器位于 `python/mortal_bridge.py`，通过 `MortalBridgeOptions.BridgeScriptPath` 和 `NativeModuleDirectory` 选择。它加载原部署的 `inference.py`、原模型代码和权重，保持每行事件对应一个 `ok/reaction` 响应，并继续遮蔽他家手牌和摸牌。运行诊断只写stderr。

编译产物位于忽略的临时目录。清理临时目录、移动项目或换Python ABI后，先重新生成：

```powershell
./build-mortal-native.ps1
# 如需另一环境：
./build-mortal-native.ps1 -Python '其他环境/Scripts/python.exe' -MortalRoot 'G:\Mortal-main'
```

该脚本使用本机Rust工具链、原源码和已缓存依赖，执行 `--locked --offline`，输出也仅在项目临时目录；不往venv安装包。新机器没有这些缓存时需要另行准备构建依赖。

## 验证结果

- 原150项.NET检查全部通过，构建0错误、0警告。
- GPU冒烟测试通过：2组张量、四个座位共8个合成场景，含立直后舍牌及自摸。
- 四家真实CUDA模型启用诅咒，种子2完成4局、201次模型操作，无本地默认操作替代；包含荣和、自摸、九种九牌、荒牌、立直和鸣牌。
- 本次诅咒统计中，效果2发生1次活牌交换，效果3发生16次活牌交换及1次王牌交换，效果4发生4次活牌交换。

这验证GPU接入与运行链路，不是大规模夺一率实验，也没有据此认定GPU相对CPU的吞吐提升比例。结构化结果见 [validation/Mortal-CUDA.json](validation/Mortal-CUDA.json)。已有国士抢暗杠模型兼容性限制仍按 [CURSES.md](CURSES.md) 处理。
