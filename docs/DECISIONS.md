# 关键决策记录

> 只记录“以后可能被推翻”的决策。每条包含：日期 / 背景 / 决定 / 代价。
> 改代码前若与某条冲突，先更新本文件并告知用户。

---

## D1 · 自动隐藏改用 Windows 原生 appbar 状态

- **日期**：2026-10-30（commit `dbcdb1f`）
- **背景**：上游用自绘 alpha 渐变隐藏任务栏，并改写工作区（`SPI_SETWORKAREA`），导致窗口尺寸异常。
- **决定**：设置 `ABS_AUTOHIDE`，由 Explorer 管理隐藏与工作区；删除自绘淡入淡出与工作区改写。
- **代价**：触发依赖“鼠标命中任务栏窗口在屏幕边缘的可绘制像素”，因此受区域裁剪影响，需要探头条；这正是后续一系列问题的根源。
- **推翻条件**：若能接受自行管理工作区/窗口尺寸，可改回自管隐藏（方案 C）。

## D2 · 隐藏态保留探头条，且仅在 top margin > 0 时添加

- **日期**：2026-11-03（`fc0c0f8`）、2026-11-04（`271f2cc`）
- **背景**：top margin ≥ 3 时，探头条被区域裁掉，任务栏无法弹出；悬停模式下静止区域为空，更无法触发。
- **决定**：隐藏态把屏幕边缘的探头条并入区域；若某分段的 top margin = 0，其区域本身已到边缘，不加线。
- **代价**：正 margin / 圆角时线条与任务栏边缘不贴合（几何必然）；全部分段 margin = 0 的悬停模式依赖 100 ms 悬停轮询触发。
- **相关**：`docs/LESSONS.md` 中已否决的替代方案。

## D3 · 悬停模式保留上一次分段区域，但 Apply 强制重算

- **日期**：2026-11-03（`e046f15`）、2026-11-04（`8a20a24`）
- **背景**：鼠标离开时立刻清空区域会让任务栏一帧消失，丢失收回动画；但无条件保留会让已关闭的分段继续可触发。
- **决定**：后台循环保留最后一次分段区域；设置界面的 Apply 路径传 `forceRegion: true`，按新设置立即重算。
- **代价**：新增 `forceRegion` 参数，调用方需区分“后台循环”与“用户 Apply”。

## D4 · 读取 `StuckRects3` 对齐自动隐藏（开发版，未发布）

- **日期**：2026-11-04（`59c1ac7`）
- **背景**：启动时读取 appbar 状态可能读到 `AlwaysOnTop`（Explorer 尚未应用设置），恢复该状态会永久关闭自动隐藏。
- **决定**：Always show 时读 `HKCU\...\Explorer\StuckRects3` 的 `Settings[8] & 0x02`，仅使 appbar 状态与之一致；Always hide 时才在修改前捕获 live 状态并在退出时恢复。
- **代价**：注册表读取失败时退回原逻辑；若旧版已关闭系统设置，需用户手动重开一次。

## D5 · 自动隐藏对齐采用事件驱动（开发版，未发布）

- **日期**：2026-11-04（`2edc96f`，替代 `a31752a` 的 5 秒轮询）
- **背景**：长时间运行后 Explorer 会丢失/重置 appbar 状态，任务栏不再收回；但定时轮询读注册表 + `Dispatcher` 有固定开销。
- **决定**：监听 `WM_SETTINGCHANGE`、`WM_DISPLAYCHANGE`、`WM_POWERBROADCAST`、`WM_DPICHANGED` 与 `TaskbarCreated`，1 秒去抖后对齐；状态一致时不调用 `SetTaskbarState`。
- **代价**：若 Explorer 在无广播的情况下静默丢失状态，将无法自愈（当前无已知此类事件）。

## D6 · 发布流程：WPF stable 与 Light prerelease 隔离

- **日期**：2026-10 起沿用；2026-11-24 扩展
- **决定**：WPF `ci.yml` 对 `v1.0*` tag 构建并上传 stable ZIP；Light `light-v*` 只走单 EXE prerelease job；其他分支 push 构建验证 artifact。`license-attach-*` maintenance tag 只给既有 Release 追加许可证附件，不重建/替换二进制资产。
- **约定**：WPF 版本沿 `v1.0.6` → `v1.0.7`；实验性轻量宿主独立 `light-v0.1.0-preview.N`；稳定版与实验预发布不可混称。

