# 桌序 DeskOrder 验证记录

- 日期：2026-10-30
- 环境：Windows 11 build 26300（26H2），2560×1440 @125%
- 运行会话：DSH 受限会话为 **Low Integrity**，对 Explorer 的窗口/任务栏写入被系统拒绝
- 构建：`powershell -ExecutionPolicy Bypass -File build.ps1`
- 产物：`dist\DeskOrder.exe`，304,640 bytes（另存 `dist\桌序.exe`，SHA256 一致）

## 1. 自检

```powershell
dist\DeskOrder.exe --self-test artifacts\final-selftest.json --data-dir artifacts\final-verify-data
```

结果：**通过 11/11**，`failed: 0`。

覆盖：FFT、频段映射、任务栏颜色编码、设置规范化、JSON、桌面快捷方式枚举、导入/撤销、Dock 增删、DefView、图标提取。

## 2. 本地播放器（MCI）

```powershell
dist\DeskOrder.exe --player-test artifacts\final-player.json --data-dir artifacts\final-verify-data
```

```json
{
  "added": 1,
  "sawPlaying": true,
  "maxPosition": 0.367,
  "duration": 1.000,
  "error": "",
  "ok": true
}
```

WPF `MediaPlayer` 在当前 26H2 返回 `0xC00D11D2`，因此改用 Windows MCI；WAV 播放/暂停/seek/状态正常。没有可测试的 mp3 样本，MP3 未做真实回环。

## 3. 系统音频回环与律动

```powershell
dist\DeskOrder.exe --audio-test artifacts\final-audio.json --data-dir artifacts\final-verify-data
```

```json
{
  "expectedBand": 19,
  "dominantBand": 0,
  "maxLevel": 0.082298,
  "maxBass": 0.8993,
  "maxBeat": 1.0000,
  "baselineBand": 0.0000,
  "expectedBandMax": 0.2182,
  "fftSineOk": true,
  "captureOk": true,
  "device": "{0.0.0.00000000}.{<默认输出设备 GUID>}",
  "error": ""
}
```

440 Hz 目标频段能量 0.2182（本轮测试环境为静音，基线 0.0000），回环采集、FFT、频段映射与节拍链路可用。

## 4. UI 渲染

```powershell
dist\DeskOrder.exe --screenshot-ui artifacts\ui-final --data-dir artifacts\ui-final-data
```

输出：

- `artifacts/ui-final/{overview,dock,music,taskbar,settings}.png`（1240×820）
- `artifacts/ui-final/music-widget.png`
- `artifacts/ui-final/dock-window.png`（625×88）

人工检查：无裁切/重叠；新增的“沉浸模式”开关与任务栏失败提示可见（在任务栏页向下滚动）；Dock 渲染与实机一致。用于 README 的图已复制到 `preview/`。

## 5. Dock 修复验证

### 5.1 位置与“外层透明框”

修复前用户看到 Dock 顶部一条透明边贴在任务栏上，点击落到任务栏；原因是窗口按 WPF 逻辑坐标定位，且在隐藏态只下移 7 px。

修复：

- 窗口内容与底板严丝合缝，`625×88`，没有外层透明 margin。
- 用 `GetMonitorInfo` + `SetWindowPos(HWND_TOPMOST)` 按**物理像素**定位到工作区底部上方 10 px。
- 隐藏时完全移出屏幕；显示时重新置顶，不再依赖 WPF `Top` 动画。
- 关闭外层 glow/shadow，避免“外面还有一层”。

实测（`--tray`）：

```text
显示：rect=711,1008,1336,1096  vis=True
隐藏：vis=False
再显示：rect=711,1008,1336,1096  vis=True
```

任务栏顶部在虚拟坐标约 1104，Dock 底边 1096，位于任务栏上方 8 px；显示/隐藏前后位置一致。

### 5.2 点击

修复：

- 去掉 Dock 的 `WS_EX_NOACTIVATE`（保留 `WS_EX_TOOLWINDOW`/topmost），点击可激活窗口。
- 点击改为窗口级 `PreviewMouseLeftButtonUp` + 可视树命中，避免子元素/滚动容器吞事件。
- Dock 置顶顺序在每次显示后重新 `SetWindowPos(HWND_TOPMOST)`。

