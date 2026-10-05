# 第三方组件与视觉来源

## 7-Zip

`tools/7z.exe` 与 `tools/7z.dll` 为随附的 7-Zip 26.03 Windows x64 文件。许可、源地址和对应完整源码见：

- `tools/License.txt`
- `tools/THIRD-PARTY.txt`
- `tools/7z2603-src.7z`

程序以独立进程调用 7-Zip。7-Zip 的许可不被本项目的 MIT 许可替换。

## Babel Tower

本项目管理用户另行下载的 [Babel Tower](https://github.com/c1375rick/BabelTower) 完整包，通过其已有接口检查桥和翻译服务。不包含其 Mod、桥源码或游戏资源。原项目的许可说明见原仓库。

## 视觉设计

界面参考以下社区设计规则，采用灰白 / 炭黑 / 信号黄、少量切角、留白和操作层级；保留原生 WinForms 控件行为：

- [ark-ui Skill](https://github.com/Brandon030722/ark-ui-skill/blob/main/SKILL.md)
- [ak-ui Skill](https://github.com/YunYouJun/ak-ui/blob/master/skills/ak-ui/SKILL.md)

图标、等高线抽象图形和布局代码均为本项目原创绘制。未复制游戏标识、角色、官方网页资源或第三方组件代码；字体使用 Windows 系统字体。
