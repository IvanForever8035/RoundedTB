# 阶段总览

项目：RoundedTB Windows 任务栏定制 fork
文档同步：2026-11-24
当前阶段：阶段 3 · 兼容性/恢复验收仍进行；阶段 4 · 轻量宿主实验性预发布已启动
稳定基线：[WPF GitHub Release `v1.0.6`](https://github.com/IvanForever8035/RoundedTB/releases/tag/v1.0.6)（`8a20a24`）。轻量宿主实验性 single-EXE prerelease：[`light-v0.1.0-preview.4`](https://github.com/IvanForever8035/RoundedTB/releases/tag/light-v0.1.0-preview.4)，不是稳定版。

---

## 阶段 0 · 文档基线（已完成）

- **目标**：让后续会话可按项目根目录 `RESUME.md` 接续。
- **交付物**：`CLAUDE.md`、`docs/STATE.md`、`docs/DECISIONS.md`、`docs/LESSONS.md`、本文件。
- **验收**：当前运行架构、回滚路径、已验证功能和限制已同步。

## 阶段 1 · WPF 稳定版验证与内存定位（已完成）

- **目标**：区分稳定基线、开发候选和运行期内存增长。
- **结论**：WPF 完整设置界面常驻是主要启动基线；轻量宿主原型大幅降低驻留内存。
- **交付物**：历史对照及 `docs/STATE.md` 中内存记录。

## 阶段 2 · 轻量宿主与按需设置（本机功能验收已完成；源码已纳入仓库）

- **目标**：常驻只保留 Win32/CLR 轻量任务栏宿主，设置 UI 按需启动并退出。
- **当前实现**：`RoundedTB.Host/` 是轻量宿主权威源码；最初双 EXE host/settings 架构保留为历史 `.1` 预发布；当前推荐的 `.4` 把两种模式合入一个 net9 framework-dependent EXE，默认 host、`--settings-only` 为 WPF settings child process。
- **已验证**：单屏底边 Raw Input 无探头条 hover 经用户手动确认；正常 host Exit 清 region；纯几何/策略 self-tests 和 CI 单 EXE 检查通过。旧双 EXE 架构的 Settings Apply/热重载历史测试不等于当前 `.4` WPF 设置面板已可用。
- **待确认**：`.4` 的 Settings... 用户报告打不开；分支有 `InitializeComponent()` 候选修复但无现场成功确认。当前 Light prerelease 暂不建议依赖 Settings；WPF stable `v1.0.6` 保持可用。
- **当前包**：实验性 prerelease `light-v0.1.0-preview.4`；ZIP 只有 `RoundedTBHost.exe`、`LICENSE`、`THIRD-PARTY-NOTICES.txt`。要求 .NET 9 Windows Desktop Runtime x64（EOL），非稳定版。
- **回滚**：停止宿主，运行 `F:\Program Files\Claude\downloads\candidate-69\RoundedTB.exe`（WPF rollback）；旧日常双 EXE package 仍位于 `F:\Program Files\Claude\RoundedTB-light`。


## 阶段 3 · 兼容性与恢复能力（进行中）

- **目标**：确认轻量宿主在不同显示布局、DPI、Explorer 重启和异常退出时可靠。
- **已完成源码项**：多任务栏 HWND 状态模型、monitor/DPI 几何路由、D4/D5 自动隐藏对齐与退出恢复、单 EXE settings-only 进程模式。
- **2026-11-24 新增**：High integrity 前台窗口下悬停不触发已修复（D13，每 tick `GetCursorPos`），现场 A/B 与用户确认通过；Light 包改为只含程序 EXE，许可证作为 Release 独立资产（D14）。
- **可交付物**：逐项测试记录，明确成功/失败/未覆盖，不把模拟自测当作平台验收。
- **待验收重点**：多显示器、顶部/左右停靠、锁屏/全屏/远程会话 Raw Input、Settings 面板现场启动与 Apply、Explorer 重启、appbar 状态实际改变与恢复、异常退出后的任务栏恢复。
- **已手动确认**：单屏底边 hover reveal/retract；正常 host exit region 恢复。settings WPF 初始化路径通过 CI 的资源/StartupUri self-test，但 `.4` 用户现场打开仍待确认。
- **安全边界**：每次真实 Explorer 接管/重启测试须先明确授权；正常使用继续使用已发布 `.4`，启动项尚指向旧目录。

## 阶段 4 · 实验性预发布与产品化（预发布已发布；稳定化待定）

- **已完成**：轻量宿主源码纳入 repo；WPF `--settings-only` 合并后同一个 net9 apphost 支持默认 host 与 settings child process；GitHub Actions 将 WPF `v1.0*` 与 Light `light-v*` 隔离；公开 prerelease `.4` 精简 ZIP 单 EXE + LICENSE + THIRD-PARTY-NOTICES。
- **待发布 `.5`**：包含 hover 修复（`2e87c99`）与新的打包规则（ZIP 只含程序 EXE，许可证/声明作为 Release 独立资产）。tag 尚未创建。
- **Settings 状态**：用户于 2026-11-24 确认 `.5` 的托盘 Settings 可正常打开；`.4` 的故障不再复现。`.6` 发布说明已去掉已知问题条目。
- **当前限制**：.NET 9 framework-dependent 且已 EOL，目标机需要 .NET 9 Windows Desktop Runtime。多显示器/非底边/TTB/maximise/Explorer restart/强杀恢复尚未完整验收。
- **下一步**：首先重现并定位 Settings UI 故障；真实 WPF UI smoke test 通过后才创建新 Light prerelease（`.5`），不重写 `.4`。之后继续平台验收和 runtime 支持迁移；完成前不能称稳定版或替代 WPF v1.0.6。
- **运行/启动项状态**：当前 `.4` 是用户从下载目录手动启动；HKCU Run 仍指向旧 `RoundedTB-light` 目录，未迁移。
- **WPF tag 保护**：旧 WPF Release `v1.0.x` 工作流与 `light-v*` prerelease 分开；许可证附件由 `license-attach-*` maintenance workflow 追加，不重建历史 ZIP。

---

## 可选文件判定（沿用）

| 文件 | 建 / 不建 | 理由 |
|---|---|---|
| `docs/LESSONS.md` | 建 | 有多种已否决方案，避免重复踩坑 |
| `docs/GLOSSARY.md` | 不建 | 术语数量少 |
| `docs/ACCEPTANCE.md` | 不建 | 个人项目，无外部验收方 |
| `.claude/settings.json` | 不建 | 构建与验证命令尚不稳定/频繁 |
| `.claude/agents/*.md` | 不建 | 暂无固定重复角色 |
| `docs/CHANGELOG.md` | 不建 | 版本发布记录由 Release 管理 |
