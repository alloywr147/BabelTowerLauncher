# BabelTowerLauncher — 巴别塔启动器 1.7

用于 [Babel Tower](https://github.com/c1375rick/BabelTower) Deadlock 聊天翻译 Mod 的 Windows 启动与更新管理器。Mod 本体由原作者维护，启动器独立维护。

1.7 仅保留完整界面：等高线动态背景、深浅色主题、启动检查、在线更新、诊断与修复。

![完整界面](docs/images/preview-main.png)

## 使用

下载 `BabelTowerLauncher-1.7-win64.zip`，完整解压后运行“巴别塔启动器.exe”，保留 assets 和 tools。需要 Windows x64、.NET Framework 4.8、Microsoft Edge WebView2 Runtime。

- 在“设置”中自动检测或选择 Deadlock 和专用 BabelTower 安装目录。
- 启动前检查所选桥、翻译接口及 Mod 文件，通过后确认启动游戏。
- 支持 Babel Tower 1.2.1：主桥启动时检查并补启动低延迟通道，停止与重启时一起处理所选版本的两种进程。
- 顶部“Mod 设置”打开所选桥的本地网页设置页；桥尚未运行时先启动并检查本地服务。
- “低延迟通道”独立显示证书、端口、进程和 HTTPS 检查结果。主桥正常与快通道可用分别显示。
- 主菜单和诊断页提供“配置证书”，调用当前 Mod 自带官方脚本，并验证证书与 Windows 信任状态。
- 更新页顶部切换 Mod / 启动器更新；“检查更新”发现新版后变为“下载并安装”。
- 安装或更新 Mod 后，发送前翻译默认“仅译文”；正常启动及修复保留后续手动选择。
- 保留一键修复、脱敏诊断报告、启动更新提醒和失败恢复。

程序包不含 Mod 本体、个人配置或证书。旧版没有低延迟脚本时继续使用原有通道。1.7 移除简易模式入口与相关代码，旧的模式记录不再影响界面，深浅色选择继续保存。

## 低延迟通道

低延迟通道采用 Mod 官方的本机 HTTPS 机制。首次使用可在主菜单或诊断页点击“配置证书”，阅读说明并点“开始配置”。Windows 若弹出安全警告，核对 BabelTower-localhost 后选择“是”；之后**完全退出并重新打开 Steam**，包括托盘，再点击“重启桥”。启动器只在你主动确认后运行所选 Mod 自带的官方脚本，不自动重启 Steam。已有有效受信任证书会直接复用。

每台电脑各自生成证书，发布包不带证书或私钥。取消信任、脚本失败、证书无效会明确提示，不会显示配置成功。若设置了自定义证书环境变量，请按 Mod 文档手动配置，按钮会保留现有配置。

“本地可用”表示启动器对所选进程、8792 端口、证书与 HTTPS 健康响应的检查通过；游戏侧还需要实际确认。未配置证书时主桥可继续使用原有通道。

## 验证范围

已提供隔离的安装、恢复、更新、通道状态和屏幕外界面测试。1.2.1 的游戏内快速翻译、网页保存后即时生效，以及 Steam 证书缓存行为，需要在实际游戏中验证。

## 升级与构建

升级前退出旧启动器，用新程序包替换程序、assets 和 tools，保留 data。也可解压到独立目录测试，原 1.6 可继续保留。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\restore-webview.ps1
cd web
npm ci
npm test
npm run build
cd ..
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test-ui.ps1
```

产物位于 dist/BabelTowerLauncher-1.7。

## 文档与贡献

- [使用与在线更新](docs/ONLINE_UPDATES.md)
- [晚间游戏测试](docs/EVENING_TEST.md)
- [已知限制](docs/KNOWN_ISSUES.md)
- [实现与验证](docs/IMPLEMENTATION_NOTES.md)
- [GitHub 上传](docs/GITHUB_UPLOAD.md)
- [第三方组件](docs/THIRD_PARTY.md)

感谢 [c1375rick 的 PR #1](https://github.com/alloywr147/BabelTowerLauncher/pull/1) 提供 1.2.1 适配、低延迟进程识别、网页设置地址及测试。本版基于该贡献补齐现有界面和独立通道检查。

启动器采用 [MIT](LICENSE)。第三方许可及 7-Zip 对应源码保留；使用 AI 辅助开发。