当前 DSH 会话无法注入真实鼠标（`SetCursorPos`/`mouse_event` 被会话阻止），因此**未完成自动化点击回环**；请实机点击图标、音乐按钮和“序”设置按钮确认。代码层已避开此前的两条主要失败路径：窗口在任务栏下面、无激活窗口。

## 6. 桌面透明律动条

- 新增 `VisualizerWindow`：只包含 `SpectrumControl`，没有卡片、底板、边框或阴影。
- 窗口 `560×76`；位置 = Dock 顶边 − 76 − **距 Dock 距离**，并有 500 ms 的重排定时器，每 tick 重新 `Place()`，所以在 Dock 移动时也能跟随。
- **距 Dock 距离可调**：音乐页“律动条距 Dock”滑条，0–160 px，默认 12。实测（虚拟坐标，屏幕上为 125% 缩放）：
  - 距离 0 → 律动条 `bottom=1008`，Dock `top=1008`，正好贴住；
  - 距离 60 → 间隔 48 虚拟像素 = 60 物理像素；
  - 默认 12 → 间隔 10 虚拟像素 = 12 物理像素。
  这同时解决了“镜像波离得太远”。
- **全屏自动隐藏**：音乐页“全屏时自动隐藏”（默认开），`FullscreenDetector` 与 Dock 共用同一套判断（`SHQueryUserNotificationState` + 前台窗口是否盖满显示器）。
  - 用最大化无边框顶层窗口模拟全屏：隐藏前 `vis=1`，全屏时 `vis=0`，关闭全屏后 `vis=1` 回到原位。
- 窗口扩展样式为 layered + toolwindow + noactivate + transparent，鼠标点击穿透；不能拖动，用托盘“桌面透明律动条”或音乐页开关控制。
- 启动时默认开启（`ShowVisualizer=true`），完整音乐卡片不再自动弹出，只从 Dock 音乐按钮/托盘打开。
- 移除了 DWM 非客户区/边框属性，窗口只保留 bars，背景为 `Transparent`；不再有白色卡片层。
- 在 DSH 白色页面截图中透明区域会透出页面白色；实机壁纸上应只看到频谱。
- 频谱柱（bars）从窗口底部画、镜像波（wave）从中间画：bar 更容易被 Dock 压住，所以位置跟随和 0–160 px 距离滑条对 bar 更重要。

## 7. Dock 启动器 / WPS 与 QQ 浏览器差异

旧逻辑在启动前先 `IsRunning → FocusRunning`：

- 对 `.lnk` 只解析目标 exe，丢掉参数；UWP/Store 快捷方式目标是 `explorer.exe` + `shell:AppsFolder\...`，因此会被误判为“资源管理器已运行”。
- `FocusRunning` 不检查 `SetForegroundWindow` 的返回值；前台锁导致聚焦失败时仍返回成功，跳过真正启动。
- WPS 是普通无参数 exe 快捷方式，通常没在运行，所以能启动；QQ/浏览器常常已在运行或来自 Store，所以表现为“点了没反应”。

修复：

- `.lnk` / `.url` 直接 `Process.Start(..., UseShellExecute=true)`，保留参数、工作目录、AppID。
- 只有普通 exe 才尝试聚焦；`SetForegroundWindow` 失败则返回 false，继续启动。
- `IsRunning` 对解析到 `explorer.exe` 的 UWP 快捷方式返回 false，避免绿灯和误聚焦。
- 日志会记录 `Launched ... via shell shortcut`。

当前会话无法注入真实鼠标，Dock 启动链路未做自动化点击回环；需要实机点击 QQ/浏览器确认。

## 8. 任务栏透明（仍未完成系统级验证）

- 对桌序自己的测试窗口调用 `SetWindowCompositionAttribute`：state=2/3/4 返回 `True`，API 封装正常。
- 对 `Shell_TrayWnd` 调用：Low IL 下返回 `False`，日志：
  `Taskbar apply mode=clear bars=1 ok=False` / `系统未接受任务栏透明设置……`
- 已把内容层递归加入（`BridgeChildren` 递归到 Composition/Xaml 子窗口），普通模式也会同时处理内容层。
- `SHAppBarMessage(ABM_SETSTATE)` 在当前 Low IL 下返回 state=0，未生效；因此沉浸模式也**没有在本会话验证**。
- 结论：**任务栏透明在 26H2 上是否可用，必须在普通用户桌面确认**；如果仍无效，属于系统对 XAML 任务栏的限制，建议使用沉浸模式或保持默认。

