# GitHub 发布 1.4

## 软件附件

在 alloywr147/BabelTowerLauncher 的 Releases 创建版本：

- Tag：`v1.4`，目标分支选择包含 1.4 源码的分支。
- 标题：`巴别塔启动器 1.4`。
- 附件：`BabelTowerLauncher-1.4-win64.zip`，保持该名称供软件在线更新识别。
- 说明：在线更新 Mod 与启动器、每次打开更新提醒、明暗主题切换。

附件上传完成再发布。自动更新需要 GitHub 附件提供 SHA256 digest；软件检查体积、校验值、文件版本和完整结构，不会接受 Source code 压缩包。后续使用 `v1.5`、`BabelTowerLauncher-1.5-win64.zip`，以此类推。

## 源码

使用单独的源码目录上传 src、tests、scripts、assets、tools、docs、README.md、LICENSE 和 .gitignore，保持这些项目位于仓库根目录。不要把 exe、个人 data、dist 或 artifacts 当成源码提交。1.4 的构建脚本能重新生成软件附件所需文件。
