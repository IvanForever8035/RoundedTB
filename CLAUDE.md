# CLAUDE.md · RoundedTB fork

> 项目续接唯一入口：`RESUME.md`。详细进度见 `docs/STATE.md`；决策见 `docs/DECISIONS.md`。

## 项目

RoundedTB Windows 任务栏定制 fork（圆角、分段、悬停、自动隐藏）。

- 上游：`upstream` → `RoundedTB/RoundedTB`
- Fork：`origin` → `IvanForever8035/RoundedTB`
- **当前会话 Light 进程**：详见 `docs/STATE.md`，用户最近手动运行 `.4`，Settings 面板有故障反馈。
- **开机启动项**：仍指向旧 `F:\Program Files\Claude\RoundedTB-light\RoundedTBHost.exe`，尚未迁移到单 EXE。
- **发布**：WPF stable `v1.0.6`；Light experimental `light-v0.1.0-preview.4`。Light 是 framework-dependent .NET 9 单 EXE，要求 .NET 9 Windows Desktop Runtime x64（EOL）。不得将其称为 stable。
- **源码/构建**：`RoundedTB.Host/`、`RoundedTB/RoundedTB.Light.csproj`、`RoundedTB.Host/build-host.ps1`；本机无 .NET SDK/MSBuild，CI 构建。

## 常用命令

原 WPF 工程（需 VS MSBuild，`COMReference IWshRuntimeLibrary` 使 `dotnet build` 不适用）：

```powershell
nuget restore RoundedTB.sln
msbuild "-property:Configuration=Release" -t:RoundedTB
```

轻量单 EXE 预发布使用 Windows MSBuild/.NET 9 SDK（构建机），命令：

```powershell
.\RoundedTB.Host\build-host.ps1 -OutputDir .\dist\light-publish -RunSelfTests
```

脚本执行 framework-dependent single-file `win-x64` publish；运行机器需 .NET 9 Windows Desktop Runtime。当前 runtime 已结束支持，仅用于实验性 prerelease。不要替换用户日常宿主或操作 Explorer，除非用户明确授权。

- 配置：`%LOCALAPPDATA%\rtb.json`
- 日志：`%LOCALAPPDATA%\rtb.log`（WPF）；轻量宿主诊断日志在其运行目录 `probe.log`。
- 开机启动：轻量宿主托盘菜单 `Start with Windows`（HKCU Run，默认关闭）。

## 代码结构

```text
RoundedTB/                         WPF stable project + light unified app project
  Taskbar.cs                       WPF region, auto-hide, UIA geometry
  MainWindow.xaml(.cs)              WPF settings UI; `--settings-only` safe mode
  LightEntryPoint.cs                light default-host / settings-only process dispatch
RoundedTB.Host/                     light Win32 host sources, geometry, tests and build script
F:\Program Files\Claude\prototype  historical local prototype copy
F:\Program Files\Claude\RoundedTB-light  daily portable package (local)
```

## 代码约定

- 注释、commit message 用英文；与用户用中文。
- commit message 结尾：`Co-Authored-By: Claude Code <noreply@anthropic.com>`。
- 改动小、可回滚；不顺手重构。
- 每个 GDI region 必须明确交给 `SetWindowRgn` 或 `DeleteObject`；`SetWindowRgn` 成功后 region 所有权属于 Windows。
- 动 auto-hide/区域逻辑前读 `docs/DECISIONS.md` 与 `docs/LESSONS.md`。
- 轻量宿主真实接管测试必须先获用户明确授权；结束后恢复默认区域并确认回滚宿主已运行。模拟测试只能作用于原型自身 HWND。

## 禁区

- ❌ 在 WPF 旧路径中用 `ABM_SETSTATE` 反复切换自动隐藏；会改变工作区/最大化窗口尺寸。
- ❌ 给处于 Explorer 原生自动隐藏的任务栏保留空区域（WPF 方案会失去命中像素）。轻量宿主的“无探头条”方案仅允许在 Raw Input 已注册且恢复路径受控时使用，见 D7。
- ❌ 修改工作区（`SPI_SETWORKAREA`）。
- ❌ 任务栏滑动动画期间重设 region；会中断动画。
- ❌ 每 100 ms 无条件重建 region、UIA 树或 managed geometry。
- ❌ 直接向 `master` 提交；只有用户明确要求发版才打 tag。
- ❌ 手工修改 `RoundedTB/bin`、`obj`。

## 已知限制

详见 `docs/STATE.md`。Light `.4` 已确认单屏底边 hover，**Settings 面板仍有用户报告打不开**；多屏、非底边、Explorer restart、TTB、最大化填充、force-kill recovery 未验收。