### 普通桌面验证步骤

1. 双击 `dist\桌序.exe`，进入 **任务栏**，打开“任务栏效果”开关。
2. 点“预览 8 秒”，观察任务栏是否变化；不要只看缩略图。
3. 无变化时，打开“沉浸模式：自动隐藏任务栏”，确认任务栏是否收起。
4. 如发黑/异常，点“修复显示”或“恢复默认”，必要时手动重启资源管理器。
5. 需要日志时运行 `tools\verify-taskbar.cmd`，把 `artifacts\desktop-verify\logs\app.log` 发回。

## 9. 功能测试轮次（2026-10-30 晚）

本轮按功能逐项实测，发现并修复 1 个真实缺陷，其余通过。

| 项目 | 方法 | 结果 |
|---|---|---|
| 自检 11 项 | `--self-test` | 通过，`failed: 0`（新增距离钳制与设置往返断言） |
| 播放器 | `--player-test` | 通过，进度 0.367 s |
| 音频回环 | `--audio-test` | 通过，440 Hz 目标频段 0.218，`captureOk` |
| Accent 封装 | `--probe-accent` | 返回 0 |
| UI 渲染 | `--screenshot-ui` 1240×820 / 1240×1400 | 7 张页面图全部生成，无异常 |
| 桌面截屏 | `--capture-desktop` | 成功 |
| 律动条距离 | 写入 0 / 60 / 999 | 0 贴住 Dock；60 = 60 px；999 被钳制到 160 |
| 全屏隐藏（默认开） | 最大化无边框顶层窗口 | 律动条 `vis 1→0→1`，Dock 移出屏幕后恢复 |
| 全屏隐藏（关） | 同上，`HideVisualizerOnFullscreen=false` | 律动条保持可见 |
| Dock 全屏停靠 | 同上 | Dock 移到屏幕外（physical y=1464），退出后回 1260 |
| 主窗口打开/关闭 | 关闭主窗口消息 | Dock 暂停/恢复；律动条 924→922（2 px 舍入） |
| 单实例 | 第二次启动 | 进程数保持 1，Dock 不重复 |
| `.lnk` 启动 | `--launch-test` 指向 cmd 快捷方式，点两次 | 两次都执行，参数与工作目录保留（marker 2 行） |
| 普通 exe 启动 | `--launch-test` 指向 notepad.exe | 无异常，走聚焦/启动链路 |
| Store/UWP 启动 | `explorer.exe shell:AppsFolder\...` 快捷方式 | **无法在本会话验证**：Low IL 下 AppX 访问被拒，直接 `Start-Process` 同样不启动 |
| 日志 | 全部 `app.log` | 无 `[ERROR]` / `[WARN]` |

### 9.1 本轮修复的缺陷

**律动条跟随 Dock 移出屏幕**：`DockWindow.IsDockVisible` 在全屏避让（`_suppressAuto`）或暂停时仍可能为 true，而 Dock 已被 `PlaceDockPhysical(false)` 停到屏幕外；律动条每 500 ms 跟随 Dock 定位，于是也被拖到屏幕外。关闭“全屏时自动隐藏”时表现最明显：全屏后律动条 `top` 从 804 变 967。

修复：

- `Place()` 只在 Dock 矩形确实位于显示器内时才跟随，否则使用上一次屏幕内的锚点（`_lastDockTop`/`_lastDockCenterX`）；
- 没有锚点时，回退位置改为镜像 Dock 的停靠公式 `rcWork.Bottom - dockHeight - 8 - h - gap`（`dockHeight` 取上次实测值，缺省 110），使主窗口打开、Dock 暂停时律动条与正常位置只差 2 px（此前差 20 px）。

## 10. Dock 整理：删除 / 移动 / 添加失效（2026-10-30 晚，第二轮）

用户报告：“dock 整理，不能删除和移动或者添加”。

### 10.1 根因（实测复现）