## D7 · 轻量宿主用 Raw Input 事件替代可见探头条与光标轮询

- **日期**：2026-11-23
- **背景**：D2 依赖屏幕边缘的可见探头条，用户明确不想看到这条线；任务栏必须有可命中像素才能被原生自动隐藏拉出。
- **决定**：在轻量宿主中，悬停模式静止时把任务栏区域完全裁空，不保留探头条。宿主用 Raw Input（`RIDEV_INPUTSINK` 的 `WM_INPUT`）接收全局鼠标移动，指针进入屏幕边缘时立即应用对应分段区域，让 Windows 拉出任务栏；离开边缘时再收起。8 ms 节流只用于降低高频回调开销，触发本身是事件驱动，不轮询光标。上边距为 0 时不出现任何线，上边距 > 0 时只在悬停瞬间临时加 2 px。
- **代价**：触发依赖宿主进程和 Raw Input 注册；宿主未运行时任务栏不会因悬停自动弹出。Raw Input 只观察、不阻塞输入；8 ms 节流控制事件处理频率而非轮询。多显示器、其他停靠边和全屏应用尚未验证。未使用 alpha=1，避免 L5 的强杀恢复风险。
- **推翻条件**：若 Raw Input 在锁定屏幕、全屏游戏或某些远程会话中不可用，回退到探头条或低级鼠标钩子。

## D8 · 日常驻留切换为轻量宿主，WPF 设置按需启动

- **日期**：2026-11-23
- **背景**：WPF 版常驻 50–90 MiB；轻量宿主实测空闲 8.2 MiB、加载 UIA 约 9.7 MiB，1 小时不增长，并已覆盖动态/简单模式、悬停、无探头条、托盘菜单、Explorer 重建和设置界面拆分。
- **决定**：日常运行 `F:\Program Files\Claude\RoundedTB-light\RoundedTBHost.exe`；它读取 `%LOCALAPPDATA%\rtb.json` 并管理任务栏。WPF 设置程序以 `--settings-only` 由托盘菜单按需启动，Apply 写配置后宿主自动重载，关闭设置即退出。单实例用命名互斥，第二次启动通过命名事件打开设置。2026-11-23 已切换日常驻留，开机启动项当前已启用。
- **代价 / 状态**：D8 描述的是当时 `.NET Framework` 旧 host + 独立 WPF Settings EXE 的本地架构；2026-11-24 已被 D12 的 Light 单 EXE/双进程架构替代。当前开机启动项仍指向旧 `RoundedTB-light` 路径，迁移尚未授权。
- **回滚**：停止 `RoundedTBHost.exe`，运行 `F:\Program Files\Claude\downloads\candidate-69\RoundedTB.exe` 即可回到 WPF 版。

## D9 · Windows 11 运行时移植保留 WPF 填充与 composition 兼容行为

- **日期**：2026-11-23
- **背景**：功能完整迁移对照发现轻量宿主未实现 WPF 的最大化窗口 / Alt+Tab 任务切换填充行为；`CompositionCompat` 虽被读取但在宿主运行时未生效。
- **决定**：在轻量宿主中按 WPF Windows 11 语义实现：同一任务栏监视器上的可见、未 cloaked 最大化窗口触发临时整栏区域；任务切换器检测受 `FillOnTaskSwitch` 控制并依赖 `FillOnMaximise`。region 成功应用/清除后，仅在 `CompositionCompat` 开启时通知 TranslucentTB 刷新。历史配置缺少两个 fill 字段时采用 WPF Windows 11 默认值 true。
- **代价 / 状态**：最大化检测枚举当前 taskbar 所在 monitor 的顶层窗口；TTB worker 不存在时忽略。实现已在 `RoundedTB.Host/TaskbarTakeover.cs`，设置 parser 对应自检通过；真实最大化/Alt+Tab 与 TTB 联动尚未验收。
- **安全边界**：不更改工作区，不循环切换 appbar 状态；本决策不授权真实 Explorer 接管测试。

## D10 · 多任务栏按 HWND 管理，动态分段暂限水平停靠

