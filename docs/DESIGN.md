# 矿卡工具箱 RigKit · 产品设计方案

| 项 | 内容 |
|---|---|
| 文档版本 | v1.2 |
| 日期 | 2026-10-19 |
| 状态 | 设计已确认，M0 开发中 |
| 参考项目 | `D:\app`（HX 家族多功能工具箱 v5.0、minehub） |
| 仓库 | <https://github.com/1922708082/rigkit> |
| 目标平台 | Windows 10 22H2 / Windows 11 x64 |
| 首版范围 | NVIDIA CMP 六款（30HX / 40HX / 50HX / 70HX / 90HX / 170HX）+ Pascal 矿卡 P102-100 / P104-100 / P106-100 / P106-090 |
| 开源协议 | Apache-2.0 |

## 0. 结论摘要

**一句话定位**：矿卡工具箱 RigKit 是面向矿卡持有者的硬件全生命周期工作台，把「验卡、解锁、调优、维护」四件事做成可预检、可回滚、可交付报告的标准流程。

**推荐路线**：`.NET 10 LTS + WPF（MVVM）+ 独立提权 Broker + 规则包驱动 + 单文件绿色版 / 安装版双形态`。不沿用现有 C# 5.0 + 单进程管理员架构，但完整复用 NativeCore 里已经踩过坑的底层能力。

**六个关键决策**

1. UI 与硬件写操作拆成两个进程，UI 永不提权，Broker 只执行白名单命令（解决现有单进程全权管理员的最大风险）。
2. 所有写操作走「预检 → 快照 → 执行 → 验证 → 提交/回滚」事务模型，失败自动回滚（现有项目的备份分散在各功能里，没有统一快照与自动回滚）。
3. 卡型能力做成签名规则包，新卡型不发客户端版本也能支持（现有项目每加一张卡都要改 400KB 的 NativeCore）。
4. UI 走设计系统 + 页面组件化，不再用 20 万字符的单文件 XAML 和字符串 FindName 绑定。
5. 独立新版本，不背旧版兼容包袱；旧版仅作能力参考，不迁移配置与数据。
6. 开源发布：源码公开、可复现构建、开源签名 + Sigstore 校验，降低杀软误报与用户信任成本。

**最大三个风险**：解锁失败导致设备异常（变砖）、杀软误报导致分发受阻、驱动更新破坏伪装状态。对应缓解措施见第 7 章。

**预估周期**：单人 + AI 辅助，M0 到 M4 约 9~10 周（P 系列增加约 0.5~1 周）；首个可对外测试版约 6~7 周。

## 1. 参考项目分析

### 1.1 `D:\app` 里有什么

| 路径 / 产物 | 角色 | 技术形态 | 观察 |
|---|---|---|---|
| `HX家族多功能工具箱.exe` / `90HX Studio.exe` | 当前主力产品 v5.0 | WPF + C# 5.0 单文件 | 48.6 MB，`requireAdministrator`，12 个功能页 |
| `build_source/src/` | 全部源码 | 14 个 `.cs`（13 个参与编译）+ 4 个 XAML（3 个内嵌） | 无工程文件、无 NuGet、无测试 |
| `build_source/build.ps1` | 唯一构建入口 | 系统 `csc.exe` 直接编译 | `/warnaserror+`，警告即失败 |
| `assets.zip` | 内嵌资源包 | ZIP，47.5 MB | 每次发版全量替换 |
| `build_source/mining_unlock_toolkit/` | 各型号解锁套件 | EFI / 脚本 / 说明 | 30HX、40HX、50HX、90HX 分目录 |
| `build_source/nvperm_esp_kit/` | 引导解锁核心 | `NVPermissiveEFI.efi`、`bootmgfw.efi`、`shellx64.efi` | 解锁能力的技术底座 |
| `minehub/`（BlockKit） | 另一个完整项目 | Node.js + SQLite + Docker + Nginx | 有签名下载、RBAC、CDN、特性开关、51 项测试 |
| `wpfui_*` / `aardio_project/` | 早期技术验证 | WPF-UI / aardio | 未进入主线路，可作为历史参考 |
| `HANDOVER.md` / `docs/` | 交接与设计资料 | Markdown | 记录了关键坑与布局基线 |

`minehub` 虽然业务不同（Minecraft 模组站），但它的**分发侧工程模式**很成熟：签名下载、CDN 回源、特性开关、分阶段测试。这套模式可以直接复用到 RigKit 的更新服务与规则包分发，不需要从零设计。

### 1.2 现有产品能力地图

现有 12 个页面，按「设备 → 显卡工具 → 性能优化 → 系统」四组组织：

| # | 页面 | 核心能力 | 底层依赖 | 成熟度 |
|---|---|---|---|---|
| 1 | 硬件信息 | 主板 / CPU / 内存 / 显卡 / 显示器 / 硬盘 / 声卡 / 网卡全景 | WMI、注册表、NVML | 高 |
| 2 | 显卡型号伪装 | 76 款 GeForce 型号，注册表 + 应用缓存联动，PnP 热重载 | 注册表、SetupAPI、NVIDIA App 缓存 | 高 |
| 3 | 游戏内覆盖 | 绕过 NVIDIA App 白名单，解锁 Alt+R / Alt+Z | 前端 JS 补丁、CefCache 清理 | 中高 |
| 4 | 90HX 解锁 | NVPermissiveEFI，通道 A/B/C/D，图形+算力+光追 | BCD、ESP、EFI 文件释放 | 高 |
| 5 | 50HX 解锁 | Turing 光追激活 | 同上 | 中 |
| 6 | 40HX 解锁 | TU106 纯注册表安全伪装 | 注册表 | 中高 |
| 7 | 双卡调度 | 核显输出 + 矿卡渲染，MS-Hybrid | 注册表、D3D 适配器枚举 | 中 |
| 8 | DLSS 3 帧生成 | 多镜像测速、运行库热替换、RE 框架注入 | HTTP、文件替换 | 中高 |
| 9 | 硬件与电源 | HAGS、电源计划、TGP、风扇、驱动重启 | NVAPI、powercfg | 中 |
| 10 | 审计日志 | 深色终端日志 | 本地文件 | 中 |
| 11 | 赞赏与支持 | 收款码 | 静态资源 | 完成 |
| 12 | 软件更新 | GitHub Release 检测与下载 | GitHub 页面解析 | 低（待重做） |

这张表说明一件事：**现有产品的能力密度已经很高，缺的不是功能，而是工程结构、安全边界和产品化表达**。RigKit 的重点不是再加十个功能，而是把已有能力重新组织成可信、可维护、可分发的产品。

### 1.3 技术结构与构建链

```
build.ps1
  └─ csc.exe（.NET Framework 4.x，C# 5.0 语法）
       ├─ /target:winexe
       ├─ /win32manifest:app.manifest  → requireAdministrator
       ├─ /resource:MainWindow.xaml    → XamlReader.Load 运行时解析
       ├─ /resource:assets.zip         → 运行时解压到 ProgramData
       └─ 13 个参与编译的 .cs 一次性编译
            └─ 输出 90HX Studio.exe（约 48.6 MB）
                 └─ 复制为 HX家族多功能工具箱.exe，并同步桌面
```

关键规模数据：

| 文件 | 体积 | 说明 |
|---|---|---|
| `NativeCore.cs` | 400 KB，205 个方法 | 注册表、BCD、EFI、nvidia-smi、省电、伪装、DLSS、更新全部塞在一起 |
| `App.cs` | 201 KB | 生命周期、控件绑定、事件、导航、传感器刷新 |
| `MainWindow.xaml` | 206 KB | 12 个页面 + 全部样式，单文件 |
| `assets.zip` | 47.5 MB | 解锁套件、DLSS 运行库、图片等 |

这个结构在单人快速迭代阶段是高效的，但已经出现明显的边际成本：改动一个页面要动 20 万字符的 XAML；新增一个卡型要改上帝类；任何 XAML 重名都会导致整个窗口加载失败。

### 1.4 现有设计语言

从 `screenshots/cockpit.png`、`unlock.png` 和 `docs/界面结构概览.md` 可以提炼出：

| 维度 | 现状 |
|---|---|
| 风格 | Clean Studio 浅色，接近苹果工作台 + 图吧工具箱 |
| 背景 | `#FBFBFD`，侧栏 `#F5F5F7`，卡片 `#FFFFFF` |
| 主色 | `#0071E3`，选中底 `#E3F0FF` |
| 文字 | 主 `#1D1D1F`，次 `#475467`，辅助 `#98A2B3` |
| 布局 | 42px 顶栏 + 208~220px 左侧分组导航 + 内容区 + 底部状态栏 |
| 组件 | 卡片圆角 9~11px、胶囊按钮、状态徽章、环形仪表、深色日志终端 |
| 优点 | 信息层级清楚、状态一眼可见、轻量不花哨 |
| 不足 | 没有 token 化、页面样式硬编码、缺深色模式、图表能力弱、空状态与错误态缺失 |

这套视觉资产可以直接继承为 RigKit 的「浅色工作台」基线，但需要补一套深色「Rig Mode」和完整的设计 token。

### 1.5 可直接复用的资产

| 资产 | 复用方式 | 价值 |
|---|---|---|
| NativeCore 205 个方法 | 按领域拆成 8 个 Handler，保留实现细节 | 省掉 3~4 周底层踩坑 |
| PCI ID / 76 款型号映射 | 转成 `rules/nvidia-cmp.json` | 立刻具备卡型识别与伪装目标推荐 |
| NVPermissiveEFI 套件 | 打成签名资源包，校验 SHA-256 | 解锁能力的核心 |
| 省电 / 驱动热重载 / 伪装逻辑 | 移植为 Capability Handler | 已验证的时序与容错参数 |
| AI 网关与动作注册表 | 升级为 tool schema + 多步编排 | 已有可用后端与 prompt 资产 |
| build / render 脚本 | 改造成 CI + 视觉回归 | 已有无头渲染验证思路 |
| 设计 token 与截图 | 作为新设计系统的起点 | 保持老用户熟悉感 |
| minehub 分发模式 | 更新服务、签名下载、CDN、灰度 | 少走分发侧的弯路 |

