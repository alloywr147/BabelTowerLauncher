# GitHub 上传

源码文件上传到仓库 Code；Windows 可运行压缩包上传到 Release 的 Assets。不要把源码 ZIP 当成代码文件直接提交到仓库。

网页一次需要少于 100 个文件。桌面“1.5 干净源码上传”内分为两批：打开批次文件夹，将里面的文件和目录拖入 GitHub 上传页面，分别提交。不要拖入“第1批”或“第2批”的外层文件夹；两批中的 web 等同名目录会保持正确路径。

建议提交标题：chore: clean up 1.5 source distribution。

网页上传覆盖同名文件，不会自动删除仓库已有的旧文件。随附“旧文件清理清单.txt”列出本次移除的路径；如旧包已上传，需在仓库移除，或使用 GitHub Desktop 同步干净源码后提交删除。

个人 data、artifacts、dist、node_modules、下载的 lib 依赖、旧版本备份及缓存不应上传。
