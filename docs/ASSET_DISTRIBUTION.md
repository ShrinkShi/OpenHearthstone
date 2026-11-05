# 素材分发与更新

`assets/` 和 `assetsresearch/` 不放入普通 Git 提交：当前素材库有数十 GB、数十万个文件，并包含超过 GitHub 单文件上限的文件。它们作为同一仓库的 Release 分卷分发；配置脚本下载并按清单校验 SHA-256。运行时包另外包含已构建客户端、卡牌数据库及默认 UI 校对。

默认运行 `Setup-OpenHearthstone.cmd` 会安装运行时、完整素材及研究缓存。`-RuntimeOnly` 只安装运行时；之后重新运行不带该参数的脚本可补齐完整素材。分卷缓存在 `work/setup-downloads/`，重新运行时会复用校验通过的文件。已有本地文件不会因解压被覆盖。

项目的源代码由 Git 跟踪。资源库更改需要制作新的 Release 分卷和校验清单，并更新 `tools/setup.ps1` 的 Release 地址；仅执行 `git push` 不会上传本地素材。发布前应核对素材、研究报告、文件体积与授权范围，不要打包 `local/` 的账号数据库、令牌或个人资料。

本仓库提供 `tools/rechunk_release.py` 将已制作的完整 7z 分卷无损切分为较小的连续分卷；`tools/upload_release_chunks.ps1` 可以将这些分卷上传到 Release 草稿。上传完成后需核对远端文件数量、大小及 SHA-256，并用 7-Zip 测试整个分卷集，再公开 Release。
