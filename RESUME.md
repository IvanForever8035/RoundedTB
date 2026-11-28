# RESUME · RoundedTB
最后更新：2026-11-28
当前阶段：Light `v0.1.1` 已正式发布，发布资产与 SHA256 已核对；用户当前使用暂未发现问题

> 续接时先读 `CLAUDE.md`，检查 `git status --short --branch` 和近期提交，再读 `docs/STATE.md`。若工作区状态或实际代码与文档不符，先报告并按实际情况更新，不沿用猜测。

## 当前项目

- WPF `v1.0.6` 保持 GitHub Latest；Light `v0.1.1` 已正式发布，不标 Latest，Release 附件与 SHA256 已核对。
- Light 是 .NET 9 framework-dependent 单 EXE，需要 .NET 9 Windows Desktop Runtime x64（已 EOL）；不是 WPF 稳定版的全面替代。
- EXE 可放任意目录，无固定安装路径；配置与日志位于 `%LOCALAPPDATA%\RoundedTB\`。

## 当前进度

- hover 按启用分段触发；离开分段后保留最后 region 交由 Explorer 原生 retract，稳定隐藏后再清空，动画期间不重设 region。
- 几何 readiness 门控在必要坐标缺失时清除自定义 region，交还 Explorer 并重试；保留已有有效测量。隐藏态 `TrayNotifyWnd` 零宽时通过 UIA 测量托盘，避免一直等待原生窗口矩形。
- 缺配置时创建安全默认值，现有坏配置不覆盖；设置子进程通过 `--host-pid` 随 host 退出关闭。
- 自启动路径由启用它的实例写入，日志与托盘动态核对是否指向当前 EXE，不自动改写。
- 用户当前使用暂未发现问题，不等于全平台验收。多显示器/混合 DPI、非底边停靠、最大化填充、TranslucentTB、Explorer 重启及强杀恢复仍未验收；真实 Explorer/taskbar takeover 测试需先获用户明确授权。

## 入口

- 项目约束与代码结构：`CLAUDE.md`
- 当前事实与验收状态：`docs/STATE.md`
- 当前设计约束：`docs/DECISIONS.md`
- 可复用工程经验：`docs/LESSONS.md`
- 当前架构与测试范围：`docs/plans/00-overview.md`
