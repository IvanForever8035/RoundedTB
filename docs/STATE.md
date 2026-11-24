# 项目状态

最后更新：2026-11-24
当前阶段：Light 单 EXE 实验预发布故障排查与平台验收

## 项目目标

RoundedTB fork：保留 WPF 稳定版，同时逐步验证轻量 Windows 11 任务栏宿主。稳定发布与实验性 Light prerelease 分开。

## 当前运行状态

- **当前会话运行**：2026-11-24 现场验证后，运行分支 CI 构建的候选 `F:\Program Files\Claude\downloads\light-candidate-2e87c99\run\RoundedTBHost.exe`（commit `2e87c99`，PID 20200）；原 `.4` 已正常退出。候选位于下载目录，重启后不会自动沿用。
- **重要现场问题（已解决）**：用户反馈 Terminal 聚焦时任务栏悬停几乎无法触发。根因是 host 复用最后一次 Raw Input 坐标，而 Raw Input 在 High integrity 前台窗口下可能停止送达。修复为每个已有 tick 用 `GetCursorPos` 采样实时光标（`2e87c99`）。现场 A/B：`.4` 6 次中 2 次失败，候选 10/10 成功（elevated Windows Terminal 前台）；用户确认为“没问题了”。
- **Settings 面板**：用户于 2026-11-24 确认 `.5` 的托盘 Settings 能正常打开（设置子进程已启动并显示），`.4` 的故障在 `.5` 已不复现。Apply/热重载此前已验证。
- **开机启动项**：HKCU Run 仍指向旧路径 `F:\Program Files\Claude\RoundedTB-light\RoundedTBHost.exe`（旧 96,768 B .NET Framework 宿主）；未迁移。手动启动的下载目录候选不等于重启后会使用它。
- **旧便携目录**：`F:\Program Files\Claude\RoundedTB-light` 保留双 EXE 旧包。未获用户授权前不要替换该目录或更改启动项。
- **配置/日志（`.6` 起）**：统一在 `%LOCALAPPDATA%\RoundedTB\`：`rtb.json`、`rtb.log`（设置 UI）、`rtb-host.log`（host）。旧 `%LOCALAPPDATA%\rtb.json` 首次运行自动复制迁移，旧文件保留。EXE 旁不再写 `probe.log`；旧 `probe.log` 可删除。
- **当前配置行为**：`ShowAppList=false`、`ShowTray=false`、`TrayHoverEnabled=true`，悬停只在托盘区域触发，底部中间/左侧不触发是配置行为。
- **回滚**：退出新 host 后，可运行旧 WPF `candidate-69`：`F:\Program Files\Claude\downloads\candidate-69\RoundedTB.exe`。

## 发布状态

- WPF 稳定版仍为 `v1.0.6`（`8a20a24`）。
- Light `.4`：`light-v0.1.0-preview.4`；framework-dependent 单文件，目标机需安装 .NET 9 Windows Desktop Runtime x64。**.NET 9 于 2026-11-10 EOL**，这是实验性预发布，不是稳定版。
- `.4` ZIP 14,053,004 bytes，SHA256 已与侧车文件核对一致；包含 `RoundedTBHost.exe`、`LICENSE`、`THIRD-PARTY-NOTICES.txt`。
- **发布包规则（2026-11-24 起）**：Light ZIP 只包含程序 EXE；`LICENSE` 与 `THIRD-PARTY-NOTICES.txt` 作为 GitHub Release 的独立资产上传，不再放进程序包。CI `ci.yml` 的 light job 已按此调整（校验包内只有 1 个文件）。
- `.1`–`.4` tags/assets 均保留，不重写。**`.5` 已发布**：`light-v0.1.0-preview.5`（commit `5f53f3b`），prerelease；资产为单 EXE ZIP、SHA256、`LICENSE`、`THIRD-PARTY-NOTICES.txt`（后两者独立，不在 ZIP 内）。包含 hover 修复（`2e87c99`）。
- **`.6` 已发布**：`light-v0.1.0-preview.6`（commit `7783efc`），prerelease；统一数据目录到 `%LOCALAPPDATA%\RoundedTB\`，EXE 旁不再生成文件；Settings 已知问题条目已移除。
- **`v0.1.0` 已发布**：`light-v0.1.0`（commit `b725e58`），非预发布、非 Latest；资产为 `RoundedTBHost.exe`、`RoundedTBHost.exe.sha256`、`LICENSE`、`THIRD-PARTY-NOTICES.txt`，无 ZIP。WPF `v1.0.6` 仍为 GitHub Latest。
- WPF Release 资产 v1.0.1–v1.0.6 已通过 `license-attach-*` workflow 附加 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`；原 ZIP 未重建。v1.0.0 无 Release，跳过。
- CI workflow 将 WPF `v1.0*` stable 与 Light `light-v*` prerelease 隔离。本机无 .NET SDK/MSBuild、无 `gh`；Windows CI 负责 Light 构建/发布。

