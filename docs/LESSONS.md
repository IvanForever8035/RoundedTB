# 工程经验与强约束

只保留仍适用于当前代码的失败教训与操作约束，不记录每次工作区操作或历史发布流程。

## 任务栏区域与自动隐藏

- Explorer 原生 auto-hide 依赖屏幕边缘可命中的任务栏像素；改变 region 时必须保留正常 reveal/retract 路径。
- 不在任务栏滑动动画期间重设 region，否则 Explorer 可能跳到动画终点。
- 不修改 `SPI_SETWORKAREA`；不要用循环 `ABM_SETSTATE` 作为 watchdog，这会触发布局/工作区变化并影响最大化窗口尺寸。
- 不用 alpha=1 伪装隐藏任务栏；host 异常终止时窗口可能停留在近透明状态。
- GDI region 必须明确交给 `SetWindowRgn` 或 `DeleteObject`；`SetWindowRgn` 成功后所有权属于 Windows。
- 正常退出与恢复路径是功能的一部分；真实 takeover 测试后检查 region 已还原。

## Hover 输入

- Raw Input（`WM_INPUT`）可触发及时更新，但不能将其最后一次坐标当作长期缓存：高完整性前台窗口时消息可能不再送达。
- 每次 hover 更新通过 `GetCursorPos` 获取当前光标；保留既有 timer 作为 Raw Input 不可用时的兜底。
- hover 只响应命中的启用分段；鼠标离开后不要立即清掉可见态 region，应保留到 Explorer 原生 retract 完成。

## 几何、配置与进程生命周期

- 隐藏态 `TrayNotifyWnd` 可返回零宽矩形，不能视为托盘不存在；使用 UIA 托盘边界兜底。必要几何未就绪时交还 Explorer 原生 region 并重试，测量失败不丢弃已有有效值。
- 首次运行不能依赖已存在的配置文件：迁移后仍缺配置才创建默认值，现有坏配置保留，不静默覆盖。
- 设置是独立子进程；通过 `--host-pid` 跟随 host 退出，关闭设置窗口不应停止 host。
- 自启动保存完整 EXE 路径；EXE 移动或从其他目录启动后应动态核对目标，但不能未经用户操作自动改写。

## 验证边界

- 用户当前使用暂未发现问题，只能说明当前使用范围，不替代未测试场景的验收。
- 纯几何、自测和 CI 只能证明对应逻辑/构建通过，不代表 Explorer 或真实 Windows 桌面矩阵已验收。
- 真实 Explorer/taskbar takeover、Explorer 重启和异常恢复测试需用户明确授权；测试前备好回滚方式，结束后验证任务栏状态。

## 交叉引用

代码风格、工作区操作与项目规则见 `CLAUDE.md`；当前实现和未验收范围见 `docs/STATE.md`。
