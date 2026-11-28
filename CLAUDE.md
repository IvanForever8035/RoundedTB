# RoundedTB 项目说明

RoundedTB 是 Windows 任务栏定制项目，维护 WPF 稳定版与 Light 轻量宿主两条发布线。当前唯一工作仓库为本目录。

## 当前版本

- WPF `v1.0.6`：稳定线，GitHub Latest。
- Light `v0.1.1`：正式 Release `light-v0.1.1`；不标 Latest，WPF `v1.0.6` 保持 Latest。直接发布 `RoundedTBHost.exe`、SHA256、LICENSE 和 third-party notices；不是 WPF 稳定线的全面替代。
- Light 基于 `net9.0-windows`、framework-dependent，需要 .NET 9 Windows Desktop Runtime x64。该运行时已 EOL；多显示器、非底边停靠、Explorer 恢复等仍未完成平台验收。

## 结构与构建

- `RoundedTB/`：WPF 设置界面、稳定项目及统一 Light app 项目。
- `RoundedTB.Host/`：Light Win32 host、几何/自动隐藏逻辑、自测与发布脚本。
- `.github/workflows/ci.yml`：Windows 构建、自测及发布工作流。
- WPF stable 使用 Visual Studio MSBuild：`nuget restore RoundedTB.sln` 后执行 `msbuild "-property:Configuration=Release" -t:RoundedTB`。
- Light 使用 Windows MSBuild/.NET 9 SDK：`.\RoundedTB.Host\build-host.ps1 -OutputDir .\dist\light-publish -RunSelfTests`。
- 构建与 self-test 只验证对应逻辑，不代表 Windows 桌面平台矩阵已验收；用户当前使用暂未发现问题。

## 当前运行与数据

- EXE 可独立放在任意目录，无固定安装路径，运行时不会在 EXE 所在目录产生数据文件。
- 用户设置与日志：`%LOCALAPPDATA%\RoundedTB\rtb.json`、`rtb.log`、`rtb-host.log`。旧 `%LOCALAPPDATA%\rtb.json` 会在新文件不存在时复制迁移；仍缺配置时创建默认值，已有坏配置不覆盖。
- HKCU Run 值 `RoundedTB-Light` 由用户启用自启动时写入当前 EXE 路径；日志与托盘动态核对路径，不因启动另一位置的 EXE 自动改写。
- 默认启动 Win32 tray host；托盘 Settings 使用同一 EXE 的 `--settings-only --host-pid` 启动独立 WPF 子进程，随 host 退出关闭。
- 几何未就绪时交还 Explorer 原生 region 并重试；隐藏态 `TrayNotifyWnd` 零宽时使用 UIA 托盘边界兜底。hover 按启用分段触发，保留 Explorer 原生 retract。

## 工程与安全约束

- 注释、commit message 使用英文；面向用户使用中文。提交说明结尾使用 `Co-Authored-By: Claude Code <noreply@anthropic.com>`。
- 保持改动小且易回滚；不要手工修改 `bin/`、`obj/`。
- 每个 GDI region 必须明确交给 `SetWindowRgn` 或 `DeleteObject`；`SetWindowRgn` 成功后所有权转给 Windows。
- 不调用 `SPI_SETWORKAREA`；不要以循环 `ABM_SETSTATE` 做 watchdog。
- 任务栏动画期间不重设 region；不使用 alpha=1 伪装隐藏态。
- hover 每次 tick 用 `GetCursorPos` 读取实时坐标；Raw Input 仅是即时触发信号，不缓存坐标作为长期位置来源。
- 不假设 taskbar takeover 测试已获授权。真实 Explorer/taskbar 接管、Explorer 重启或异常恢复测试前，先取得用户明确授权；测试后验证 region 恢复。
- 修改自动隐藏/区域逻辑前先看 `docs/LESSONS.md`；当前设计补充看 `docs/DECISIONS.md`，状态与未验收项目看 `docs/STATE.md`。
