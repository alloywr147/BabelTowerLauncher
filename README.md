# BabelTowerLauncher — 巴别塔启动器

<img src="assets/app-endfield.png" width="96" alt="巴别塔启动器图标" />

用于 [Babel Tower](https://github.com/c1375rick/BabelTower) Deadlock 聊天翻译 Mod 的 Windows 可视化启动与更新管理器。此项目独立维护，Mod 本体由原作者维护。

当前启动器版本为 **1.3.1**。启动器与 Mod 的版本号相互独立。

![启动界面](docs/images/preview-main.png)

## 功能

- 自动检测 Steam 游戏库、Deadlock 目录和完整的巴别塔安装包，支持手动选择。
- 启动前检查 Mod 文件与加载配置、本地桥归属和在线翻译；通过后弹出启动确认。
- 请求 Steam 使用 `-console -condebug` 启动游戏。
- 从更新页选择或拖入 ZIP / 7Z / RAR 完整包，整包替换，更新游戏里的 Babel Tower VPK。
- 显示本地桥、翻译接口与安装状态，提供重启桥并复检。
- 灰白、炭黑、信号黄的图形界面和多尺寸图标。

本地桥与在线接口检查通过，并不代表游戏内通信已经完成验证。游戏载入后仍需确认实际翻译效果。

## 使用

1. 从本仓库的 [Releases](https://github.com/alloywr147/BabelTowerLauncher/releases) 下载 `巴别塔启动器-1.3.1-完整压缩包.zip`，完整解压后运行 `巴别塔启动器.exe`。
2. 在首次设置页确认游戏目录、专用巴别塔安装目录和已有完整版本。没有已有版本时可留空，保存后导入官方 Mod 完整包。
3. 点“检查并启动游戏”，等待检查完成，再确认启动。
4. 更新 Mod 时先退出游戏，进入左侧“更新”，拖入或选择新版压缩包。

下载包不包含 Mod 本体。请从 [原作者仓库](https://github.com/c1375rick/BabelTower) 或 [GameBanana](https://gamebanana.com/mods/700107) 获取官方 Windows 完整包。

选择已有版本仅登记管理目录；已有 Mod 仍需正确安装到游戏中。安装检查失败时，可在诊断页查看原因。

## 环境与构建

- Windows x64、.NET Framework 4.8。
- 原生 WinForms；不需要 npm、浏览器运行时或额外的 NuGet 依赖。

在项目根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1
```

产物位于 `dist/BabelTowerLauncher-1.3.1/`。构建脚本复制图标、配置和随附解压工具，不复制个人数据。

隔离测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test.ps1
```

测试使用临时游戏与安装目录、模拟 HTTP 端点和隐藏窗口；不启动真实游戏或桥，不修改实际安装。测试产物位于 `artifacts/`。

## 目录

```text
src/       启动器源码与运行配置
assets/    原创图标、SVG 与 Windows ICO
scripts/   构建、测试及图标生成源码
tests/     隔离测试与解压 fixture
tools/     7-Zip 工具、完整对应源码和许可文件
docs/      限制、第三方说明与发布说明
```

## 当前限制

1. 同版本修复包暂不支持直接覆盖导入。
2. 旧包损坏时，新版更新可能被旧包完整性校验阻挡。
3. 更新后的加载配置检查尚未完全纳入提交前验证。
4. 不同启动器同时更新同一安装目录时，缺少共同的事务锁。
5. 外部修改翻译配置或 VPK 后，部分显示状态需要手动复检。
6. 固定窗口布局尚未充分验证小屏幕和高 DPI。

详细边界见 [已知问题](docs/KNOWN_ISSUES.md)。当前版本按测试版发布，请勿把隔离测试通过理解成所有实际游戏场景已经验证。

## 许可与贡献

启动器源码采用 [MIT](LICENSE)。Babel Tower Mod 的许可及维护由原项目负责。本仓库不包含 Mod 源码、游戏资源、个人配置或 API Key。

随附 7-Zip 的许可和来源见 [第三方组件](docs/THIRD_PARTY.md) 与 `tools/License.txt`。图标和界面几何图形为原创实现。

欢迎提交可复现的启动器问题；反馈时给出启动器版本、Mod 版本和脱敏后的错误记录。

本项目使用 AI 辅助编写与调试。已运行源码构建及隔离测试，真实游戏启动、端到端连接和高 DPI 的测试范围仍需持续补充。
