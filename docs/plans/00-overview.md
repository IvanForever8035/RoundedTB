# 当前架构与验收范围

## 组件

- `RoundedTB/`：WPF 设置界面、WPF 稳定项目及 Light 统一项目。
- `RoundedTB.Host/`：Light Win32 host、任务栏区域/几何与策略、自测和构建脚本。
- `RoundedTB/RoundedTB.Light.csproj`：单 EXE host/settings apphost；无参数启动 host，`--settings-only` 启动 WPF 设置子进程。
- `.github/workflows/ci.yml`：WPF 与 Light 分开的 Windows CI/发布流程。

## 当前发布

- WPF `v1.0.6` 是 GitHub Latest 稳定线。
- Light `v0.1.1` 是独立正式 Release `light-v0.1.1`；直接提供 EXE + SHA256 + LICENSE + notices，不提供 ZIP，不标 Latest，WPF `v1.0.6` 保持 Latest。
- Light 使用 framework-dependent .NET 9 Windows Desktop Runtime x64；.NET 9 已 EOL。多种显示和恢复场景未验收，Light 不是 WPF 的全面替代。

## 当前功能边界

- 任务栏由 Explorer 原生 auto-hide 控制；hover 按启用分段触发，离开分段后保留最后 region 供原生 retract，稳定隐藏后再清空，动画期间不重设 region。
- 必要几何未就绪时清除自定义 region 交还 Explorer 并重试；隐藏态 `TrayNotifyWnd` 零宽时以 UIA 测量托盘边界，失败保留已有有效值。
- `WM_INPUT` 提供即时 Tick，hover tick 读取实时光标位置；正常关闭清理任务栏 region。
- Settings 由同一个 EXE 以独立进程启动，托盘启动附带 `--host-pid`，随 host 退出关闭。通过 `%LOCALAPPDATA%\RoundedTB\rtb.json` 共享配置，host 观察配置并热重载；缺文件时创建默认值，现有坏配置不覆盖。
- EXE 可放任意目录，无固定安装路径，数据只写入 LocalAppData。自启动由用户启用时保存当前 EXE 路径，日志与托盘动态核对，不自动改写。
- 多任务栏 HWND 路由与四边简单模式已实现；动态分段布局按水平轴实现，左/右竖直停靠不会应用动态分段。
- 真实 Explorer/taskbar 接管测试须先获用户明确授权；测试后确认 region 恢复和回滚状态。

## 当前已确认

用户当前使用暂未发现问题；既有单屏底部 auto-hide hover、elevated Terminal 前台 hover、Settings 打开及 host 正常退出恢复的确认仍限于对应场景。CI/self-tests 覆盖逻辑与安全自测，不等于所有 Windows 桌面场景通过。

## 尚未验收

1. 多显示器、混合 DPI、负坐标。
2. 顶部/左右停靠及动态竖向布局。
3. 最大化窗口/Alt+Tab 填充、TranslucentTB 联动。
4. Explorer 重启与自动隐藏状态改变/恢复。
5. 锁屏、全屏/远程会话输入，异常终止恢复。
6. 迁移至受支持的 .NET LTS runtime。

依据每项场景的实际证据更新 `docs/STATE.md`；不要记录完整工作区操作时间线。