- **日期**：2026-11-23
- **背景**：轻量宿主原先只处理单个 `Shell_TrayWnd`（主显示器），无法覆盖多显示器；`DynamicGeometry` 按水平轴布局分段。逐项移植对账把多显示器/非底边列为阶段 3 缺口。
- **决定**：
  - `TaskbarTakeover` 改为以 HWND 为键的多任务栏状态；`TrayProbe` 发现 `Shell_TrayWnd` 与全部 `Shell_SecondaryTrayWnd`，每个任务栏按自身监视器 rect、DPI 与停靠边应用区域；Raw Input 指针经纯 `TaskbarModel`（停靠边判定、边缘触发条、指针路由、句柄对账）路由到对应显示器/边。
  - 简单模式与停靠方向无关，四边通用；动态分段保持水平布局，适用于底部与顶部停靠。
  - 左右竖直停靠的任务栏在动态模式下**不应用**动态区域（`TickState` 显式跳过早返回），避免把水平布局套到竖直窗口；simple 模式仍覆盖。
- **代价 / 状态**：纯几何/句柄对账自检通过；单屏底边 hover 手动通过。真实多屏、负坐标、混合 DPI、其他边缘尚未实测；左右竖直停靠的动态分段尚未实现。当前 light 统一程序以单 EXE 预发布交付。
- **推翻条件**：若需要左/右竖直停靠的动态分段，需为 `DynamicGeometry` 增加竖直轴向布局并单独验收。

## D11 · 轻量宿主迁入 D4/D5 自动隐藏对齐（事件驱动、幂等）

- **日期**：2026-11-23
- **背景**：D8 记录轻量宿主未迁移 WPF 开发分支的 D4/D5 自动隐藏状态对齐；Explorer 长时运行/重启后可能丢失自动隐藏状态，导致圆角与 Raw Input 悬停失效。
- **决定**：
  - 纯 `AutoHidePolicy` 镜像 WPF `AutoHide(...)`：`AutoHide==0`（Always show）读 `StuckRects3` 的 `Settings[8] & 0x02` 并仅在 Explorer 状态不一致时对齐；`AutoHide>0`（Always hide）在首次改动前捕获原状态再强制 `ABS_AUTOHIDE`；registry 不可读且曾由我们对齐时回退到捕获的原状态。状态一致时**不调用** `ABM_SETSTATE`（幂等）。
  - `Debouncer` 把 `WM_SETTINGCHANGE/DISPLAYCHANGE/POWERBROADCAST/DPICHANGED` 与 `TaskbarCreated` 的突发合并为约 1 秒后一次对齐；另有启动与设置重载时的对齐请求。
  - 保持红线：不改工作区、不作看门狗轮询、不在动画中改状态。
- **代价 / 状态**：正常退出、注销和异常退出事件均调用幂等 `ShutdownRestore`：优先恢复到退出时可读的 Windows `StuckRects3` 偏好；registry 不可读时使用 Always hide 开启前捕获的状态；随后清理 Raw Input、设置 watcher 和任务栏 region。策略/去抖/退出恢复纯自检通过；light `.4` 在 CI self-test 及用户本机运行。现场确认状态一致时不会触发 `ABM_SETSTATE`（符合幂等设计），实际状态变更与多屏行为未测。

## D12 · 轻量宿主发布合并为一个 .NET 9 EXE、双进程模式

- **日期**：2026-11-24
- **背景**：用户要求减少发布包中的 EXE 数量，同时保留常驻 host 与按需设置 UI 的进程隔离。
- **决定**：Light 预发布统一为 `RoundedTB.Light.csproj`（`net9.0-windows`），同一程序集含 WPF settings 和 Win32 host；`LightEntryPoint` 无参数分流 host，`--settings-only` 初始化 WPF `App` 后打开设置。Host 通过自身 apphost 路径启动 settings child process，Apply 仍由 host watcher 热重载。发布采用 framework-dependent single-file `win-x64`；`.4` ZIP 仅含程序 EXE、`LICENSE` 与 `THIRD-PARTY-NOTICES.txt`。
- **运行时取舍**：目标机必须安装 .NET 9 Windows Desktop Runtime x64；.NET 9 已于 2026-11-10 结束支持。此 `.4` 保留 net9 并标 experimental prerelease；未来稳定版需重新选受支持 LTS 或 self-contained。
- **已知发布缺陷/修复尝试**：`.2` 漏调 `App.InitializeComponent()`，导致 Settings 面板未初始化；`.3` 起加入该调用，CI 入口/resources self-test 通过。但用户之后反馈 `.4` Settings 面板仍打不开，说明还存在未定位原因。`.4` 在代码上承接初始化调用，只精简包内容；不得称为已确认修复。下一次发布前要做真实 WPF Settings UI smoke test。
- **验证边界**：`.4` 的 CI publish/self-tests/单 EXE 检查通过；公开 ZIP 下载核对 hash、仅含 3 个文件。真实 Settings 面板、Apply/reload 尚待用户确认。
- **代价**：目标运行时由旧 host 的系统内置 .NET Framework 4.8 变为 .NET 9 Desktop Runtime；WPF resources 随统一程序集装入，默认 host 入口必须在创建 `Application` 前分流，避免常驻 host 承担 WPF dispatcher 初始化。

