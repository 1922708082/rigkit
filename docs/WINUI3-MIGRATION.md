# RigKit WinUI 3 迁移设计

状态：P0 / P1 壳已验证，P2 待做  
日期：2026-10-19  
决策：WinUI 3 + Windows App SDK 2.5.1 + C# / .NET 10

> 已落地：`src/RigKit.Desktop`（unpackaged，self-contained WAM）。Mica、原生标题栏、1 px 窗口边框、NavigationView 侧栏收缩、深浅主题、设置持久化、90HX 真实传感器读取均已实测通过。WPF 版仍保留到功能对齐。

## 1. 为什么是 WinUI 3

目标不是“换个皮肤”，而是把 WPF 壳换掉，得到 Win11 原生的窗口、控件、主题和交互。

| 方案 | 语言 | 优点 | 代价 | 结论 |
|---|---|---|---|---|
| **WinUI 3 + Windows App SDK** | C# / XAML | 原生 Fluent、Mica、NavigationView、系统深浅色、圆角与 DWM 边框 | 工具链与打包更重，图表等第三方生态少 | **采用** |
| WPF + Fluent 主题 | C# / XAML | 现有代码可直接复用，交付最快 | 不是真正的 Win11 原生，控件行为仍是 WPF | 备选，不作为主路线 |
| Avalonia | C# / XAML | 跨平台，UI 自由度高 | 原生感弱，Windows 集成要自己补 | 如果未来要 Linux 再考虑 |
| C++ / Qt | C++ / QML | 性能和控制力最强 | 内核要重写，单人成本过高 | 不采用 |

## 2. 迁移边界

**保留：**

- `RigKit.Domain`：设备、传感器、能力矩阵、体检模型
- `RigKit.Application`：设备服务、体检 Runner、配置抽象
- `RigKit.Infrastructure`：NVML、nvidia-smi、CUDA 显存测试、SQLite、Serilog
- `RigKit.Contracts`、`RigKit.Broker`：命名管道、审计、调用方校验
- `tests/RigKit.UnitTests`：现有 43 个测试

**重写：**

- `RigKit.App`（WPF）→ `RigKit.Desktop`（WinUI 3）
- 所有 View、ViewModel、导航、主题、窗口壳
- 图表控件：WinUI 没有内置图表，自己写轻量 `TelemetryChart`
- 单实例：WPF 的命名信号量 → `AppInstance` / `AppActivationArguments`

**不做：**

- 不重写硬件内核，不把 NVML / CUDA 调用搬进 View
- 不先做全部页面；第一版只做工作台 + 设置，其余页面占位
- 不引入 Electron / Qt / WebView 壳

## 3. 目标架构

```text
RigKit.Desktop (WinUI 3)
  Views/            WorkbenchPage, SettingsPage, 占位页
  ViewModels/       CommunityToolkit.Mvvm
  Controls/         TelemetryChart, StatusPill, MetricCard
  Services/         NavigationService, ThemeService, DialogService
        ↓
RigKit.Application  (用例、体检 Runner、配置抽象)
        ↓
RigKit.Domain       (设备、传感器、体检模型)
        ↑
RigKit.Infrastructure (NVML / CUDA / WMI / SQLite / Serilog)
        ↑
RigKit.Broker       (命名管道、审计、提权边界)
```

依赖方向保持单向：UI 只依赖 Application；Infrastructure 实现 Application 的接口；Broker 不依赖 UI。

## 4. WinUI 3 关键技术点

### 4.1 工程

- TFM：`net10.0-windows10.0.19041.0`
- 包：`Microsoft.WindowsAppSDK` 2.5.1、`Microsoft.Windows.SDK.BuildTools`
- 先做 **unpackaged**：`WindowsPackageType=None`，`WindowsAppSDKSelfContained=true`，开发期直接 `dotnet run`
- 发布期再评估 MSIX；不把打包问题带进第一版
- `App.xaml` 会生成入口点，**不要再手写 `Program.cs`**（探针已踩过）

### 4.2 窗口

- `ExtendsContentIntoTitleBar = true`，`SetTitleBar(AppTitleBar)`
- 根节点加 1 px `Border`，颜色取 `WindowBorderBrush`，解决“窗口和其他软件分层”的问题
- Win11 使用 `MicaBackdrop`；Win10 回退 `DesktopAcrylicBackdrop`，再回退纯色
- 窗口按钮使用系统 caption buttons，保留最小化/最大化/关闭的系统行为
- 圆角由 DWM 处理，不强行 `AllowsTransparency`

