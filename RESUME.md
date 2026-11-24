# RESUME · RoundedTB 项目续接入口
最后更新：2026-11-24
当前阶段：Light 单 EXE 实验预发布故障排查与平台验收

> **给 Agent**：读到本文件后按顺序执行——
> 1. 读同目录 `CLAUDE.md`；
> 2. 读 `docs/STATE.md` 与 `docs/DECISIONS.md`；按当前任务需要再读 `docs/plans/00-overview.md` 或 `docs/LESSONS.md`；
> 3. 检查 `git status --short --branch` 与 `git log --oneline -10`；
> 4. 输出 ≤15 行简报：项目 / 当前阶段与进度 / 关键决策 / 下一步 / 待确认点；
> 5. **停住**等用户确认。若文档与实际代码不符先报告，不按旧状态硬干。

## 一句话目标

在 Windows 11 上维护 RoundedTB WPF stable 线，并逐步把已验证的轻量宿主发展为可支持的 Light 版本。

## 当前状态

- 已完成：WPF stable `v1.0.6`；Light 宿主源码进仓库；单 EXE 双进程 `.4` prerelease 已发布；旧 WPF Release 已补 LICENSE/third-party notices。
- 已完成：High integrity Terminal 聚焦时悬停几乎不触发的问题已修复（`2e87c99`，每 tick 用 `GetCursorPos` 采样），现场 A/B 通过，用户确认正常。
- 已完成：`light-v0.1.0-preview.5` 预发布（hover 修复；ZIP 只含程序 EXE，许可证作为独立 Release 资产）。Settings 面板已由用户确认可正常打开。
- 进行中：`.6` 统一数据目录（`%LOCALAPPDATA%\RoundedTB\`：rtb.json / rtb.log / rtb-host.log），EXE 旁不再写文件；CI 通过，待 tag。
- 下一步：发布 `.6`；之后继续多显示器/非底边/TTB/Explorer 重启等验收。

## 必须保留的决策

- WPF `v1.0.6` 是稳定线；Light `light-v0.1.0-preview.4` 是实验预发布。
- Light 单程序 EXE 保留 host 与 settings 两进程，入口参数 `--settings-only`。
- Light framework-dependent 目标 net9.0-windows，目标机需 .NET 9 Desktop Runtime x64；.NET 9 已 EOL。
- `.4` Settings 面板仍有用户报告故障；不得当成已修复或发布稳定版。
- 用户启动项仍指向旧 `F:\Program Files\Claude\RoundedTB-light\RoundedTBHost.exe`；迁移需另外确认。
- Explorer/任务栏真实测试需授权；测试后恢复 region 并验证 host/rollback。

## 本阶段相关文件

- 状态：`docs/STATE.md`
- 决策：`docs/DECISIONS.md`
- 阶段计划：`docs/plans/00-overview.md`
- Settings WPF：`RoundedTB/LightEntryPoint.cs`、`RoundedTB/App.xaml.cs`、`RoundedTB/MainWindow.xaml.cs`
- Unified project：`RoundedTB/RoundedTB.Light.csproj`
- Host：`RoundedTB.Host/TrayProbe.cs`、`RuntimeSettings.cs`、`build-host.ps1`

## 可选文件

- `docs/LESSONS.md`：修改 auto-hide/region 前查阅。
- `RoundedTB.Host/README.md`、`RELEASE-NOTES.md`：更新/审核 Light 包文档时查阅。

## 已知问题 / 风险

- `.4` Settings UI 现场启动未通过确认；只通过 CI 初始化 self-test。
- `.4` 包内含许可证文件；`.5` 起 ZIP 只含程序 EXE，许可证作为 Release 独立资产。
- 多显示器、非底边、TTB、最大化填充、Explorer restart、强杀恢复未验收。
- Light 单 EXE 使用 EOL .NET 9；非自包含发布需安装 Desktop Runtime。