## 已完成

- [x] WPF fork 与 stable Releases v1.0.0–v1.0.6。
- [x] 轻量宿主源码纳入 `RoundedTB.Host/`，统一 WPF/Win32 程序进入 `RoundedTB/RoundedTB.Light.csproj`。
- [x] 单 EXE 双进程入口：默认 Win32 host，`--settings-only` WPF 设置子进程；统一入口为 `RoundedTB/LightEntryPoint.cs`。
- [x] 设置配置解析使用 `System.Text.Json`，托盘图标嵌入统一 EXE。
- [x] 多任务栏 HWND 模型、纯几何四边/负坐标路由模型、D4/D5 自动隐藏策略和正常退出恢复策略。
- [x] 单屏底边 hover reveal/retract 经用户手动确认；正常 host exit 清理 region 已实测。
- [x] 单 EXE `preview.4` CI publish、自检、包内 EXE 数量检查通过；旧 WPF release 的许可附件补挂完成。

## 已验证 / 未验证

- **已验证**：纯逻辑与自有 HWND tests；单屏底边 hover；托盘配置改动的旧架构热重载；正常退出清理 region；`.4` ZIP 只有一个程序 EXE；License/notices 附件存在。
- **待确认/失败报告**：当前 `.4` Settings 面板用户报告打不开；CI 只测试入口参数、WPF 资源/StartupUri 初始化，不等于成功呈现真实窗口。候选修复尚未由用户现场确认。
- **未验收**：最大化/Alt+Tab 填充、TranslucentTB、多显示器与混合 DPI、四边真实 Raw Input、Explorer 重启、ABM_SETSTATE 非幂等状态改变、强杀恢复。

## 当前限制 / 下一步

1. 先重现并定位 `.4` Settings 不打开的根因；在真实 settings UI smoke test 通过前，不发布修复 tag，不宣称 Settings 可用。
2. 修复通过后由 Windows CI 构建/跑 self-tests，出新的 prerelease（不覆盖 `.4`）；发布后再更新手动解压版。
3. 用户授权后再考虑迁移启动项/旧便携目录；保留回滚可执行文件与当前配置。
4. 后续才继续多显示器、四边、Explorer 重启、TTB、最大化填充及 runtime EOL 迁移评估。
5. 只有完成平台矩阵并选择受支持 runtime 后，才评估 Light 稳定版；WPF `v1.0.6` 仍是稳定基线。

## 决策索引

详见 `docs/DECISIONS.md`：D7 无探头条 Raw Input；D8 host/settings 分离；D9 WPF fill/CompositionCompat 迁移；D10 多任务栏模型和动态布局限制；D11 D4/D5 appbar 对齐；D12 单 EXE 双进程、net9 prerelease。

## 已知经验

- region 成功传给 `SetWindowRgn` 后所有权转给 Windows；失败/未交付需 `DeleteObject`。
- 任务栏动画期间不重设 region；不修改工作区、不使用 alpha=1、不以 `ABM_SETSTATE` 做 watchdog。
- 详见 `docs/LESSONS.md`。

## 可选文件索引

| 文件 | 用途 | 何时查阅 |
|---|---|---|
| `docs/LESSONS.md` | 已否决方案和区域/自动隐藏坑 | 修改 region、hover、auto-hide 前 |
| `docs/DECISIONS.md` | 架构决策历史 | 改 host/settings/发布模式前 |
| `docs/plans/00-overview.md` | 阶段/发布范围 | 阶段切换、平台验收规划 |
| `RoundedTB.Host/README.md` | Light 包使用/runtime说明 | 修改用户包行为或需求时 |
| `RoundedTB.Host/RELEASE-NOTES.md` | prerelease 页面正文来源 | 新建 Light prerelease 前 |

## 常用路径

- WPF 稳定源：`RoundedTB/`
- Light host 源与自检：`RoundedTB.Host/`
- Unified app project：`RoundedTB/RoundedTB.Light.csproj`
- Light publish script：`RoundedTB.Host/build-host.ps1`
- WPF stable `v1.0*` 与 Light `light-v*` workflow：`.github/workflows/ci.yml`
- License backfill workflow：`.github/workflows/attach-license.yml`
- Light package download：https://github.com/IvanForever8035/RoundedTB/releases/tag/light-v0.1.0-preview.4