### 1.6 现存问题诊断

| # | 问题 | 证据 | 影响 | RigKit 对策 |
|---|---|---|---|---|
| P1 | 上帝类 | `NativeCore.cs` 400 KB / 205 方法 | 改一处牵连全部，无法单测 | 按 Capability 拆分，接口隔离 |
| P2 | 单文件巨型 XAML | `MainWindow.xaml` 206 KB，12 页 | 重名即整窗崩溃，页面无法并行开发 | 每页独立 View + 组件库 |
| P3 | 全进程管理员 | manifest `requireAdministrator` | UI、网络、AI、硬件写入同权限，攻击面大 | UI 不提权 + Broker 白名单 |
| P4 | 语言版本锁死 | C# 5.0，无插值 / nameof / 记录类型 | 开发效率低，第三方库不可用 | .NET 10 + C# 14 |
| P5 | 无事务与快照 | 备份分散在各功能，无统一快照与自动回滚 | 失败后依赖用户自救 | 快照 + 自动回滚 + 救援盘 |
| P6 | 无规则包 | 新卡型必须改代码发版 | 支持周期长，长尾卡型覆盖不了 | 签名规则包，热更新 |
| P7 | AI 只是关键词匹配 | `AiAgentActionRegistry.Match()` | 复杂表达匹配不到，不能多步执行 | 工具调用 + 计划编排 + 执行日志 |
| P8 | 更新链路脆弱 | 解析 GitHub Release HTML | 国内网络不稳，无签名校验、无灰度 | 自建 manifest + CDN + 签名 + 通道 |
| P9 | 会话与单实例坑 | Session 0 读不到 GPU；互斥体导致“启动即退” | 用户误判为崩溃 | Agent 跑用户会话；启动诊断面板 |
| P10 | 无崩溃聚合 | 只有本地 `crash.log` | 问题靠用户口述，无法定位 | 本地 dump + 可选匿名上报 |
| P11 | 资源全量内嵌 | 47.5 MB `assets.zip` | 每次更新全量下载，启动解压慢 | 资源分包 + 按需下载 + 差分 |
| P12 | 样式硬编码 | 每页内联样式 | 视觉不一致，改版成本高 | Design Token + 统一组件 |

### 1.7 首版新增：Pascal 矿卡 P102 / P104 / P106

现有 HX 工具箱只覆盖 CMP（Ampere / Turing / Volta 衍生型号），P 系列是本次新增范围。它们和 CMP 的底层逻辑不同，不能照搬“解锁 + 伪装 + 游戏”的叙事。

| 型号 | 架构 | 显存 | 典型功耗 | 视频输出 | 图形能力 | 计算能力 | 首版支持级别（统一 Experimental） |
|---|---|---|---|---|---|---|---|
| P102-100 | GP102 | 10 GB GDDR5X | 250 W | 无 | 无官方图形驱动 | CUDA 可用（专用驱动） | 实验性：识别 / 体检 / 调优 / 计算 |
| P104-100 | GP104 | 8 GB GDDR5X | 180 W | 无 | 社区驱动有限 | CUDA 可用 | 实验性：识别 / 体检 / 调优 / 计算 |
| P106-100 | GP106 | 6 GB GDDR5 | 120 W | 无 | 社区修改驱动可游戏 | CUDA 可用 | 实验性：识别 / 体检 / 调优 / 游戏（受限） |
| P106-090 | GP106 | 3 GB GDDR5 | 75 W | 无 | 不建议 | CUDA 可用 | 实验性：识别 / 体检 / 计算（PCIe x1） |

**P 系列整体标记 `Experimental`，这是产品决策，不是临时状态。** 即使体检全绿，UI 也只显示“实验性”，不显示 Supported / Verified，不承诺长期稳定性。升级为 Supported 的条件：至少 3 张不同型号真机、30 天连续运行、无不可恢复故障、驱动来源可追溯。

三条硬约束：

1. **不捆绑、不分发修改版驱动。** NVIDIA 官方不为 P 系列提供 GeForce 驱动，社区修改驱动涉及 INF 改动与签名问题。RigKit 只做驱动状态检测、版本建议、签名校验与回滚，驱动文件由用户自行从可信来源获取。
2. **不承诺图形解锁。** P 系列没有视频输出，游戏必须依赖核显输出 + 跨适配器渲染；P102/P104 基本不适合游戏，P106-100 仅在特定修改驱动下可玩。UI 必须明确标注“实验性”，不能写成“一键解锁”。
3. **不做 vBIOS 写入。** 只读备份与校验，避免把“体检工具”变成“变砖工具”。

因此 P 系列的能力矩阵以「体检 + 功耗 + 计算 Profile + 驱动诊断」为主，和 CMP 的「引导解锁 + 伪装 + 覆盖」是两条产品线，但共用同一套设备抽象、快照与报告。

## 2. 产品定义

### 2.1 定位

**矿卡工具箱 RigKit** 是面向矿卡持有者的硬件全生命周期工作台。

它解决的不是“怎么挖矿”，而是“怎么把一张来路不明、能力受限的矿卡，安全地变成可验证、可调优、可长期使用的生产力设备”。

Slogan：**让每一张矿卡，有据可查，有路可退。**

英文：**Know your card. Keep a way back.**

三层价值：

1. **买前敢买**：10 分钟出一份可分享的验卡报告，温度、显存、PCIe、稳定性、矿龄推断都有据可查。
2. **买后能用**：解锁 PCIe、恢复图形能力、伪装型号、修驱动、开覆盖、上 DLSS，全流程有预检有回滚。
3. **长期好养**：功耗/风扇/场景 Profile、异常告警、快照恢复、诊断包导出。

### 2.2 目标用户与场景

| 角色 | 典型画像 | 核心任务 | 关键诉求 |
|---|---|---|---|
| 二手买家 | 图吧 / 闲鱼用户，预算敏感 | 面交前验卡、到手复检 | 报告可信、结论直白、不误判 |
| 矿卡玩家 | 有 1~2 张 CMP，玩 3A 和本地 AI | 解锁、伪装、DLSS、双卡 | 一次成功、失败能退、别变砖 |
| 小型工作室 / 装机店 | 5~50 张卡，接渲染/推理单 | 批量体检、统一调优、交付报告 | 可批量、可导出、可复现 |
| 内容创作者 | 做矿卡测评 | 数据采集、前后对比 | 图表好看、数据可导出 |

三个高频 Job：

- “这张 40HX 值不值 400 块？” → 体检报告 + 同型号基线对比。
- “怎么让它打游戏不闪退？” → 解锁向导 + 伪装 + 驱动修复。
- “昨天调完今天又掉驱动了，怎么恢复？” → 快照时间线 + 一键回滚。

### 2.3 价值主张与差异化

| 维度 | 常见矿卡工具 | RigKit |
|---|---|---|
| 安全 | 一键脚本，失败靠重装系统 | 预检 + 快照 + 自动回滚 + 救援盘 |
| 通用 | 只支持单一型号 | 规则包驱动，CMP 六款 + P 系列四款起步，可扩展到 AMD |
| 透明 | 黑盒 bat，不知道改了什么 | 每一步命令、注册表路径、前后差异可见可导出 |
| 可验证 | 改完靠感觉 | 内置基准与稳定性测试，改动前后对比 |
| 可维护 | 出新卡等作者发版 | 规则包热更新，社区可贡献 |
| 表达 | 极客向，术语堆砌 | 说人话，风险分级，给建议而不是给参数 |

### 2.4 功能地图

| 优先级 | 模块 | 功能 | 说明 |
|---|---|---|---|
| P0 | 设备资产 | 自动发现、卡型指纹、能力矩阵、资产卡 | CMP 六款 + P102/P104/P106 四款（统一标 Experimental）；以 PCI 位置为主键，多卡不乱序 |
| P0 | 一键体检 | 8 项检测、评分、报告导出 | 支持 HTML / PDF / 分享码 |
| P0 | CMP 安全解锁向导 | PCIe Gen2、计算模式、引导通道 A/B/C/D | 仅 CMP；预检、风险告知、快照、验证 |
| P0 | P 系列驱动诊断 | 驱动状态、版本建议、签名校验、回滚 | 不捆绑修改驱动，只做检测与指引 |
| P0 | 传感器监控 | 温度 / 功耗 / 频率 / 显存 / 风扇 / PCIe | 1s 采样，阈值告警，托盘常驻 |
| P0 | 快照与恢复 | 注册表、BCD、驱动状态、配置文件 | 时间线、差异对比、一键还原 |
| P0 | 日志与诊断 | 结构化日志、诊断包导出 | 脱敏，支持一键附到反馈 |
| P1 | 性能调优 | 功耗墙、核心/显存偏移、风扇曲线、场景 Profile | 读 vBIOS 上下限，防非法值 |
| P1 | 生态补丁 | NVIDIA App 覆盖、DLSS 运行库、RE 框架 | 补丁可检测、可还原 |
| P1 | 双卡调度 | 核显输出 + 矿卡渲染，按应用绑定 | MS-Hybrid 状态可视化 |
| P1 | 基准测试 | D3D12 计算、显存带宽、稳定性压测 | 与同型号基线对比 |
| P1 | 更新与规则包 | 客户端更新、规则包热更新、灰度通道 | 签名校验、失败回滚 |
| P2 | AI Copilot | 工具调用、多步任务、执行解释 | 默认只读，写操作需确认 |
| P2 | 插件市场 | 第三方工具、卡型包、报告模板 | 签名 + 权限声明 |
| P2 | 多机管理 | 局域网批量体检、统一配置 | 需轻量服务端 |

### 2.5 三条关键用户旅程

**旅程 A：买前验卡（10 分钟）**

```
插入卡 → 打开 RigKit → 自动识别 → 一键体检
  → 8 项检测（约 6 分钟，含 3 分钟压测）
  → 生成评分与结论（建议购买 / 谨慎 / 不建议）
  → 导出报告或生成分享码 → 发给卖家/朋友
```

**旅程 B：买后改造（约 30 分钟 + 1 次重启）**