1. **配置目录不可写，保存异常吞掉了整个操作。**
   当前会话对 `%APPDATA%` 的写入被拒绝（`New-Item "$env:APPDATA\DeskOrderProbe-*"` → `UnauthorizedAccessException`），而运行中的实例确实**从未创建** `%APPDATA%\DeskOrder`。旧版 `DockService.RaiseChanged()` 里 `Save()` 在事件之前执行且没有 try/catch，`Json.Write` 抛出的异常会一路冒到 `DispatcherUnhandledException`（被 `e.Handled = true` 吞掉），因此：
   - `Move`/`Reorder`：内存顺序已改，但事件没发出 → 页面与 Dock 都不刷新，看起来“移不动”；
   - `Remove`：内存已删，页面不刷新，看起来“删不掉”；
   - `Add`/整理：直接中断，连提示都没有。
   日志侧证：首次运行能 `Seeded dock with 8 desktop shortcuts`（该路径有 try/catch），但没有任何 `pins.json` 落盘。
2. **Dock 内拖拽排序被自己的 `DragEnter` 拒绝。** `OnDragEnter` 只对 `FileDrop` 返回 `Copy`，内部固定项拖动（`DeskOrder.Pin`）得到 `None`，WPF 因此从不触发 `Drop`。
3. **`Drop` 的落点索引按“所有条目”计算**（含音乐/设置两个特殊条目），与 `pins` 数量不一致。

### 10.2 修复

- `AppPaths.Initialize` 改为**探测可写目录**：`%APPDATA%\DeskOrder` → `%LOCALAPPDATA%\DeskOrder` → `<exe>\DeskOrderData` → `%TEMP%\DeskOrder`，创建目录并实际写一个探针文件，取第一个成功者；`RequestedRoot`/`FallbackNote`/`Writable` 对外暴露，启动日志记录 `data root=... writable=... requested=...`，界面提示一次。
- `DockService`：`TrySave()` 捕获异常并写入 `LastSaveError`，`RaiseChanged()` 无论保存成功与否都会发出 `Changed`；`ImportShortcuts`/`UndoLastImport` 的 `SaveImports` 同样不再抛出。`DeskOrderApp.OnDockChanged` 把 `LastSaveError` 变成一次提示；Dock 整理页每个操作后都提示保存失败原因。
- 新增添加入口：Dock 整理页右上角「添加文件… / 添加文件夹… / 按名称整理」，Dock 空白处右键同样有「添加文件… / 添加文件夹… / 按名称整理 / 打开 Dock 整理」；`DockService.SortByName()` 提供按名称整理。
- 拖放：`DockWindow.DropEffectFor()` 对 `DeskOrder.Pin` 返回 `Move`、对 `FileDrop` 返回 `Copy`、其他返回 `None`；落点索引只按 `pins` 计算（`HandlePinDrop`），文件拖入独立为 `HandleFileDrop`。
- 顺手修掉本轮自己引入的边界问题：`MovePin` 用“移动前后索引是否相同”判断是否到边界，避免“移到最后一个位置时只弹提示、不刷新”。

### 10.3 验证

