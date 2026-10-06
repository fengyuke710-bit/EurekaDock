# EurekaDock

EurekaDock 是适用于 Windows 11 的本地桌面 Dock。它提供透明桌面栏目、可拖动的图标轮播、快捷栏，以及可选的音乐律动显示。设置和应用列表保存在当前用户的 `%LOCALAPPDATA%\EurekaDock`；程序不需要管理员权限，也没有联网、遥测或自动更新功能。

作者：**EurekaOrette**

This repository contains the Windows source code and build scripts. Personal configuration, logs, build output, and the bundled .NET runtime are excluded.

Author: **EurekaOrette**

## 构建

需要 Windows 11 x64、.NET 10 SDK、Visual Studio 2022 Build Tools 的“使用 C++ 的桌面开发”组件和 Windows SDK。在 PowerShell 中运行：

```powershell
.\build.ps1
```

脚本构建 WPF 主程序、两个 Win32 桥接 DLL，以及便携发行包使用的启动器。结果位于 `bin`。`NuGet.Config` 禁用了在线包源；构建只使用本机安装的 SDK 和 Windows 库。

## 运行与测试

安装 Microsoft .NET 10 Windows Desktop Runtime x64 后可直接运行 `bin\EurekaDock.exe`。首次运行会创建当前用户的默认配置。`bin\EurekaDock.Launcher.exe` 是便携发行包的启动器，只有将主程序置于同级 `app` 子目录，并在同级提供 .NET 运行时的 `host`、`shared` 目录时才可单独使用。

基础探针可在独立数据目录中运行：

```powershell
.\bin\EurekaDock.exe --probe --data="C:\Temp\EurekaDock-test"
```

不要将探针的 `--data` 指向实际使用中的配置目录：部分探针会有意测试配置损坏与恢复。

## 本地系统权限

- 用户添加的图标可启动 EXE、快捷方式、文件和文件夹；右键菜单调用 Windows Shell。
- 开机启动只创建当前用户的启动快捷方式。
- “显示桌面图标”只切换 Explorer 的桌面视图标志，不删除桌面文件。
- 音乐律动读取默认播放设备的输出音频；歌曲名称与封面来自 Windows 媒体会话，均不上传。

此仓库不含个人应用列表、播放记录或任何密钥。请不要将 `%LOCALAPPDATA%\EurekaDock`、`preview-data` 或历史备份提交到仓库。

## 版本管理

源码由 Git 管理，`CHECKPOINT.md` 记录最近一次完成的功能和验证结果。
`checkpoint-before-static-reveal` 标签保留固定画面弹出改动之前的版本。
个人配置、日志、编译结果、便携运行时和开发工具缓存不提交。

后续修改完成并测试后，可运行：

```powershell
git add src README.md CHECKPOINT.md
git commit -m "Describe the change"
git push origin main
git push origin --tags
```

远程仓库使用 GitHub 私有仓库；登录由 GitHub CLI 与当前用户凭据存储管理。