## D13 · 悬停位置改为每 tick 读取 `GetCursorPos`，Raw Input 只做触发信号

- **日期**：2026-11-24（commit `2e87c99`）
- **背景**：用户反馈 High integrity 的 Terminal 聚焦时，Light 几乎无法通过悬停拉出任务栏。旧实现收到 `WM_INPUT` 后缓存坐标，之后每个 tick 复用缓存；当 Raw Input 因完整性级别/前台窗口组合停止送达时，坐标永久过期。用户同时要求比“时间戳新鲜度 + 回退策略”更简单的方案。
- **决定**：`TaskbarTakeover.ApplyDynamic` 每次更新都调用 `GetCursorPos` 取当前位置；`WM_INPUT` 仍作为立即 `Tick()` 的信号，但不再是位置来源。不新增轮询线程；沿用已有 100 ms timer。`RawInput.SetPointer` 只保留边缘状态统计。
- **代价**：Raw Input 不可用时，悬停响应最慢约一个 100 ms 周期；空闲时每 tick 多一次 `GetCursorPos` 调用（可忽略）。未改变 D7 的“无探头条”设计。
- **验证**：2026-11-24 现场 A/B（elevated Windows Terminal 前台）：`.4` 6 次中 2 次失败；候选 10/10 成功；用户确认正常。
- **推翻条件**：若未来需要更低延迟且能保证 Raw Input 在所有完整性组合下送达，可重新引入缓存，但必须保留实时采样兜底。

## D14 · Light 发布包只放程序 EXE，许可证作为 Release 独立资产

- **日期**：2026-11-24
- **背景**：用户要求程序 ZIP 内不要放证书/许可证文件，改为在 GitHub Release 中分开发布。
- **决定**：Light 包只含 `RoundedTBHost.exe`；`LICENSE` 与 `THIRD-PARTY-NOTICES.txt` 作为同一 Release 的独立资产上传。CI 在打包阶段校验包内只有程序 EXE 一个文件。`.4` 及更早资产不改写；`.5` 起适用。
- **代价**：下载 ZIP 的用户需在同一 Release 页面另行获取许可证文件；发行说明与 README 已注明。

## D15 · Light 0.1.0 作为正式 Release 直发 EXE，不再提供 ZIP

- **日期**：2026-11-24
- **背景**：用户要求发布正式版时不提供压缩包，直接把 EXE 作为 Release 资产；并明确选择去掉 Pre-release 标记、使用 `light-v0.1.0`。
- **决定**：Light Release 资产为 `RoundedTBHost.exe`、`RoundedTBHost.exe.sha256`、`LICENSE`、`THIRD-PARTY-NOTICES.txt`，无 ZIP。`gh release create` 不加 `--prerelease`，并加 `--latest=false`，避免抢占 WPF `v1.0.6` 的 Latest 标记。`.1`–`.6` 的 ZIP 资产不改写。WPF `v1.0.x` 稳定线继续用 ZIP。
- **代价 / 风险**：去掉 Pre-release 标记会让 Release 看起来是稳定版；但 Light 仍使用 EOL .NET 9，且多屏/非底边/TTB/Explorer 重启等未验收，发布说明必须明确“不是 WPF 稳定版替代”。用户已知悉并选择该做法。
- **推翻条件**：若后续决定回到预发布或恢复 ZIP，可改 workflow 并发布新 tag，不重写历史资产。