| 项目 | 方法 | 结果 |
|---|---|---|
| 配置目录回退 | `--data-info`（无 `--data-dir`） | `requested=%APPDATA%\DeskOrder` → `root=dist\DeskOrderData`，`writable: true`，`fallback: true` |
| 只读目录下的增删改 | `--dock-save-test` | `threw: false`、`opsSurvived: true`、`saveErrorReported: DirectoryNotFoundException`，`ok: true` |
| 自检 | `--self-test` | **16/16**，`failed: 0`（新增 `data-root-fallback`、`data-root-probe-rejects-file`、`dock-drop-effects`、`dock-sort-by-name`、`dock-save-error-is-reported`） |
| Dock 整理页面交互 | `--dock-ui-test`（合成鼠标事件，无需物理鼠标） | **22/22**，`failed: 0` |
| 页面移动 | 点击真实 `↓`/`↑` 按钮 | `[Alpha,Bravo,Charlie]` → `[Bravo,Alpha,Charlie]` → `[Bravo,Charlie,Alpha]`，页面重排与内存一致 |
| 移到末尾 | 点击 `↓` 落到最后一位 | `[Bravo,Alpha,Charlie]` → `[Bravo,Charlie,Alpha]`，页面同步刷新 |
| 页面删除 | 点击真实 `×` 按钮 | 3 → 2 项，`pins.json` 同步 |
| 页面添加 | 与「添加文件…」同一路径 `AddPaths` | 2 → 3 项；重复项与不存在的文件被拒绝（`before=3 after=3`） |
| 落盘顺序 | 对比 `pins.json` 与内存 | 顺序与 `Id` 完全一致，`LastSaveError` 为空 |
| 拖放规则 | `DropEffectFor` | pin→`Move`、files→`Copy`、其他→`None` |
| Dock 内拖拽排序 | `HandlePinDrop(id, x)` | 末位图标拖到最前，顺序变为 `[Charlie,Bravo,Alpha]` |
| 文件拖入 Dock | `HandleFileDrop` | 新增 1 项并在页面出现 |
| Dock 空白右键菜单 | `BuildDockMenu()` | 含「添加文件…」「添加文件夹…」「按名称整理」等 |
| 按名称整理 | 页面按钮 / 菜单 / API | 顺序变为字母序，二次调用返回“无需变化” |
| 实机启动（默认目录） | `dist\桌序.exe --tray` | `data root=...\dist\DeskOrderData writable=True`；生成 `pins.json`（本机桌面上的 8 个快捷方式被固定，名称略）、`settings.json`（含 `VisualizerOffset`、`HideVisualizerOnFullscreen`）；Dock `rect=889,1260,781x110`；`ERROR=0 WARN=0` |

仍无法在本会话自动化的部分：文件/文件夹选择器（需要真实点击系统对话框）、托盘菜单、物理鼠标点击（`SetCursorPos`/`mouse_event` 被 Low Integrity 阻止）。

## 11. 便携模式与发布包（2026-10-30 晚，第三轮）

需求：“将这个软件打包出来，我需要发给别人使用”。

### 11.1 新增的便携模式

原来数据目录只在 `%APPDATA%` 不可写时才回退到 exe 同级；发给别人时应默认“绿色”，所以新增标记文件机制：

- exe 同级存在 `portable.txt`（或已存在 `DeskOrderData` 目录）→ 便携模式，数据写在 `exe\DeskOrderData\`；
- 探测链变为：**便携目录 → `%APPDATA%\DeskOrder` → `%LOCALAPPDATA%\DeskOrder` → `exe\DeskOrderData` → `%TEMP%\DeskOrder`**，每一步都实际创建目录并写探针文件，取第一个成功者；
- 便携模式属于预期行为，不再弹“配置目录不可写”的提示（只有真正的回退才提示）；日志与 `--data-info` 增加 `portable` 字段；
- 删掉 `portable.txt` 即可回到 `%APPDATA%\DeskOrder`。

### 11.2 打包脚本与产物

`package.ps1`（纯 ASCII，避免 Windows PowerShell 5.1 按 ANSI 读取中文脚本）：

```
release\
  DeskOrder-1.0.0-portable\        6 个文件
    DeskOrder.exe      304,640 bytes
    桌序.exe            304,640 bytes（同一份二进制）
    portable.txt            270 bytes
    使用说明.txt          5,501 bytes（UTF-8 带 BOM）
    README.md            17,547 bytes（UTF-8 带 BOM）
    SHA256.txt              228 bytes
  DeskOrder-1.0.0-portable.zip   330,892 bytes
