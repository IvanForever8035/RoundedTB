# 当前设计约束

此文件只描述当前实现约定，不维护版本、发布或工作区历史过程。可复用的失败教训见 `docs/LESSONS.md`；项目操作规则见 `CLAUDE.md`。

## 自动隐藏与区域

- Windows Explorer 负责原生任务栏自动隐藏；Light 仅按设置调整任务栏窗口 region，不自行接管工作区。
- 不使用 `SPI_SETWORKAREA`；不循环调用 `ABM_SETSTATE` 做状态看门狗。
- 任务栏动画期间不重建或替换 region，以免中断 Explorer 动画。
- hover 仅按命中的启用分段触发；离开后保留最后 region 供原生 retract，稳定隐藏后再清空。
- 必要几何未就绪时清除自定义 region，交还 Explorer 并重试，不以空分段阻断唤出。隐藏态 `TrayNotifyWnd` 零宽时使用 UIA 托盘边界；失败不覆盖已有有效测量。
- 维持可控的正常退出恢复路径；强杀/异常恢复仍未完成全平台验收。

## Light host 与设置

- Light 是单 EXE、双进程：默认入口运行 Win32 host，`--settings-only` 启动 WPF 设置子进程。托盘启动时附带 `--host-pid`，设置随 host 退出关闭。
- EXE 可置于任意目录，无固定安装路径；设置由 `%LOCALAPPDATA%\RoundedTB\rtb.json` 共享，host 监视文件并热重载。设置 UI 与 host 日志分别写入同目录 `rtb.log`、`rtb-host.log`。
- 配置迁移后仍缺文件时创建安全默认值，已有坏配置不覆盖。自启动只由用户操作写入当前 EXE 路径，日志与托盘动态核对，不自动改写。
- Host 按每次更新时的 `GetCursorPos` 读取实时位置；Raw Input 只作为即时 Tick 提示。
- Dynamic 分段为水平布局；左右垂直停靠下不应用动态分段。

## 发布与支持

- WPF `v1.0.6` 保持 GitHub Latest 稳定线；Light `v0.1.1` 是独立正式 Release，不标为 Latest。
- Light 直接发布 EXE、SHA256、LICENSE 和 notices，不使用 ZIP；WPF 仍使用其现有发布流程。
- Light 使用 framework-dependent .NET 9 Windows Desktop Runtime x64；此运行时已 EOL。Light 的正式 Release 状态不等于所有平台场景都已验收，也不代表它是 WPF 的全面替代。
