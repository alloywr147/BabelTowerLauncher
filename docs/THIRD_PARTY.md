# 第三方组件

- RhineLabUI：https://github.com/LBEILC/RhineLabUI，MIT。保留主题、过渡代码和 MiSans 字体样式；源码位于 web/src/vendor/rhine。光碟场景和模型已移除，等高线背景由 ContourScene.js 实现。许可位于 web/src/vendor/rhine/LICENSE 及 web/public/licenses/RhineLabUI-MIT.txt。本项目不代表原作者或游戏官方。
- Microsoft WebView2：SDK 1.0.4258.31。通过 scripts/restore-webview.ps1 从 NuGet 官方源还原；构建后许可及 Notice 位于 assets/lib。不捆绑 Evergreen Runtime。
- React / React DOM / Three.js / Phosphor Icons：许可位于 web/public/licenses；依赖版本由 web/package-lock.json 固定。
- MiSans：本地字体子集及许可位于 web/public/fonts。
- 7-Zip 26.03 x64：独立进程调用。许可、说明和对应源码位于 tools/License.txt、tools/THIRD-PARTY.txt、tools/7z2603-src.7z。
- Babel Tower：管理用户另行下载的 https://github.com/c1375rick/BabelTower 官方完整包，不捆绑 Mod、桥源码或游戏资源；许可由原项目负责。

BT 等高线图标使用 AI 辅助生成。第三方组件的许可不被启动器 MIT 许可替换。