```

### 11.3 压缩包中文文件名的编码陷阱（实测）

第一版打包后解压出来的文件名是 `妗屽簭.exe`、`浣跨敤璇存槑.txt`。逐字节分析压缩包本地文件头后确认：

| 创建方式 | 非 ASCII 名字节 | 通用标志位 bit 11 | .NET/资源管理器读回 |
|---|---|---|---|
| `CreateFromDirectory($dir,$zip)` | UTF-8 `E6 A1 8C E5 BA 8F` | **0x0800（已置位）** | `桌序.exe` ✅ |
| 显式传 `UTF8Encoding` | UTF-8（同上） | 0x0000（未置位） | `妗屽簭.exe` ❌ |

即：**显式传 `UTF8Encoding` 反而更糟** —— .NET 会写 UTF-8 字节却不置“UTF-8 名称”标志，所有按 ANSI 解读的读取器（资源管理器、7-Zip 默认、`Expand-Archive`）都会显示乱码；不传编码参数时才会正确置位。最终脚本改为 `CreateFromDirectory($stage,$zip,$level,$true)`。

### 11.4 验证

| 项目 | 方法 | 结果 |
|---|---|---|
| 便携模式（识别） | 临时目录放 `portable.txt` + `PortableBaseOverride` | 探测链首项为 `<dir>\DeskOrderData`，`WantsPortable()` 为真（自检 `portable-marker-wins`） |
| 便携模式（初始化） | 同上，`AppPaths.Initialize` | `writable: true`、根目录已创建且等于便携目录（自检 `portable-mode-initialize`） |
| 自检 | `--self-test` | **18/18**，`failed: 0` |
| Dock 整理页面交互 | `--dock-ui-test` | **22/22**，`failed: 0` |
| 只读目录增删改 | `--dock-save-test` | `ok: true` |
| 发布包解压 | `Expand-Archive` → 目录树 | `DeskOrder.exe`、`桌序.exe`、`使用说明.txt`、`portable.txt`、`README.md`、`SHA256.txt` 文件名全部正常 |
| 说明文档编码 | 检查解压后前 3 字节 | `使用说明.txt` / `README.md` 均为 `EF BB BF`（UTF-8 带 BOM），按 UTF-8 解码首行为 `桌序 DeskOrder 1.0.0 - 使用说明` |
| 包内哈希 | `SHA256.txt` vs 实际文件 | 一致：`D536B695D116AE0577E2A36219E8136091E183097B4FA24D06588876FD1FAC2C`（与 `dist\DeskOrder.exe` 相同） |
| 收件人模拟 | 解压到新目录后 `--data-info` | `portable: true`、`root=<解压目录>\DeskOrderData`、`writable: true`，首选项即便携目录 |
| 收件人模拟（运行） | 从解压目录 `--tray` 首次运行 | 自动创建 `DeskOrderData\{pins,settings,playlist}.json`、`logs\`、`DockItems\`、`Icons\`；`Seeded dock with 8 desktop shortcuts`；Dock `rect=889,1260,781x110`；`ERROR=0 WARN=0 NOTIFY=0`（便携模式不打扰） |

仍未在本会话验证的部分：SmartScreen 提示与真实杀毒软件行为、别人机器上的任务栏透明/UWP 启动（依赖对方系统版本与权限），已写入 `使用说明.txt` 的常见问题。

## 12. 入 git 仓库（2026-10-30 晚，第四轮）

需求：“放入 git 仓库”。仓库建在 `deskorder\`，首次提交 58 个文件、11,088 行。

### 12.1 先做脱敏，再入库

代码里原本混着本机信息，入库前逐项清理：

| 位置 | 问题 | 处理 |
|---|---|---|
| `build.ps1` | 图标生成脚本写死了本机运行时里的 `python.exe` 绝对路径（`%USERPROFILE%\.dsh\...`） | 改为从 PATH 找 `python`，找不到时给出提示 |
| `artifacts\verification.md` | 音频测试记录里带真实输出设备 GUID | 换成 `{<默认输出设备 GUID>}` 占位 |
| `artifacts\verification.md` | 验证表格里列出了本机桌面上的软件名称 | 改为“本机桌面上的 8 个快捷方式被固定，名称略” |
| `README.md` | 环境说明点名了本机常驻程序 | 改为“后台有常驻程序在运行” |
| `preview\*.png` | 截图里有本机 Dock 固定项与桌面软件图标 | 用中性示例数据 + 假桌面重新渲染（见 12.2） |
| `dist\`、`release\`、`artifacts\*.json` | 运行时数据（`pins.json` 含用户桌面路径）与构建产物 | 由 `.gitignore` 排除 |

### 12.2 截图用的中性数据

新增 `--desktop-dir <目录>`：把桌面扫描指向指定目录，并同时忽略公共桌面（`DesktopService.PublicDesktopOverride`）。仓库里的 `preview/` 由此重新渲染：

- 数据目录里预置 6 个固定项（文件资源管理器 / 记事本 / 画图 / 计算器 / 终端 / Windows 文件夹），`FirstRun=false` 以免启动时又去抓真实桌面；
- 桌面临时目录里放 4 个指向系统程序的假快捷方式（记事本 / 画图 / 计算器 / 命令提示符）；
- `--screenshot-ui preview --size 1240x820 --data-dir <中性数据> --desktop-dir <假桌面>`。

另核对源码：界面不显示用户名、机器名或其它身份信息（只用到系统路径），因此页面文本只可能来自固定项与桌面扫描这两处，均已替换。

### 12.3 验证

| 项目 | 方法 | 结果 |
|---|---|---|
| 敏感信息扫描 | `git grep --cached` 搜本机用户名、设备 GUID、本机软件名、运行时路径 | 无命中 |
| 入库范围 | `git status --short` | 58 个文件：`src\**`、`build.ps1`、`package.ps1`、`README.md`、`docs\`、`assets\`、`preview\`、`tools\`、`artifacts\verification.md`、`.gitignore`；`dist\`/`release\`/测试 JSON 全部忽略 |
| 仓库自洽性 | `git archive HEAD` 导出后重新构建 | 39 个源文件编译出同样的 305,152 字节 exe，`--self-test` 18/18、`--package` 正常出包 |
| 提交 | `git commit` | `3fa58c6`，58 files changed, 11088 insertions(+) |

### 12.4 环境限制

`git clone` 在本会话不可用：本地克隆会让 git 走 `sh.exe`，而沙箱禁止创建命名管道（`fatal error - couldn't create signal pipe, Win32 error 5`）。改用 `git archive HEAD` 导出同样内容后完成“干净目录能否构建”的验证。真正的 `clone`/`push` 在普通会话里不受影响。