```
体检通过 → 改造中心 → 选择目标（游戏/渲染/推理）
  → 系统预检（管理员、Secure Boot、PCIe 通道、磁盘空间）
  → 展示计划与风险 → 自动快照 → 执行解锁
  → 重启 → 开机自检 → 结果报告
  → 失败则进入恢复向导，一键回滚
```

**旅程 C：日常维护（长期）**

```
托盘常驻 → 传感器异常告警（温度/掉驱动/功耗异常）
  → 一键诊断 → 给出原因与动作
  → 应用 Profile（游戏/静音/渲染）
  → 驱动更新后自动检测伪装是否失效 → 提示修复
```

### 2.6 边界：不做什么

- 不做矿池连接、算力租赁、收益计算，不参与任何加密货币挖矿功能。
- 不写入 vBIOS，只做只读备份与校验；恢复官方 vBIOS 仅限白名单卡型且需二次确认。
- 不绕过游戏反作弊、DRM、账号风控。
- 不修改系统 INF 文件与驱动签名（现有项目已因此踩坑，明确列为红线）。
- 不收集硬件序列号、不默认上传遥测。
- 不承诺超频收益或“永久解锁”，所有改动都标注可逆性。

## 3. 系统架构与技术选型

### 3.1 设计原则

1. **最小权限**：UI 以普通用户运行，只有 Broker 提权，且只认白名单命令。
2. **一切可回滚**：任何写操作先产生快照，失败自动回滚，回滚失败给出人工恢复路径。
3. **可解释**：每个操作有计划、有步骤、有前后差异、有日志。
4. **规则与代码分离**：卡型支持、推荐参数、已知坑放规则包，客户端只负责执行引擎。
5. **离线优先**：无网络也能完成识别、体检、解锁、回滚；AI 与更新是增强项。
6. **渐进式迁移**：老功能按模块迁移验收，不搞大爆炸重写。

### 3.2 技术选型对比

| 维度 | A. 延续 .NET Framework 4.8 + csc | B. .NET 10 + WPF（推荐） | C. .NET 10 + WebView2 混合 | D. Tauri 2 + Rust |
|---|---|---|---|---|
| 现有资产复用 | ★★★★★ | ★★★★☆ | ★★★☆☆ | ★☆☆☆☆ |
| 安全与稳定 | ★★☆☆☆ | ★★★★☆ | ★★★☆☆ | ★★★★☆ |
| UI 表现力 | ★★★☆☆ | ★★★★☆ | ★★★★★ | ★★★★★ |
| 开发效率 | ★★☆☆☆ | ★★★★☆ | ★★★★☆ | ★★☆☆☆ |
| 分发体积 | ★★★★★ | ★★★☆☆ | ★★★★☆ | ★★★★★ |
| 长期维护 | ★☆☆☆☆ | ★★★★★ | ★★★☆☆ | ★★☆☆☆ |
| 结论 | 只适合过渡 | **首选** | 视觉优先时备选 | 不建议 |

**为什么选 B**

- 现有 205 个底层方法都是 C#，迁移到 .NET 10 是改语法与拆结构，不是重写逻辑；选 Rust 等于全部重来。
- WPF 在 .NET 10 上仍然是一等公民，硬件工具需要低延迟传感器刷新，原生控件比 WebView 更稳。
- 自包含单文件发布后约 70~90 MB，和现在 48.6 MB 同量级，用户无感。
- MVVM + DI + xUnit 能真正建立测试与回归，这是现有项目最缺的。
- WebView2 方案视觉上限更高，但会引入前后端 IPC、Web 运行时依赖和额外的调试面；首版不建议。

**关于 .NET 版本**：当前时间点 .NET 10 是 LTS，.NET 8 已接近支持尾期。新项目直接上 .NET 10，目标 `net10.0-windows`，`win-x64` 自包含发布。

### 3.3 总体架构

```
┌─────────────────────────────────────────────────────────────────────┐
│                          RigKit.App (UI)                           │
│  WPF + MVVM   工作台 / 体检 / 改造 / 调优 / 维护 / 设置 / AI Drawer   │
│  权限：asInvoker（永不提权）                                          │
└───────────────┬─────────────────────────────────────────────────────┘
                │ Named Pipe + JSON-RPC 2.0（长度前缀帧）
┌───────────────▼─────────────────────────────────────────────────────┐
│                       RigKit.Application                            │
│  用例编排：体检流程 / 操作计划 / 快照事务 / 报告生成 / 更新检查         │
├─────────────────────────────────────────────────────────────────────┤
│                          RigKit.Domain                              │
│  Device / Capability / OperationPlan / Snapshot / RiskRule / Profile │
├─────────────────────────────────────────────────────────────────────┤
│                     RigKit.Infrastructure                           │
│  DeviceProviders  SnapshotStore  RulePacks  Http  Storage  Logging   │
└───────────────┬─────────────────────────────────────────────────────┘
                │ 进程内调用（仅只读查询）
┌───────────────▼─────────────────────────────────────────────────────┐
│                     RigKit.Broker（提权进程）                        │
│  命令白名单 / 参数 Schema 校验 / 调用方校验 / 审计 / 执行 Handler      │
│  权限：requireAdministrator，按需启动，空闲 60s 退出                   │
└───────────────┬─────────────────────────────────────────────────────┘
                │ P/Invoke / 系统命令 / 文件操作
┌───────────────▼─────────────────────────────────────────────────────┐
│  NVML · NVAPI · SetupAPI · CfgMgr · 注册表 · BCD · ESP · WMI · 文件系统 │
└─────────────────────────────────────────────────────────────────────┘

旁路进程：
  RigKit.Agent    托盘常驻、传感器采集、告警、自动 Profile（用户会话，非服务）
  RigKit.Updater  等待主进程退出、校验、替换文件、失败回滚
```

### 3.4 进程与权限模型

| 进程 | 权限 | 生命周期 | 职责 | 不做什么 |
|---|---|---|---|---|
| `RigKit.App` | asInvoker | 用户启动 | UI、只读查询、报告、AI 对话 | 不写注册表、不碰 BCD、不执行系统命令 |
| `RigKit.Broker` | requireAdministrator | 按需启动，空闲 60s 退出 | 执行白名单写操作、快照、回滚 | 不接受任意命令行、不联网 |
| `RigKit.Agent` | asInvoker | 开机自启（可选） | 传感器、告警、Profile 切换 | 不提权，需要写操作时唤起 Broker |
| `RigKit.Updater` | asInvoker → 替换时提权 | 更新时启动 | 校验、替换、回滚 | 不解析网络内容，只认 manifest |

**为什么不用 Windows 服务跑传感器**：现有项目已经踩过 Session 0 的坑，服务进程拿不到 WDDM 显示句柄，NVAPI 与利用率返回 -1。Agent 必须跑在用户交互会话；只有需要“无人登录也执行”的更新场景才考虑服务，且与传感器分离。

**Broker 安全约束**

- 管道 ACL：仅当前用户 + Administrators，拒绝 Everyone。
- 调用方校验：取客户端 PID → 校验镜像路径与签名 → 只接受 `RigKit.App.exe` / `RigKit.Agent.exe`。
- 命令白名单：注册表路径、BCD 操作、文件路径全部由 Broker 侧模板生成，客户端只能传枚举与参数值。
- Schema 校验：每个命令有 JSON Schema，拒绝越界、超长、路径穿越、命令拼接。
- 速率限制：高危命令 1 次/10s，失败 3 次锁定 5 分钟。
- 审计：每次调用记录时间、调用方、命令、参数摘要、结果、快照 ID。

### 3.5 核心领域模型

```csharp
public sealed record DeviceId(string PciLocation, string PnpInstanceId);

public sealed record Device(
    DeviceId Id,
    string Model,
    string Architecture,
    string Vendor,
    string PciIds,
    string DriverVersion,
    DeviceState State);

public enum SupportLevel { Unsupported, DetectOnly, Experimental, Supported, Verified }

public sealed record Capability(
    string Key,                 // pcie.unlock / spoof.rename / tune.power ...
    SupportLevel Level,
    string Evidence,            // 规则包给出的依据
    IReadOnlyList<string> Constraints);

public enum RiskLevel { Safe, Low, Medium, High, Critical }

public sealed record OperationStep(
    int Order,
    string Title,
    string CommandId,
    IReadOnlyDictionary<string, string> Args,
    bool RequiresReboot);

public sealed record OperationPlan(
    Guid Id,
    string Title,
    RiskLevel Risk,
    IReadOnlyList<Precondition> Preconditions,
    IReadOnlyList<OperationStep> Steps,
    RollbackStrategy Rollback,
    string Explanation);

public sealed record Snapshot(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Operation,
    IReadOnlyList<SnapshotItem> Items,
    string ManifestHash);
```

### 3.6 操作事务模型

```
Draft
  → Preflight（权限/系统版本/Secure Boot/PCIe/磁盘/驱动/冲突检测）
  → AwaitingConfirm（展示计划、风险、预计耗时、回滚方式）
  → Snapshotting（注册表/BCD/驱动状态/配置，生成快照 ID）
  → Executing（逐步执行，推送进度与日志）
  → Verifying（设备状态、驱动错误码、链路速率、功能探测）
  → Succeeded（写审计，保留快照）
  → Failed（自动进入 RollingBack）
  → RollingBack（按逆序还原）
  → RolledBack / ManualIntervention（给出救援盘与人工步骤）
```

三条硬规则：

1. 没有快照 ID 不允许执行写操作。
2. 验证不通过不允许标记成功，即使命令返回 0。
3. 回滚失败必须升级为 `ManualIntervention`，并在 UI 顶部常驻红色横幅，给出救援路径。

### 3.7 IPC 协议

统一使用 Named Pipe `\\.\pipe\RigKit.Broker.v1`，长度前缀 + UTF-8 JSON，JSON-RPC 2.0。

```json
{
  "jsonrpc": "2.0",
  "id": "8f2c1a",
  "method": "op.execute",
  "params": {
    "planId": "0d9b...",
    "snapshotId": "71ae...",
    "confirmToken": "user-typed-confirm-token"
  }
}
```

