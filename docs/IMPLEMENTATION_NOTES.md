# 1.7 实现与验证

## 来源与结构

基于本地 1.6 完整源码，并参考 c1375rick/BabelTowerLauncher 的 PR #1（head 0ae1e03276f03554d0daab79ada97611778f987c）。沿用 StartPlan、HTTPS 进程识别、设置页 URL、诊断文案及六项 BridgeHttpsTests；补充 LowLatencyChannel 与 RhineChannelUI。原作者贡献保留，不将 Mod 桥 JS 代码打包进启动器源码。

LowLatencyChannel.Ensure 独立处理缺失 sidecar；Bridge.Start 在主桥已运行的分支同样调用它。异版本端口不停止；缺少或损坏证书时降级。ChannelRuntime 只启动所选包内脚本，明确传入上游 HTTP 端口。

通道检查包括匹配进程、IPv4/IPv6 回环端口归属、PFX 有效性、当前用户/本机信任库、严格 TLS 链与名称验证、证书指纹及 health 身份。请求不跟随重定向、不使用代理，也不覆盖全局 TLS 回调。

Connectivity.ApplyChannel 以 context/revision 拒绝过期异步结果，状态 25 秒过期。界面后台每 8 秒复检；操作/上下文切换使旧证据失效。报告在操作失效前保存已有状态的只读快照，不执行网络或证书写入。

## 界面与兼容

入口固定为完整 StandardApp。删除简易 App、样式、InterfacePreferences 和旧测试；保留主题偏好。顶部、路径页、诊断页提供 Mod 网页设置入口。通过原生白名单命令打开受控本地 URL，不开放任意地址。

PackageInstaller 的新包 outgoing=translation 初始化保持不变；普通启动与修复不重置用户后续选择。原安装与自更新事务仍保留回滚、路径边界和个人数据保护。

## 验证

scripts/test.ps1 覆盖安装、恢复、诊断、在线/自身更新、启动提示、原作者测试与新增通道测试。scripts/test-ui.ps1 在屏幕外运行 WebView2，测试无简易模式、网页设置命令、快通道显示、单按钮更新、主题与小窗口布局。web 的 npm test 覆盖动画调度和鼠标跟随。

测试使用隔离夹具与模拟进程/接口，不启动真实游戏或 Mod 桥，不打开浏览器，不修改证书信任。游戏内行为见 EVENING_TEST.md。

本次本地验证结果：224 项 C# 功能检查、29 项屏幕外完整界面检查、9 项前端动画测试通过。实际游戏内快速翻译和推送仍待用户晚间验证。

## 证书按钮补充

CertificateSetup 验证所选完整包、安装目录边界和无目录联接的官方脚本。用户确认后，用固定的 Windows PowerShell 5.1 路径执行 -File … -Trust；隐藏代码窗口，保留 Windows 自身信任确认。读取 stdout/stderr 并限制日志量，不在 UI 线程等待。脚本完成后重新核对 PFX 私钥、有效期、指纹对应的 Windows 信任；脚本报成功但用户拒绝信任仍视为失败。已有有效受信任证书不重复执行。自定义证书环境变量保持不变并提示手动配置。

配置完成使旧连接证据失效，后台重新检查实际通道；证书配置成功不等同于游戏已使用快通道。Steam 仍由用户自行完整重启。新增 17 项证书流程检查（含无副作用的真实 PowerShell 假脚本），以及证书确认、取消、忙碌互斥和结果关闭的界面回归。修正结果弹窗在清空后台结果时中断关闭动画的问题。
