# Contributing to RigKit

## 开发环境

- Windows 10 22H2 / Windows 11 x64
- .NET 10 SDK（`dotnet --version` 应为 10.0.x）
- 无 NVIDIA 显卡时可跑单元测试；真机相关用例会标明

## 分支与提交

- 分支：`feat/<topic>`、`fix/<topic>`、`docs/<topic>`
- 提交信息遵循 Conventional Commits：
  - `feat(m0): add NVML provider`
  - `fix(broker): handle broken pipe`
  - `docs: update M0 status`
  - `test: cover provider fallback`
- 一个提交只做一件事；不要混入格式化全仓的改动

## 提交前检查

```powershell
dotnet build RigKit.slnx -c Release
dotnet test RigKit.slnx -c Release
```

涉及 UI 的改动请附 `--smoke` 截图：

```powershell
dotnet run --project src\RigKit.App -- --smoke .\docs\screenshots\home-m0.png
```

## 安全边界

- 不提交驱动文件、vBIOS、证书私钥、令牌或本机路径
- 不新增遥测；AI 上下文必须脱敏并经用户确认
- 写操作必须先有快照与回滚方案，M0/M1 阶段不开放
- 规则包与卡型数据以签名后的 JSON 为准，不在代码里硬编码长尾卡型

## PR

请使用 `.github/PULL_REQUEST_TEMPLATE.md`，写清楚变更、验证方式与风险。