| 方法 | 方向 | 说明 |
|---|---|---|
| `system.info` | App → Broker | 权限、系统版本、Secure Boot、可用空间 |
| `device.list` | App → Broker | 设备枚举与状态 |
| `sensor.snapshot` | App → Broker | 一次传感器采样 |
| `op.plan` | App → Broker | 生成计划（不执行） |
| `op.execute` | App → Broker | 执行计划 |
| `op.cancel` | App → Broker | 取消未开始步骤 |
| `snapshot.list` / `snapshot.restore` | App → Broker | 快照管理 |
| `log.tail` | App → Broker | 增量日志 |
| `event.*` | Broker → App | 进度、日志、设备变化通知 |

### 3.8 数据存储

```
%ProgramData%\RigKit\
├─ config\
│  ├─ app.json              界面、更新通道、语言
│  ├─ profiles.json         调优 Profile
│  └─ privacy.json          遥测开关（默认全关）
├─ data\
│  ├─ rigkit.db            SQLite：设备历史、审计、快照索引
│  └─ sensors\              按天降采样的时序数据（保留 30 天）
├─ snapshots\
│  └─ <timestamp>_<op>\
│     ├─ manifest.json      项列表、哈希、创建者、版本
│     ├─ registry.reg      导出片段
│     ├─ bcd.txt            bcdedit 导出
│     └─ files\            被替换文件的副本
├─ logs\
│  ├─ app-YYYYMMDD.log
│  ├─ broker-YYYYMMDD.log
│  └─ crash\
├─ packs\
│  ├─ rules\nvidia-cmp.json
│  └─ tools\                按需下载的解锁套件
└─ update\                  下载缓存与回滚包
```

便携模式：`RigKit.exe --portable` 时数据目录改为 exe 同级 `.\data`，并在标题栏显示「便携模式」。如果检测到 exe 位于 `Program Files`，提示可能无写权限。

### 3.9 规则包机制

规则包是 RigKit 的核心扩展点。客户端只实现「能力原语」，规则包描述「哪张卡支持什么、怎么做、有什么坑」。

```json
{
  "schema": 1,
  "pack": "nvidia-cmp",
  "version": "2026.10.1",
  "devices": [
    {
      "match": { "pciIds": ["10DE:2189"], "subsystem": ["*"] },
      "model": "NVIDIA CMP 30HX",
      "architecture": "TU116",
      "capabilities": {
        "pcie.unlock": { "level": "supported", "handler": "nvperm", "channels": ["uefi", "shell", "usb"] },
        "spoof.rename": { "level": "supported", "targets": ["GTX 1660 SUPER"] },
        "tune.power": { "level": "experimental", "min": 70, "max": 125 }
      },
      "knownIssues": [
        { "id": "cmp30-pcie-coldboot", "title": "冷启动偶发回落 Gen1", "fix": "重新执行通道 A 并断电 10 秒" }
      ]
    }
  ],
  "signature": "base64-ed25519"
}
```

约束：

- 规则包必须 Ed25519 签名，公钥内置客户端。
- `handler` 只能引用客户端已注册的原语 ID，规则包不能执行任意代码。
- 规则包有独立版本号，可与客户端版本解耦。
- 加载失败或签名不通过时回退到内置基线包，并在 UI 提示。

### 3.10 AI 架构

目标不是“会聊天的助手”，而是“能看懂当前设备状态、能给出可执行计划、能解释每一步”的 Copilot。

```
用户问题
  → 意图识别（本地规则 + 模型）
  → 上下文注入：设备快照、传感器、最近操作、日志摘要、规则包 knownIssues
  → 模型返回 tool_calls（只允许 OperationRegistry 中的只读/写操作）
  → 本地生成 OperationPlan（预检 + 风险 + 快照）
  → 用户在 UI 确认（高危需输入确认词）
  → Broker 执行 → 事件流回 UI → 模型总结结果
```

| 能力 | 首版 | 说明 |
|---|---|---|
| 只读问答 | 支持 | “这张卡为什么跑在 PCIe 1.1？” |
| 单步操作建议 | 支持 | 给出按钮，用户点击执行 |
| 多步任务链 | P2 | “解锁并伪装成 3080” |
| 失败自主排障 | P2 | 读错误码，尝试替代方案，最多 3 轮 |
| 离线降级 | 支持 | 无网络时用本地知识库 + 规则引擎 |
| 隐私 | 默认关闭上传 | 只上传脱敏后的状态摘要，用户可查看原文 |

网关沿用现有 `api.muyaa.top`，新增 `rigkit` 路由与独立配额；同时允许用户填自己的 OpenAI 兼容 Key。

**双通道计费与 Key 管理**

| 模式 | 适用用户 | 接入方式 | 额度与计费 | 安全要求 |
|---|---|---|---|---|
| A. 内置网关 | 不想折腾的普通用户 | `https://api.muyaa.top/rigkit/v1`，设备匿名令牌 | 每日 50,000 token 免费额度（输入+输出合计），北京时间 0 点重置；超出提示充值或引导自填 Key | 客户端不持有上游 Key；令牌可撤销；服务端按设备令牌 + IP 双维度限流 |
| B. 自带 Key | 有自己 API 的高级用户 | 任意 OpenAI 兼容 Base URL + Key + 模型名 | 用户自付，RigKit 不抽成 | Key 用 Windows DPAPI（CurrentUser）加密存储，绝不写日志/诊断包 |
| C. 离线 | 无网络或隐私敏感 | 本地规则引擎 + 知识库 | 免费 | 不出网 |

交互细节：

- 设置页提供「连接测试」，返回延迟、模型列表与错误原因，不打印 Key。
- 每次发送前展示将上传的上下文摘要（设备型号、传感器、错误码），用户可取消。
- 网关失败自动降级到自带 Key（若已配置），再失败降级到离线规则，不阻塞主流程。
- 额度与账单只在设置页展示，不在主界面打扰。
- 免费额度每日 50,000 token（输入 + 输出合计），约等于 20~30 次带设备上下文的问答；超出后不静默扣费，必须用户确认充值或切到自带 Key。

### 3.11 技术栈清单

| 层 | 选型 | 版本 | 理由 |
|---|---|---|---|
| 运行时 | .NET | 10 LTS | 长期支持，WPF 一等公民 |
| UI | WPF + CommunityToolkit.Mvvm | 8.x | 原生、低延迟、成熟 |
| 图表 | LiveChartsCore.SkiaSharpView.WPF | 2.x | MIT，传感器曲线够用 |
| 日志 | Serilog + File Sink | 4.x | 结构化、滚动、易脱敏 |
| 存储 | Microsoft.Data.Sqlite + Dapper | 9/2.x | 轻量，无需 ORM 重开销 |
| IPC | Named Pipes + StreamJsonRpc | 2.x | 成熟，支持通知与取消 |
| JSON | System.Text.Json | 内置 | 源生成，AOT 友好 |
| 原生互操作 | CsWin32 + 手写 P/Invoke | 最新 | 减少句柄与结构体错误 |
| 测试 | xUnit + FluentAssertions | 最新 | 单测 + 集成测 |
| UI 自动化 | FlaUI（可选） | 4.x | 冒烟与回归 |
| 安装器 | Inno Setup | 6.x | 轻量、脚本化、国内常用 |
| CI | GitHub Actions | windows-latest | 与现有 Release 流程衔接 |
| 签名 | signtool（Authenticode）+ Sigstore cosign + Ed25519（NSec） | 最新 | 安装包签名、发布物透明签名、规则包签名 |

### 3.12 不选其他方案的理由

- **Electron**：体积 150 MB 起，内存高，硬件工具不需要 Chromium。
- **Tauri**：安装包小、UI 好，但现有 C# 底层资产全部作废，Rust 侧写 BCD/EFI/NVAPI 成本过高。
- **Avalonia**：跨平台好，但本项目只做 Windows，WPF 的硬件与系统集成更直接。
- **WinUI 3**：打包与部署链路更复杂，XAML 生态与现有资产差异大。

## 4. 模块划分与数据流

### 4.1 解决方案结构

```
RigKit.slnx
├─ src/
│  ├─ RigKit.App/                 WPF 主程序（asInvoker）
│  │  ├─ Views/                    Home / Health / Unlock / Tune / Maintain / Settings
│  │  ├─ ViewModels/
│  │  ├─ Controls/                 Gauge / StatusPill / RiskBanner / Stepper / LogConsole
│  │  ├─ Themes/                   tokens.xaml / light.xaml / dark.xaml / controls.xaml
│  │  └─ Services/                 Navigation / Dialog / Notification / Update
│  ├─ RigKit.Application/         用例编排，不依赖 WPF
│  │  ├─ Health/                   CheckRunner / ScoreCalculator / ReportBuilder
│  │  ├─ Operations/               PlanBuilder / Preflight / Transaction / Verifier
│  │  ├─ Devices/                  DeviceService / CapabilityResolver
│  │  └─ Updates/                  UpdateService / RulePackService
│  ├─ RigKit.Domain/              实体、值对象、规则，不依赖任何基础设施
│  ├─ RigKit.Infrastructure/      实现层
│  │  ├─ Nvidia/                  NvidiaSmi provider（已实现）/ NVML / NVAPI
│  │  ├─ Devices/                 WMI / SetupAPI providers
│  │  ├─ Snapshots/                Registry / Bcd / File snapshot stores
│  │  ├─ Rules/                    RulePackLoader / SignatureVerifier
│  │  ├─ Storage/                  Sqlite / JsonConfig
│  │  └─ Http/                     UpdateClient / AiClient
│  ├─ RigKit.Broker/              提权执行器
│  │  ├─ PipeServer/               ACL / 调用方校验 / 限流
│  │  ├─ Handlers/                 Pcie / Spoof / Driver / Power / Files / Bcd
│  │  └─ Audit/
│  ├─ RigKit.Agent/               托盘、传感器、告警、自动 Profile
│  ├─ RigKit.Updater/             更新替换与回滚
│  └─ RigKit.Contracts/           DTO、IPC 契约、命令 Schema
├─ tools/
│  └─ RigKit.Probe/               设备探测命令行（开发期验证）
├─ tests/
│  ├─ RigKit.UnitTests/
│  ├─ RigKit.IntegrationTests/    模拟 Provider，CI 可跑
│  └─ RigKit.HardwareTests/       真机手工/半自动
├─ rules/
│  ├─ nvidia-cmp.json
│  └─ schema/rules.schema.json
├─ build/
│  ├─ build.ps1
│  ├─ sign.ps1
│  ├─ installer.iss
│  └─ release.ps1
└─ docs/
```

