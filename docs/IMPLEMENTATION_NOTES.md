# 1.5 构建与验证

当前界面使用 React、Three.js 和本地 WebView2。ContourScene.js 提供 GPU 等高线背景，theme-ui、theme.css 和 ui-transitions 保留莱茵的主题与过渡实现；没有加载光碟模型。MiSans 字体子集由本地资源提供。

RhineShell.cs 绑定原生功能，仅允许本地指定来源调用白名单命令。RepairService.cs 负责修复事务，ReportExport.cs 导出脱敏摘要。更新、桥检测、配置和安装继续使用现有 C# 服务；编译依赖的 WinForms 备用界面仍保留。

## 验证入口

- scripts/test.ps1：安装与回滚、目录发现、压缩包导入、诊断、连接状态、在线更新、自更新、启动更新及修复。
- scripts/test-webhost.ps1：当前 WebView2 界面的原生集成验证。
- scripts/test-settings.ps1：设置页面切换和滚动条占位回归验证。
- web 中执行 npm test：动画调度、鼠标缓动及当前页面与背景边界回归检查。

测试使用隔离夹具、模拟服务和屏幕外窗口，不启动真实 Steam、游戏或所选 Mod 桥。真实游戏内翻译效果仍需实际验证。

源码仅保留构建文件、当前前端模块、功能测试、许可及字体。旧图标生成与预览脚本、旧 WinForms 外观测试、未引用的光碟代码与模型、历史截图和备份已移除。构建产物、下载依赖及个人配置不随源码发布。