### 4.3 主题

- 浅色 / 深色通过 `FrameworkElement.RequestedTheme = ElementTheme.Light / Dark`
- 设置持久化到 `%LOCALAPPDATA%\RigKit\app.json`
- Mica 跟随窗口主题需要实测；若 Mica 不跟随 ElementTheme，就回退为 Acrylic 或纯色背景
- 颜色全部走 `ThemeResource` / `ResourceDictionary`，不在 View 里写死十六进制

### 4.4 导航与侧栏

- `NavigationView` + `Frame`
- `PaneDisplayMode=Left`，`IsPaneOpen` 控制展开/收起
- 收起后保留图标，展开宽度 224，收起宽度 48
- 状态持久化；首次启动展开
- 设置页作为 `NavigationView` 的一个 MenuItem，不依赖系统设置项

### 4.5 图表

- 不引入第三方图表库
- 自写 `TelemetryChart`：`Canvas` + `Polyline` / `Path`
- 数据源为 `IReadOnlyList<TelemetryPoint>`，不把 WPF 的 `PointCollection` 带进来
- 60 点、1 秒刷新；后续可加 300 点窗口和悬停读数

### 4.6 生命周期

- 单实例：`AppInstance.FindOrRegisterForKey` + `RedirectActivationTo`
- 日志：沿用 Serilog
- 崩溃：`App.UnhandledException` + `TaskScheduler.UnobservedTaskException`
- 配置：沿用 `JsonConfigStore`，增加 `Theme`、`SidebarCollapsed`

## 5. 视觉方向

以第二版浅色为基准，但使用 WinUI 原生材质：

- 浅色：Win11 云母底 + 白色卡片 + 铜色强调
- 深色：石墨底 + 铜色遥测 + 低对比边框
- 标题栏、NavigationView、设置项都用系统控件，不再手绘 WPF 模板
- 首屏优先保证：设备名、健康分、4 个遥测值、体检结果可见
- 窗口边框始终保留 1 px 深色描边

## 6. 风险与决策门

| 风险 | 影响 | 应对 |
|---|---|---|
| WinUI 工具链 | 编译/运行失败 | 探针已通过：NuGet 还原 + `dotnet build` + 窗口启动正常；入口点由 XAML 生成 |
| Mica 不跟随 ElementTheme | 深色模式观感不一致 | 实测；不行就 Acrylic / 纯色回退 |
| WinUI 图表生态弱 | 曲线实现成本 | 自写轻量控件，先 60 点 |
| 窗口边框 | 与 DWM 圆角/阴影冲突 | 根 Border 1 px + 系统 caption，不强制无边框透明 |
| 打包 | 安装/更新复杂 | 第一版 unpackaged，MSIX 留到发布阶段 |
| UI 重写回归 | 工作台功能丢失 | 核心 43 个测试不动；按页面逐步迁移，每步可运行 |

## 7. 验收标准

1. `RigKit.Desktop` 能 `dotnet build`、`dotnet run` 启动
2. Win11 上窗口有 Mica、圆角、1 px 深色边框、原生 caption buttons
3. 侧栏可展开/收起，重启后保持
4. 设置页可切换浅色/深色，立即生效并持久化
5. 工作台显示 90HX 实时温度/功耗/显存/频率和 7 项体检结果
6. 现有 43 个单元测试仍通过
7. WPF 项目从解决方案移除后，`RigKit.slnx` 仍能构建

## 8. 迁移阶段

| 阶段 | 内容 | 产出 | 预估 |
|---|---|---|---|
| P0 | WinUI 工程、包、入口、Mica/NavigationView 探针 | 可启动的空壳 | 0.5 天 |
| P1 | 窗口壳、标题栏、边框、主题、侧栏、设置页 | 可切换主题的壳 | 1–2 天 |
| P2 | 工作台：设备卡、遥测、图表、体检列表 | 真实数据工作台 | 2–3 天 |
| P3 | 设备/改造/调优/维护占位页、日志、单实例、崩溃 | 功能对齐 | 1–2 天 |
| P4 | 截图、CI、文档、移除 WPF、归档分支 | 可交付版本 | 1 天 |

## 9. 待确认

1. 第一版导航是否只做 **工作台 + 设置**，其余页面占位？
2. 发布形态先 **unpackaged**，MSIX 留到后面？
3. 视觉是否以 **浅色 + Mica** 为默认，深色作为设置项？
4. WPF 分支 `wip/wpf-theme` 只作参考，不合并？

确认后从 P0 开始，不直接动业务代码。