### 4.2 模块职责

| 模块 | 职责 | 关键类 | 依赖 |
|---|---|---|---|
| Device | 枚举、指纹、状态、能力解析 | `DeviceService`、`ICapabilityResolver` | Domain、Infrastructure |
| Health | 8 项检测、评分、报告 | `HealthCheckRunner`、`ScoreCalculator` | Device、Snapshot |
| Operation | 计划、预检、事务、验证 | `OperationPlanner`、`TransactionRunner` | Broker、Snapshot |
| Snapshot | 创建、索引、差异、恢复 | `SnapshotService`、`ISnapshotStore` | Infrastructure |
| Tune | 功耗、频率、风扇、Profile | `TuningService`、`ProfileApplier` | Broker |
| Ecosystem | 覆盖、DLSS、RE 框架 | `PatchService`、`MirrorSelector` | Http、Broker |
| Rules | 加载、校验、匹配、回退 | `RulePackLoader`、`CapabilityResolver` | Domain |
| AI | 上下文、工具调用、计划生成 | `CopilotService`、`ToolRegistry` | Application |
| Update | 客户端与规则包更新 | `UpdateService`、`RollbackGuard` | Http、Updater |
| Diagnostics | 日志、dump、诊断包 | `DiagnosticsService` | Storage |

### 4.3 依赖规则

- `Domain` 不引用任何其他项目。
- `Application` 只引用 `Domain` 与 `Contracts`。
- `Infrastructure` 引用 `Domain` 与 `Contracts`，实现接口。
- `App` 引用 `Application`，不直接引用 `Infrastructure`（通过 DI 组合）。
- `Broker` 只引用 `Contracts` 与底层 Handler，不引用 UI / Application。
- 任何跨进程调用只能走 `Contracts` 中的 DTO，禁止共享可变状态。

### 4.4 关键数据流

**启动**

```
App 启动
  → 单实例检查（Local\RigKit.App）
  → 加载 config / 规则包（签名校验）
  → 探测 Broker 是否在跑；不在则按需启动
  → device.list（只读）
  → 匹配规则包，生成能力矩阵
  → 启动 Agent 连接（可选）
  → UI 进入工作台，后台开始 1s 传感器轮询
```

**传感器**

```
Timer(1s)
  → NVML: 温度/功耗/利用率/显存/频率/PState/PCIe
  → 失败降级：nvidia-smi CSV → WMI → 标记 UNKNOWN
  → 归一化（单位、精度、缺失值）
  → 环形缓冲 300 点 + SQLite 每分钟降采样
  → 阈值规则：温度/功耗/风扇/掉卡
  → UI 数值补间 + 曲线追加 + 告警事件
```

**写操作**

```
用户点击「执行解锁」
  → UI 调 op.plan
  → Preflight（10 项）
  → 返回 OperationPlan + 风险等级
  → 用户确认（高危输入卡型后四位）
  → op.execute(planId, snapshotId)
  → Broker: 创建快照 → 逐步执行 → 推送 event
  → Verifier: PnP 状态、Code 43/14、PCIe 速率、驱动版本
  → 成功：审计 + 报告；失败：自动回滚
  → UI 展示结果卡片与「查看差异」
```

**体检**

```
HealthCheckRunner
  → 识别与真伪（PCI ID / 子系统 / vBIOS 哈希）
  → PCIe 链路（当前/最大代数与宽度）
  → 显存完整性（分配 + 读写校验 + 错误计数）
  → 温度与散热（空载/负载曲线）
  → 风扇（转速响应、最低/最高）
  → 功耗（空载/负载、墙值）
  → 驱动与签名（版本、日期、Code）
  → 稳定性（3 分钟轻载 + 5 分钟满载，掉卡/降频/错误）
  → 评分（加权 100 分）+ 结论 + 建议
```

**更新**

```
启动后 10s / 每 6h
  → GET latest.json（CDN）
  → 验签 + 版本比较 + 通道过滤
  → 下载到 update\（断点续传）
  → SHA-256 校验
  → 提示用户；确认后启动 Updater
  → Updater 等待主进程退出 → 备份旧版本 → 替换 → 启动
  → 启动失败 3 次 → 自动回滚到旧版本
```

### 4.5 关键接口

```csharp
public interface IDeviceProvider
{
    Task<IReadOnlyList<Device>> EnumerateAsync(CancellationToken ct);
    Task<SensorSample> ReadSensorsAsync(DeviceId id, CancellationToken ct);
}

public interface ICapabilityResolver
{
    CapabilityMatrix Resolve(Device device, RulePack pack);
}

public interface ISnapshotStore
{
    Task<Snapshot> CreateAsync(string operation, SnapshotScope scope, CancellationToken ct);
    Task RestoreAsync(Guid snapshotId, CancellationToken ct);
    Task<IReadOnlyList<Snapshot>> ListAsync(CancellationToken ct);
}

public interface IOperationHandler
{
    string CommandId { get; }
    RiskLevel Risk { get; }
    Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct);
    Task<StepResult> VerifyAsync(StepContext ctx, CancellationToken ct);
    Task RollbackAsync(StepContext ctx, CancellationToken ct);
}
```

### 4.6 事件、日志与审计

| 类型 | 去向 | 保留 | 内容 |
|---|---|---|---|
| UI 事件 | 进程内 EventBus | 不落盘 | 进度、设备变化、告警 |
| 应用日志 | Serilog 文件 | 14 天 | 操作、异常、更新 |
| Broker 审计 | SQLite `audit_log` | 永久（可导出） | 调用方、命令、参数摘要、结果、快照 |
| 传感器 | SQLite 降采样 | 30 天 | 温度、功耗、利用率、频率 |
| 崩溃 | dump + crash.log | 30 天 | 异常栈、模块、版本 |

日志脱敏：用户名、路径中的个人目录、序列号、IP 默认打码；诊断包导出前展示将包含的文件清单。

### 4.7 错误处理与降级

- Provider 失败：NVML → nvidia-smi → WMI → `UNKNOWN`，UI 明确标注数据来源与新鲜度。
- Broker 不可用：只读功能全部可用，写操作按钮置灰并给出「启动服务失败」诊断。
- 规则包缺失：回退内置基线，UI 提示「卡型未完全支持」。
- 网络失败：更新与 AI 降级，不影响本地功能。
- 回滚失败：红色常驻横幅 + 救援盘制作入口 + 人工步骤文档。

## 5. 美术风格与界面设计规范

### 5.1 设计定位

**关键词：工业仪器、冷静、精密、可信、克制。**

参考气质：Teenage Engineering 的产品面板、Braun 的刻度与秩序、现有 HX Studio 的浅色工作台。明确避开：电竞 RGB、赛博霓虹、金币/矿机插画、过度玻璃拟态。

界面要传达的核心情绪是「这台机器现在什么状态，我下一步该做什么，出了问题怎么退」。所以数据优先、装饰退后。

### 5.2 品牌与启动

- **Logo**：六边形芯片轮廓，右下角一个缺口，内部是负形的 `M`。象征“被切掉一块能力的芯片，被重新接上”。
- **字标**：`RigKit` 使用几何无衬线，字距略收紧；中文「矿卡工具箱」用同等字重。
- **图标**：深色底 + 铜橙描边，16/32/48/256 四档手工对齐。
- **启动动画**：深色底，一道铜橙色光束从左到右扫过芯片轮廓，约 1.1s，随后 Logo 落定、窗口淡入。保留“短、稳、无进度条”的原则，不做长时间品牌动画。

### 5.3 色彩系统

品牌色选「熔铜橙」，来自 PCB 铜箔与工业警示色，和 NVIDIA 绿、AMD 红都能区分。

**浅色（默认）**

| Token | 色值 | 用途 |
|---|---|---|
| `bg.canvas` | `#F6F7F9` | 应用背景 |
| `bg.surface` | `#FFFFFF` | 卡片、面板 |
| `bg.subtle` | `#F1F3F6` | 次级区块、表头 |
| `border.default` | `#E4E7EC` | 卡片边框、分割线 |
| `text.primary` | `#16181D` | 标题、关键数值 |
| `text.secondary` | `#5B6472` | 正文、说明 |
| `text.tertiary` | `#98A2B3` | 辅助、占位 |
| `brand.600` | `#C9551F` | 主按钮、选中态 |
| `brand.500` | `#E0662E` | 强调、图表主色 |
| `brand.50` | `#FDF1EA` | 选中底、浅色标签 |
| `info` | `#2563EB` | 链接、信息提示 |
| `success` | `#12805C` | 正常、通过 |
| `warning` | `#B54708` | 注意、降级 |
| `danger` | `#D92D20` | 危险、失败、破坏性操作 |

**深色 Rig Mode**

| Token | 色值 | 用途 |
|---|---|---|
| `bg.canvas` | `#0F1115` | 应用背景 |
| `bg.surface` | `#171A1F` | 卡片 |
| `bg.subtle` | `#1E2228` | 次级区块 |
| `border.default` | `#2A2F37` | 边框 |
| `text.primary` | `#F2F4F7` | 主文字 |
| `text.secondary` | `#A7B0BE` | 正文 |
| `text.tertiary` | `#6B7686` | 辅助 |
| `brand.500` | `#FF8A4C` | 主强调 |
| `success` | `#3DDC97` | 正常 |
| `warning` | `#F5A524` | 注意 |
| `danger` | `#FF6B6B` | 危险 |

规则：

- 主按钮只用 `brand.600`，破坏性操作用 `danger` 描边，绝不把“删除/还原”做成主色实心。
- 数据颜色不表达好坏，只表达类别；好坏用状态色 + 文案，避免色盲误读。
- 深色模式不是简单反色，卡片层级靠 `surface` 与 `border` 区分，不用大面积阴影。

### 5.4 字体与排版

