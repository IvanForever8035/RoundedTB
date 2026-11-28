# 当前项目状态

最后核对：2026-11-28

## 项目与发布

- WPF `v1.0.6`：稳定线，保持 GitHub Latest。
- Light `v0.1.1`：正式 Release `light-v0.1.1`（提交 `262372f`）；发布 run #111 成功，下载资产 SHA256 已核对，EXE FileVersion 为 `0.1.1.0`。不标 Latest。直接发布 `RoundedTBHost.exe`，无 ZIP；SHA256、LICENSE、THIRD-PARTY-NOTICES 为单独资产。
- Light 不是 WPF `v1.0.6` 的全面替代，兼容性平台矩阵仍未完成。

## 当前运行与数据

- EXE 可放任意目录，无固定安装路径，不在程序所在目录生成运行数据。
- 配置与日志位于 `%LOCALAPPDATA%\RoundedTB\`。优先迁移旧 `%LOCALAPPDATA%\rtb.json`；仍缺配置时创建安全默认值，已有非法或损坏配置不覆盖。
- HKCU Run `RoundedTB-Light` 在用户启用自启动时写入当前 EXE 路径。日志与托盘动态核对目标路径；启动另一个位置的 EXE 不会自动改写 Run 值。

## 当前实现

- Light 项目：`RoundedTB/RoundedTB.Light.csproj`，目标 `net9.0-windows`，framework-dependent `win-x64`，需安装 .NET 9 Windows Desktop Runtime x64。**.NET 9 已 EOL**。
- 默认启动轻量 Win32 tray host；Settings 通过同一 EXE 加 `--settings-only --host-pid` 启动独立 WPF 子进程，随宿主退出关闭；不带 host PID 的独立设置进程行为不变。
- 自动隐藏与 hover：隐藏态仅由命中的启用分段保留边缘 hit pixels；无目标时为空 region。离开分段后，可见态保留最后 region 交由 Explorer 原生 retract，稳定隐藏后再清空；过渡期间不重设 region。top margin 为 0 时无需 peek 条。
- 几何 readiness：应用栏/托盘启用但必要坐标未就绪时，清除自定义 region 交还 Explorer，测量成功后恢复分段。隐藏态 `TrayNotifyWnd` 可返回零宽矩形，现以 UIA 托盘边界兜底。
- 开机启动等待 taskbar 约 60 秒；几何测量启动后 60 秒内每 10 秒刷新，缺失值每 2 秒重试，失败不清除已有有效坐标。rebind/几何就绪后重新请求 auto-hide 对齐；诊断输出 `STARTUP context`、`GEOMETRY ready/pending`。
- Host hover 每次 tick 用 `GetCursorPos` 读当前位置，Raw Input 仅触发即时更新。

## 验证边界

用户当前使用暂未发现问题；既有单屏底边 hover、elevated Terminal 前台 hover、Settings 打开及正常退出恢复的确认，不扩展为全平台验收。以下仍未验收：

- 多显示器、混合 DPI、负坐标。
- 顶部/左右停靠（动态分段当前只支持水平布局）。
- 最大化窗口与 Alt+Tab 填充、TranslucentTB 联动。
- Explorer 重启、自动隐藏状态实际变化及恢复。
- 锁屏、全屏、远程会话输入；异常终止后的任务栏恢复。

真实 Explorer/taskbar takeover 测试需明确用户授权；CI/self-test 不代表平台实测。

## 下一步

按授权和平台条件继续未验收矩阵。评估从已 EOL 的 .NET 9 迁移到受支持 runtime，先确定发布目标与兼容策略。只更新当前事实，不维护本地工作区历史时间线。

## 相关文件

- 项目约定：`CLAUDE.md`
- 代码约束/经验：`docs/LESSONS.md`
- 当前架构和验收范围：`docs/plans/00-overview.md`
- Light 用户说明：`RoundedTB.Host/README.md`
