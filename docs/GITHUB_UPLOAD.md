# 首次上传 GitHub

## 创建仓库

仓库名可用 `BabelTowerLauncher`，可见性选 Public。若使用本项目提供的 README、.gitignore 和 LICENSE，创建时不自动生成这些文件。

## 上传源码

1. 创建成功后，在仓库页面选择“uploading an existing file”，或“Add file → Upload files”。
2. 打开解压后的源码目录，把目录**里面的内容**拖入上传页，使 README.md、LICENSE、src、assets 等位于仓库根目录。
3. 不要把源码 ZIP 当作源码上传；不要上传 dist、artifacts、个人 data、日志、API 配置或桌面快捷方式。
4. 填写提交说明，例如 `Add Babel Tower Windows launcher 1.3.1`，点击 Commit changes。
5. 仓库首页应能显示项目说明和 src 目录。缺文件时可再次上传。

## 发布软件压缩包

1. 进入仓库 Releases，点 Create a new release。
2. 新建标签 `v1.3.1`，标题填 `巴别塔启动器 1.3.1`。
3. 把桌面的 `巴别塔启动器-1.3.1-完整压缩包.zip` 拖到附件区。
4. 写出本版变化和已知限制。当前建议勾选预发布（Set as a pre-release）。
5. 发布后，朋友从 Releases 下载附件；首页的“Code → Download ZIP”下载的是源码，不是完整软件。

## 给原作者提 PR

PR 是提交给原仓库的一组具体修改。它需要从原仓库 fork 后创建分支；新建的独立仓库不能直接当成上游分支。

第一次可先准备一个小的文档 PR：在原项目 README 增加第三方启动器说明、源码和下载入口，由原作者决定是否推荐。

如果希望将完整启动器源码集成到原仓库，再准备独立模块及构建文档，明确维护范围和原项目许可要求。不要直接把 EXE 或完整软件 ZIP 塞进上游源码目录。

尚未创建或提交任何上游 PR；待仓库地址和具体贡献范围明确后再提交。