| 层级 | 字号/行高 | 字重 | 用途 |
|---|---|---|---|
| Display | 32 / 40 | 600 | 首页评分、关键大数字 |
| H1 | 22 / 30 | 600 | 页面标题 |
| H2 | 16 / 24 | 600 | 卡片标题 |
| Body | 13 / 20 | 400 | 正文、表格 |
| Caption | 12 / 18 | 400 | 辅助说明 |
| Micro | 11 / 16 | 500 | 标签、状态 |
| Mono | 12 / 18 | 400 | 日志、路径、命令 |

字体族：

- 中文：`Microsoft YaHei UI`（系统内置，避免字体授权与体积）。
- 拉丁：`Segoe UI Variable`。
- 数字仪表：`Bahnschrift`（Windows 10+ 内置，等宽感强，适合温度/功耗/频率）。
- 日志：`Cascadia Mono` → `Consolas` 回退。

数字规范：仪表与表格数值使用 tabular numerals，右对齐；温度保留 1 位小数，功耗 1 位，频率取整，显存按 GB 保留 1 位。

### 5.5 栅格、间距与形状

| 项 | 规格 |
|---|---|
| 基础单位 | 4px |
| 常用间距 | 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 |
| 内容栅格 | 12 列，gutter 16，页面左右 24 |
| 圆角 | 小 6 / 中 10 / 大 14 / 胶囊 999 |
| 卡片阴影 | `0 1px 2px rgba(16,24,40,.04), 0 1px 3px rgba(16,24,40,.06)` |
| 弹层阴影 | `0 12px 32px rgba(16,24,40,.16)` |
| 边框 | 1px，`border.default` |
| 焦点环 | 2px `brand.500` + 2px offset |

### 5.6 布局骨架

```
┌──────────────────────────────────────────────────────────────────────────┐
│ 44  TitleBar：Logo · 产品名 · 设备切换 · 驱动/链路/温度胶囊 · 窗口按钮      │
├──────────────┬───────────────────────────────────────────────────────────┤
│ 232 Sidebar  │ PageHeader：标题 · 副标题 · 页面级操作                       │
│              ├───────────────────────────────────────────────────────────┤
│ 工作台        │                                                           │
│ 设备          │   Content（12 列栅格）                  ┌ 320 RightRail ┐  │
│ 改造          │   卡片 / 表格 / 图表 / 向导              │ 实时传感器     │  │
│ 调优          │                                        │ 告警 / AI      │  │
│ 维护          │                                        └───────────────┘  │
│ 设置          │                                                           │
├──────────────┴───────────────────────────────────────────────────────────┤
│ 28  StatusBar：当前操作 · 数据新鲜度 · 版本 · 网络 · 时间                   │
└──────────────────────────────────────────────────────────────────────────┘
```

- 默认窗口 1360 × 860，最小 1180 × 720。
- 侧栏可折叠为 64px 图标栏；RightRail 可收起。
- 高 DPI 下使用 1.25/1.5 缩放，图标提供矢量，位图提供 2x。

### 5.7 核心组件规范

| 组件 | 规格 | 状态 |
|---|---|---|
| 主按钮 | 高 36，左右 16，圆角 8，`brand.600` 底白字 | hover 加深 8%，pressed 下沉 1px，disabled 40% |
| 次按钮 | 白底，1px 边框，`text.primary` | hover `bg.subtle` |
| 危险按钮 | 白底红边红字；仅确认弹窗内允许红色实心 | 必须配二次确认 |
| 状态胶囊 | 高 24，圆点 6px，12px 文字 | 正常 / 注意 / 异常 / 未知 四态 |
| 设备卡 | 圆角 14，左图右信息，顶部状态条 | 选中时 2px 品牌色描边 |
| 评分环 | 160px，8px 环宽，中心 32px 数字 | 0-59 红 / 60-79 琥珀 / 80-100 绿 |
| 风险横幅 | 左侧 3px 色条 + 图标 + 标题 + 说明 + 操作 | 不可关闭的高危横幅常驻 |
| 步骤条 | 水平 5 步，当前铜橙，完成打勾 | 失败步骤红色并展开原因 |
| 表格 | 表头 40，行 44，无斑马纹，hover 浅灰 | 数值右对齐，支持列固定 |
| 日志控制台 | 深色 `#0F1115`，Mono 12，等级着色 | 自动滚动可暂停，支持复制/导出 |
| 空状态 | 48px 线性图标 + 一句解释 + 一个动作 | 不写“暂无数据”就结束 |
| Toast | 右上，宽 360，4s 自动消失 | 成功/信息/警告/错误四色左边条 |

### 5.8 页面清单与关键线框

| # | 页面 | 主要内容 |
|---|---|---|
| 1 | 工作台 | 设备卡、评分、4 个 KPI、待处理事项、实时曲线、快捷动作 |
| 2 | 设备详情 | 完整资产表、PCIe 拓扑、驱动信息、能力矩阵 |
| 3 | 一键体检 | 8 项检测进度、单项结果、评分、报告导出 |
| 4 | 改造中心 | 解锁向导、型号伪装、驱动修复、双卡调度 |
| 5 | 性能调优 | 功耗墙、频率、风扇曲线、场景 Profile |
| 6 | 生态补丁 | NVIDIA App 覆盖、DLSS 运行库、RE 框架 |
| 7 | 备份与恢复 | 快照时间线、差异对比、恢复、救援盘 |
| 8 | 基准测试 | 计算/带宽/稳定性、历史对比 |
| 9 | 日志与诊断 | 实时日志、崩溃记录、诊断包导出 |
| 10 | 设置与关于 | 更新通道、隐私、语言、许可、免责声明 |

**工作台线框**

```
┌ TitleBar ────────────────────────────────────────────────────────────────┐
│ MK 矿卡工具箱    [CMP 90HX ▾]     ● 驱动 616.92   ● PCIe Gen2 x16   ● 48℃ │
├──────────┬───────────────────────────────────────────────────────────────┤
│ 工作台 ●  │ 工作台                                          [一键体检]     │
│ 设备      │ 今天 12:47 · 本机 2 张卡 · 数据新鲜度 1s                       │
│ 改造      │ ┌───────────────────────────┐ ┌────────┬────────┬────────┐  │
│ 调优      │ │  [卡图] CMP 90HX          │ │  温度   │  功耗   │  PCIe  │  │
│ 维护      │ │  GA102 · 10GB · 10DE:220D │ │ 48.2℃  │ 82.9W  │ Gen2x16│  │
│ 设置      │ │  ● 运行正常      评分 86  │ └────────┴────────┴────────┘  │
│           │ └───────────────────────────┘ ┌────────────────────────────┐ │
│           │ ┌ 待处理 2 项 ──────────────┐ │ 实时曲线  温度 / 功耗 / 负载 │ │
│           │ │ ! 尚未创建首次快照         │ │      ╱╲    ╱╲               │ │
│           │ │ ! 驱动有新版本 620.11      │ │  ╱╲╱  ╲╱╲╱  ╲              │ │
│           │ └───────────────────────────┘ └────────────────────────────┘ │
│           │ [安全解锁] [创建快照] [应用游戏 Profile] [导出报告]            │
└──────────┴───────────────────────────────────────────────────────────────┘
```

**体检结果页**：左侧 8 项检测列表（图标 + 名称 + 状态 + 耗时），右侧评分环 + 结论 + 3 条建议；底部「导出报告 / 生成分享码 / 查看原始数据」。

**解锁向导**：5 步步骤条（选择目标 → 环境预检 → 风险确认 → 执行 → 重启验证），每步只做一件事；预检失败时列出具体原因与修复动作，不允许“忽略并继续”。

### 5.9 动效

| 场景 | 时长 | 曲线 |
|---|---|---|
| 悬停 / 按下 | 120ms | ease-out |
| 页面切换 | 180ms | 淡入 + 上移 4px |
| 弹窗 / 抽屉 | 240ms | cubic-bezier(.2,.8,.2,1) |
| 数值变化 | 300ms | 数字补间，禁止滚动跳变 |
| 图表追加 | 200ms | 线性 |
| 告警出现 | 200ms | 轻微上浮 + 左边条展开 |

尊重系统「减少动态效果」设置；传感器刷新不做花哨动画，避免长时间运行眩晕。

### 5.10 图标与插图

- 图标：线性 1.5px，20/24px 两档，圆角端点，统一 24 网格。
- 设备插图：卡型剪影 + 接口/供电位置标注，用于识别与安装引导。
- 风险插图：只用简单几何与色块，不用恐吓性写实图片。
- 禁止：矿机、金币、火箭、闪电等滥用符号。

### 5.11 可访问性与高 DPI

- 正文对比度 ≥ 4.5:1，大字号 ≥ 3:1。
- 不仅靠颜色传达状态，必须配图标或文字。
- 全键盘可达，Tab 顺序与视觉顺序一致，Esc 关闭弹层。
- 最小可点击区域 32×32。
- 125% / 150% / 200% 缩放下不裁切、不重叠。

### 5.12 文案语气

- 说人话：`PCIe 链路被限制在 Gen1`，不写 `PX_CHNL_STS=0x1`。
- 动词开头：`创建快照`、`恢复出厂名称`。
- 风险说清楚：`此操作会修改引导项，失败可能导致无法开机。已为你创建快照。`
- 不用绝对化承诺：不写“永久解锁”“百分百成功”。
- 错误文案三段式：发生了什么 → 为什么 → 现在可以做什么。

### 5.13 设计验收清单

- [ ] 所有数值有单位、有精度、有数据来源。
- [ ] 所有危险操作有风险说明、二次确认、回滚路径。
- [ ] 所有页面有加载态、空状态、错误态。
- [ ] 所有颜色来自 token，无硬编码色值。
- [ ] 深浅色主题切换无对比度问题。
- [ ] 1180×720 与 200% 缩放下布局不破。

## 6. 打包、发布与分发

### 6.1 构建流水线

**本地一键构建**

```powershell
# 还原、测试、发布
dotnet test RigKit.sln -c Release
dotnet publish src/RigKit.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:PublishReadyToRun=true -p:DebugType=none
dotnet publish src/RigKit.Broker -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

**CI（GitHub Actions）**

```
push tag v* → checkout → setup-dotnet 10 → restore → build
  → unit + integration tests
  → publish app / broker / updater
  → 打包规则包 → 生成 SHA256SUMS
  → 签名（证书放 Secrets）
  → Inno Setup 编译安装包
  → 生成 latest.json + 签名
  → 上传 Release / CDN
  → 冒烟：干净 Windows Runner 安装 + 启动 + 退出