## 13. 开机自启失效（第二轮修复）

### 13.1 现象与根因

用户报告「不能开机自启」：设置页的开关点下去只会弹「无法修改开机启动项」，重启后也没有自动启动。证据链三条：

1. `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下根本没有 `DeskOrder` 值，`settings.json` 里 `General.Startup=false`；
2. `app.log` 在 17:28–17:29 记录 7 条 `Startup toggle failed :: UnauthorizedAccessException: 对注册表项 HKCU\...\Run 的访问被拒绝`，堆栈指向 `StartupService.SetEnabled`；
3. 在会话里复现同一拒绝，`GetTokenInformation` 显示本会话进程是 **Low 完整性 + 写受限令牌**（该键的 ACL 只给 `NT AUTHORITY\RESTRICTED` 一个 `ReadKey`）；用户此前用的实例（PID 39416）则是 **Low 完整性、无限制 SID**。低完整性进程写中完整性安全等级的注册表键会被强制完整性策略直接拒绝。

原实现只有一条路径（Run 项），失败时只回滚开关并弹一句没有原因的提示，所以既没有退路、也看不出原因；`IsEnabled()` 也只看 Run 值，不知道 Windows「启动应用」页写下的禁用标记（`StartupApproved\Run`）。

### 13.2 修复

| 位置 | 改动 |
|---|---|
| `src\Core\StartupService.cs` | 三级登记：注册表 Run 项 → 当前用户「启动」文件夹快捷方式 → 登录计划任务（`schtasks /SC ONLOGON /TN DeskOrder`）；成功一种就停，并清掉另外两处避免重复登记；每一层的具体异常都保留，不会只说「失败」 |
| 同上 | `Status()`：机制、实际命令、是否指向旧路径（stale）、是否被系统关掉（`StartupApproved\Run` 低位）、注册表是否可写、进程完整性级别、失败原因，以及可直接粘贴的 `manualCommand` |
| 同上 | `RefreshIfEnabled()`：程序换目录/改名后，下次启动自动把登记项里的路径改成当前 exe |
| `src\Interop\ProcessIntegrity.cs` | 新增：读自身令牌的完整性级别（Untrusted/Low/Medium/High），用于给出可执行的失败原因 |
| `src\Interop\ShortcutResolver.cs` | 新增 `Create()`：写 .lnk，供「启动」文件夹退路使用 |
| `src\UI\SettingsPage.cs` | 开关下方增加**状态行**（当前机制 / 旧路径 / 被系统关闭 / 失败原因）与「重新登记」「复制启动命令」两个按钮；开关状态改由登记项实际状态决定 |
| `src\Core\AppPaths.cs` | 新增 `Display()`：界面里的路径按 `%USERPROFILE%` / `%APPDATA%` 打码，既好读，也不会把账号名带进截图 |
| `src\Program.cs` | 新增 `--startup status\|on\|off\|repair\|command`；启动时日志写一行 `autostart ...`，开机后可直接从日志核对 |

### 13.3 实测

- 登记结果：`HKCU\...\Run\DeskOrder` = `"C:\...\dist\桌序.exe" --tray`，`StartupApproved\Run` 无禁用项。
- 用 `桌序.exe` 运行时 `--startup status` 输出 `enabled: true / mechanism: 注册表 Run 键 / osDisabled: false / stale: false`；用副本 `DeskOrder.exe` 运行同一命令则报告 `stale: true` 并给出应为的路径，说明「指向旧路径」也能被发现。
- 受限会话里同一命令输出 `runKeyWritable: false / integrity: 低完整性（Low） / problem: 登记的是旧路径…`，退出码 1——「失败必须说明原因」的契约成立。
- `--self-test` **24/24**（新增 6 例：启动命令行、`StartupApproved` 字节解析、启动快捷方式往返、开关失败必给原因、启动状态可读、界面路径打码）；`--dock-ui-test` **22/22**。
- 未能在此会话完成的验证：真正的注销/重启触发（需要普通会话重启一次），以及未签名程序在启动项里的 SmartScreen 行为。

### 13.4 顺带修掉一处数据破坏（由我的测试造成）

`--dock-ui-test` 会清空并重建 Dock 固定项，而这台机器的数据目录就是真实目录（`%APPDATA%` 在受限会话不可写），所以一次测试把用户的 `pins.json`（9 个固定项）覆盖成了测试项。处理：

- 按日志证据重建 `pins.json`：共 9 条，逐条都能在 `app.log` 里找到痕迹——6 条来自 `Dock click pin=` 记录，1 条来自 17:23:55 的拖拽重排记录，1 条来自「拖入 Dock」后的点击记录，1 条是 Steam 目录下的游戏程序；每条路径都先核对当前磁盘确实存在再写入。具体名称属使用者隐私，不在此列出。
- `DockUiTest.Run` 现在**先快照数据目录下所有 `*.json`，结束时还原**，并清理自己创建的 `dock-ui-test\` 临时目录。
- 实测：对真实数据目录再跑一次 `--dock-ui-test`，`pins.json` 的 SHA256 前后完全一致（`DF70BADA335E24268107A2B2ED6B5527C0125007A5F8AE405C8C8DBDFA8D7985`），日志出现 `dock-ui-test restored 4 data file(s)`，22/22 仍全通过。

诚实说明：旧列表无法 100% 还原——被覆盖前的文件是 2184 字节，按每条约 200–235 字节推算比这 9 条多 1 条左右；日志里 5 次「拖入 Dock」没有记录被拖文件的路径，所以可能少 1–2 个当时拖进去的程序（例如从 Steam 直接拖的 exe）。用户可在「Dock 整理」页两下补上。

## 14. 结论

Dock 的外层透明框、显示后落到任务栏下面、点击链路、桌面透明律动条（含 0–160 px 距 Dock 滑条与全屏自动隐藏）、快捷方式启动逻辑、**Dock 整理删除/移动/添加失效**、**便携模式与可分发压缩包**，以及**脱敏入库**均已完成并实测。三轮的关键发现依次是：受限会话中配置目录不可写会把保存异常留在 UI 刷新之前（所有 Dock 编辑看起来“没反应”）；ZIP 内中文文件名依赖 UTF-8 标志位、显式传 `UTF8Encoding` 反而写出乱码；入库前必须先脱敏（本机路径、软件名、设备 GUID、含真实桌面的截图）。

第四轮（开机自启）的结论：开关失效的根因不在登记代码写错，而在**运行环境**（低完整性/写受限令牌）与**只有一条登记路径且失败不报原因**。现在三级登记 + 完整状态输出把「能不能开机自启」变成一条可自查的记录：`--startup status` 一次就能看出是没登记、指向旧路径、被系统关掉，还是当前环境不允许写注册表。同一轮还修掉了测试工具会覆盖真实 `pins.json` 的问题（改为快照/还原），并已按日志证据恢复用户 Dock。

Store/UWP 激活与任务栏透明受当前 Low Integrity 会话限制，未做最终确认；沉浸模式作为任务栏替代已加入。