```

### 6.2 产物矩阵

| 产物 | 命名 | 体积预估 | 用途 |
|---|---|---|---|
| 安装版 | `RigKit_1.0.0_x64_Setup.exe` | 70~95 MB | 普通用户，写注册表、开始菜单 |
| 绿色版 | `RigKit_1.0.0_x64_Portable.zip` | 75~100 MB | 装机店、U 盘、免安装 |
| 主程序 | `RigKit.App.exe` | 自包含单文件 | 内部与便携 |
| 提权器 | `RigKit.Broker.exe` | 自包含单文件 | 随主程序分发 |
| 更新器 | `RigKit.Updater.exe` | 小体积 | 替换与回滚 |
| 规则包 | `rules-2026.10.1.pak` | 1~3 MB | 热更新 |
| 资源包 | `tools-nvperm-2026.10.pak` | 5~15 MB | 按需下载解锁套件 |
| 校验 | `SHA256SUMS.txt`、`latest.json.sig` | KB | 完整性与防篡改 |

### 6.3 安装器设计

Inno Setup 关键配置：

```ini
AppId={{8B1F0C2A-...}}
AppName=矿卡工具箱 RigKit
AppVersion=1.0.0
DefaultDirName={autopf}\RigKit
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=admin
OutputBaseFilename=RigKit_1.0.0_x64_Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\RigKit.App.exe
```

安装流程：

1. 欢迎页 + 许可协议 + 免责声明（必须滚动到底才能继续）。
2. 选择安装位置（默认 `Program Files\RigKit`）。
3. 附加任务：桌面快捷方式（默认开）、开机启动 Agent（默认关）、创建救援 U 盘快捷入口（默认开）。
4. 安装：释放文件 → 注册协议 → 创建开始菜单 → 可选创建计划任务（仅 Agent，用户会话）。
5. 完成页：勾选「立即运行 RigKit」。

卸载：默认保留 `%ProgramData%\RigKit\snapshots` 与 `logs`，弹窗询问是否一并删除；删除前提示「快照可用于恢复，建议先导出」。

### 6.4 代码签名（开源项目路线）

RigKit 走开源发布，不需要买商业 EV 证书。可用的“开源签名”有三条路线，作用不同，建议组合使用。

| 路线 | 性质 | 费用 | 适用性 | 局限 |
|---|---|---|---|---|
| **Certum Open Source Code Signing** | OV 级 Authenticode 证书，证书主体带 “Open Source Developer” 前缀 | 约 €69~100/年（含卡片/读卡器或云签名） | **首选**。个人可申请，需身份验证与开源项目链接；能消除“未知发布者”，SmartScreen 信誉可积累 | 每年续费；证书只覆盖开源软件 |
| **SignPath Foundation** | 免费 OV 级签名服务，证书签发给 SignPath Foundation | 免费 | 适合已发布、活跃维护、OSI 许可证、公开仓库的项目；HSM 托管、CI 集成、每次发布人工审批 | 需申请审核；证书主体不是 RigKit；硬件解锁类工具可能被认定为“绕过执行环境安全措施”，存在被拒风险 |
| **Sigstore / cosign** | 基于 OIDC 的透明签名与证明（keyless） | 免费 | 所有发布物：安装包、ZIP、规则包、SHA256SUMS；可验证“这个二进制来自这次 CI 构建” | 不提供 Windows Authenticode 信任，不消除 SmartScreen 警告 |

**推荐组合**

1. 主签名：Certum Open Source Code Signing，签 `RigKit.App.exe`、`RigKit.Broker.exe`、`RigKit.Updater.exe` 与安装包。
2. 透明签名：GitHub Actions 中用 cosign keyless 对全部发布物签名，签名与证书写入 Rekor 透明日志，发布页给出验证命令。
3. 并行申请 SignPath Foundation；若通过，可作为第二签名或替代方案（需评估证书主体与审批成本）。
4. 自签名证书只用于内部测试，绝不对外分发。

**签名流程**

```powershell
# Authenticode（Certum 证书）
signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f cert.pfx /p *** RigKit.App.exe
signtool verify /pa /v RigKit.App.exe

# Sigstore keyless（GitHub Actions OIDC）
cosign sign-blob --yes RigKit_1.0.0_x64_Setup.exe --output-signature RigKit_1.0.0_x64_Setup.exe.sig
cosign verify-blob --certificate-identity-regexp "https://github.com/<org>/<repo>/.github/workflows/release.yml@refs/tags/.*" `
  --certificate-oidc-issuer https://token.actions.githubusercontent.com `
  --signature RigKit_1.0.0_x64_Setup.exe.sig RigKit_1.0.0_x64_Setup.exe
```

**现实预期**：开源证书能解决“发布者未知”，但 SmartScreen 信誉仍需下载量积累；首版可能仍有少量警告。因此下载页必须同时提供 SHA-256、Sigstore 验证命令、VirusTotal 链接与可复现构建说明。

规则包与工具包仍使用 Ed25519 签名，与代码签名分离；客户端内置公钥，校验失败回退内置基线包。

**申请顺序**：先把仓库公开、打第一个 tag、产出可下载 Release，再申请 SignPath（要求已有 Release）；Certum 可同步申请。首个公开测试版可以先用 Sigstore + 哈希，不阻塞开发。

### 6.5 更新系统

**清单文件** `https://dl.muyaa.top/rigkit/latest.json`

```json
{
  "schema": 1,
  "channel": "stable",
  "version": "1.2.0",
  "minVersion": "1.0.0",
  "os": "win-x64",
  "url": "https://dl.muyaa.top/rigkit/1.2.0/RigKit_1.2.0_x64_Setup.exe",
  "sha256": "9f2c...",
  "size": 94371840,
  "signature": "base64-ed25519...",
  "notes": "修复 40HX 冷启动回落 Gen1；新增 50HX 风扇曲线",
  "mandatory": false,
  "publishedAt": "2026-10-19T12:00:00+08:00"
}
```

机制：

- 通道：`stable` / `beta` / `nightly`，设置页可切换，默认 stable。
- 检查：启动后 10 秒一次，之后每 6 小时；手动检查立即执行。
- 下载：断点续传，存 `%ProgramData%\RigKit\update`，校验 SHA-256 + 签名。
- 安装：静默安装 + 保留旧版本目录；`Updater` 记录启动成功标记，连续 3 次启动失败自动回滚。
- 规则包：独立检查 `packs/latest.json`，不强制重启，下次启动生效。
- 紧急撤回：CDN 清单回退到上一版本，客户端看到 `minVersion` 强制升级时给出明确提示。

### 6.6 分发渠道

| 渠道 | 作用 | 注意 |
|---|---|---|
| `muyaa.top/rigkit` 官网 | 主入口、文档、更新日志 | 已有备案与 CDN，直接复用 |
| `dl.muyaa.top` | 安装包与规则包下载 | 腾讯云 COS + CDN，开防盗链与限速 |
| GitHub Releases | 海外用户、版本归档 | 与 CI 自动发布衔接 |
| 蓝奏云 / 123 云盘 | 国内备用镜像 | 文件名带版本与 SHA-256 前 8 位 |
| 图吧 / B 站 / 贴吧 | 内容分发 | 教程 + 实测，不做硬广 |
| 装机店渠道包 | 预配置、无遥测、批量 | 独立渠道 ID，便于统计 |

分发原则：所有渠道的安装包必须同一哈希；网盘只做镜像，不承载“特供版”；不提供“破解/永久激活”类版本。

### 6.7 首次启动检查

```
Windows 版本 ≥ 10.0.19041
架构 x64
可用磁盘 ≥ 500 MB（快照与资源）
VC++ 运行库（自包含可免）
Secure Boot 状态（仅提示，不阻断）
驱动版本与签名状态
是否管理员（仅影响写操作，不影响浏览）
是否存在旧版 HX 工具箱（提示可导入配置，不自动迁移）
```

### 6.8 版本策略

- 客户端：SemVer `MAJOR.MINOR.PATCH`；MAJOR 用于规则 schema 或 IPC 不兼容，MINOR 功能，PATCH 修复。
- 规则包：日期版本 `2026.10.1`，可独立回滚。
- 资源包：`tools-<name>-<date>.pak`，按需下载。
- 兼容矩阵：客户端版本 × 规则 schema × 卡型，发布页明示。

### 6.9 发布检查清单

- [ ] 版本号、AssemblyInfo、安装器脚本同步
- [ ] 单测 + 集成测全绿
- [ ] 真机回归：90HX / 40HX / 30HX 各一台（或至少两张卡）
- [ ] 危险操作回滚演练通过
- [ ] 安装 / 卸载 / 覆盖安装 / 便携模式各一遍
- [ ] 签名验证通过（`signtool verify /pa`）
- [ ] VirusTotal 全绿或误报已申诉
- [ ] 更新清单与签名已上传，旧版本可回退
- [ ] Release Notes 与已知问题更新
- [ ] 官网下载页、网盘镜像、SHA-256 同步

### 6.10 运营指标

| 指标 | 采集 | 用途 |
|---|---|---|
| 下载→首次启动率 | 可选匿名 | 判断安装器与杀软拦截 |
| 体检完成率 | 本地统计，可选上报 | 判断流程是否过长 |
| 操作成功率 | 本地审计，可选上报 | 发现卡型兼容问题 |
| 回滚率 | 本地审计 | 评估风险与规则质量 |
| 崩溃率 | dump 聚合 | 版本质量 |
| 规则包命中率 | 本地 | 判断是否需要补卡型 |

默认全部关闭，首次启动时用一句话说明「是否帮助改进」，拒绝后不再询问。

## 7. 安全、合规与风险

### 7.1 威胁模型

| 威胁 | 场景 | 缓解 |
|---|---|---|
| 本地提权 | 恶意进程连接 Broker 管道 | 管道 ACL、调用方签名校验、命令白名单、参数 Schema |
| 命令注入 | 参数拼接进 `bcdedit` / `reg` | 禁止字符串拼接，使用 API 或参数数组，路径规范化 |
| DLL 劫持 | 安装目录可写 | 安装到 Program Files，DLL 搜索路径收紧，签名校验 |
| 规则包篡改 | 替换 json 注入恶意参数 | Ed25519 签名，公钥内置，校验失败回退 |
| 更新中间人 | 劫持下载 | HTTPS + SHA-256 + 清单签名 + 安装包签名 |
| 日志泄露 | 诊断包带用户名/序列号 | 导出前脱敏与清单确认 |
| 供应链 | 第三方库漏洞 | 锁定版本、最小依赖、定期审计 |
| 误操作 | 用户点错卡型 | 高危输入确认词、计划预览、快照强制 |

### 7.2 变砖防护五层

1. **预检**：Secure Boot、BitLocker、磁盘空间、ESP 可写性、现有引导项冲突。
2. **快照**：BCD、注册表、驱动状态、关键文件，全部带哈希。
3. **双备份**：BCD 备份到 `%ProgramData%` 与 EFI 分区各一份，带时间戳。
4. **救援盘**：一键制作可启动 U 盘，内含恢复脚本与说明。
5. **回滚**：失败自动逆序还原；回滚失败给出人工步骤与社区求助模板。

### 7.3 合规与免责

- 非官方项目，与 NVIDIA、AMD 无关联。
- 仅面向用户自有硬件的检测、修复与调优。
- 不提供加密货币挖矿、矿池、收益相关功能。
- 不绕过 DRM、反作弊、账号风控。
- 不修改驱动签名与系统 INF。
- 明确提示：解锁与调优可能影响保修、稳定性与功耗，用户自行评估。
- 开源协议：**Apache-2.0**，源码公开；第三方组件遵循各自许可，发布时附 `LICENSE` 与 `NOTICE`。
- 隐私政策：默认不上传；可选匿名数据范围逐项列出；提供一键清除本地数据。

### 7.4 风险登记表

| 风险 | 概率 | 影响 | 缓解 | 负责人 |
|---|---|---|---|---|
| 解锁失败导致设备异常 | 中 | 高 | 五层防护 + 真机演练 + 灰度发布 | 开发 |
| 杀软误报 | 高 | 中 | 开源证书 + Sigstore、提交白名单、源码公开、避免加壳 | 发布 |
| 驱动更新破坏伪装 | 高 | 中 | 启动检测驱动版本变化，提示一键修复 | 开发 |
| P 系列驱动来源与兼容性 | 高 | 中 | 不捆绑修改驱动；只做检测、版本建议、签名校验与回滚；能力标注实验性 | 开发 |
| 多卡枚举错乱 | 中 | 高 | PCI 位置为主键，禁用数组下标 | 开发 |
| 规则包错误 | 中 | 高 | 签名 + 灰度 + 回滚 + 真机用例 | 规则维护 |
| AI 误操作 | 中 | 高 | 默认只读、写操作强制确认、计划预览 | 开发 |
| .NET 自包含体积 | 中 | 低 | 压缩、按需资源、可选框架依赖版 | 发布 |
| 平台下架/限流 | 低 | 高 | 官网 + 多镜像 + 邮件列表 | 运营 |
| 周期超支 | 中 | 中 | 里程碑验收、MVP 先跑通 90HX/40HX | 项目 |

## 8. 实施计划

### 8.1 里程碑

| 里程碑 | 周期 | 范围 | 验收标准 |
|---|---|---|---|
| M0 底座 | 1.5 周 | 解决方案骨架、CI、Broker、设备枚举、UI 壳、日志、单实例 | 普通权限启动 UI，能列出本机 90HX 实时传感器 |
| M1 体检与回滚 | 2 周 | 8 项体检、评分、报告、快照/恢复、审计；CMP 与 P 系列识别 | 报告可导出；注册表/BCD 恢复演练 100% 成功 |
| M2 CMP 改造闭环 | 2.5 周 | 90HX/40HX 解锁向导、伪装、驱动重载、验证；P 系列驱动诊断 | CMP 两张卡真机通过；P 系列驱动状态可判定；失败自动回滚 |
| M3 调优与生态 | 2 周 | 功耗/风扇/Profile、覆盖/DLSS、双卡、基准；P 系列计算 Profile | Profile 切换可复现；补丁可检测可还原 |
| M4 发布 | 1 周 | 安装器、签名、更新器、规则包、文档、10 人内测 | 3 台干净机安装成功；更新与回滚验证通过 |

总计约 9~10 周（P 系列增加 0.5~1 周）；如果只做 90HX 单卡 MVP，可压缩到 5~6 周。

### 8.2 任务拆解摘要

| 阶段 | 关键任务 | 依赖 |
|---|---|---|
| M0 | 建解决方案与分层；CI；Broker 管道与白名单；NVML Provider；UI 设计系统与工作台壳 | 无 |
| M1 | 体检项逐个实现；评分模型；HTML 报告；快照存储；恢复流程；审计表；P 系列识别 | M0 |
| M2 | 90HX 解锁迁移；40HX 注册表伪装迁移；驱动重载；PnP 验证；回滚；P 系列驱动诊断 | M1 |
| M3 | 功耗/风扇 API；Profile；覆盖补丁；DLSS 镜像；双卡绑定；基准；P 系列计算 Profile | M2 |
| M4 | Inno 安装器；开源签名；Updater；规则包发布；官网下载页；内测 | M3 |

完整勾选式任务清单见同目录 `TASK.md`。

### 8.3 工作量估算

| 角色 | 投入 | 说明 |
|---|---|---|
| 客户端 / 全栈 | 1 人 × 9~10 周 | 主体开发 |
| 硬件验证 | 穿插 6~10 天 | 90HX、40HX、30HX；P 系列至少借测一张 P106-100 |
| 设计 | 1 人 × 1.5 周 | 设计系统、关键页面、图标 |
| 测试 | 1 人 × 1 周 | 回归、安装、回滚演练 |

单人 + AI 辅助可以完成，但真机验证不能省。

### 8.4 完成定义（DoD）

- 功能：按验收标准通过，包含错误态与回滚。
- 质量：单测覆盖核心逻辑 ≥ 70%，关键路径有集成测。
- 安全：写操作全部经过 Broker，无 UI 直写。
- 文档：设计、任务、更新日志、用户指南同步。
- 发布：签名、哈希、更新清单、回滚包齐全。
- 合规：`LICENSE`（Apache-2.0）、`NOTICE`、第三方许可齐全。
- 可观测：日志、审计、崩溃 dump 可用。

## 9. 已确认决策与剩余待定

### 9.1 已确认（2026-10-19）

| # | 决策项 | 结论 |
|---|---|---|
| 1 | 首版范围 | NVIDIA CMP 六款 + Pascal 矿卡 P102-100 / P104-100 / P106-100 / P106-090 |
| 2 | 产品命名 | RigKit（中文副名：矿卡工具箱） |
| 3 | UI 技术路线 | .NET 10 + WPF（MVVM），自包含单文件发布 |
| 4 | 代码签名 | 走开源路线：Certum Open Source 为主，Sigstore 透明签名为辅，并行申请 SignPath Foundation |
| 5 | AI 计费 | 双通道：内置网关每日 50,000 token + 用户可填自己的 OpenAI 兼容 Key |
| 6 | 版本策略 | 独立新版本，不背旧版兼容包袱，旧版仅作能力参考 |
| 7 | 开发环境 | .NET 10 SDK 10.0.402 已安装，WPF 模板可用 |
| 8 | 开源协议 | **Apache-2.0**（含专利授权，商业友好） |
| 9 | P 系列支持级别 | 统一标记 **Experimental**，不升 Supported / Verified |
| 10 | 网关免费额度 | 每日 **50,000 token**（输入 + 输出），北京时间 0 点重置 |
| 11 | 代码仓库 | GitHub `1922708082/rigkit`，public，默认分支 `main` |

### 9.2 剩余待定（不阻塞 M0）

| # | 事项 | 选项 | 建议 |
|---|---|---|---|
| 1 | 首发渠道 | 官网 + GitHub + 网盘 / 先网盘 | 官网 + GitHub 为主，网盘做镜像 |
| 2 | 规则包贡献 | 仅官方维护 / 接受社区 PR | 接受 PR，但必须签名审核后合入 |

P 系列真机验证不作为发布阻塞项：首版统一标 Experimental，后续借到 P106-100 / P104-100 再补真机用例与基线数据。

## 附录 A：参考项目关键文件索引

| 文件 | 用途 |
|---|---|
| `D:\app\HANDOVER.md` | 现有架构、坑点、待办 |
| `D:\app\README.md` | 功能与型号矩阵 |
| `D:\app\build_source\build.ps1` | 现有构建链 |
| `D:\app\build_source\src\NativeCore.cs` | 底层能力实现（重点参考） |
| `D:\app\build_source\src\App.cs` | 导航、绑定、刷新逻辑 |
| `D:\app\build_source\src\MainWindow.xaml` | 现有 12 页布局与样式 |
| `D:\app\build_source\docs\界面结构概览.md` | 设计规范基线 |
| `D:\app\build_source\mining_unlock_toolkit\` | 各型号解锁套件 |
| `D:\app\build_source\nvperm_esp_kit\` | EFI 解锁核心 |
| `D:\app\minehub\server.js` | 分发/签名/接口参考 |

## 附录 B：术语表

| 术语 | 含义 |
|---|---|
| CMP | NVIDIA 专用矿卡系列（Cryptocurrency Mining Processor） |
| NVPermissiveEFI | 在 UEFI 阶段放宽 PCIe 限制的 EFI 方案 |
| BCD | Windows 启动配置数据 |
| ESP | EFI 系统分区 |
| MS-Hybrid | 核显输出 + 独显渲染的混合模式 |
| PnP | 即插即用，设备管理器中的设备状态 |
| Code 43 / 14 | Windows 设备错误码，常见于驱动异常 |
| Profile | 一组调优参数的命名集合 |
| 规则包 | 描述卡型、能力、参数、已知问题的签名数据包 |
| 快照 | 操作前的系统状态备份，用于回滚 |
| Broker | 提权执行进程，只接受白名单命令 |
