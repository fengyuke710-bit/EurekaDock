# EurekaDock

EurekaDock 是适用于 Windows 11 的本地桌面 Dock。它提供透明桌面栏目、可拖动的图标轮播、快捷栏，以及可选的音乐律动显示。设置和应用列表保存在当前用户的 `%LOCALAPPDATA%\EurekaDock`；程序不需要管理员权限，也没有联网、遥测或自动更新功能。

作者：**EurekaOrette**

This repository contains the Windows source code and build scripts. Personal configuration, logs, build output, and the bundled .NET runtime are excluded.

Author: **EurekaOrette**

## 开源许可 / License

本项目采用 [MIT 许可证](LICENSE)开源，允许使用、修改和分发，包括商业用途；分发时须保留版权声明和许可证。第三方组件和 .NET 运行时遵循各自的许可证。

EurekaDock is open source under the [MIT License](LICENSE). Use, modification, and redistribution, including commercial use, are permitted with the copyright and license notices retained. Third-party components and the .NET runtime remain subject to their respective licenses.

## 软件图标效果 / App icon effects

设置 → **软件图标**提供两项独立选项，均默认保留原有效果，并实时自动保存：

- **点击反馈 → 弹跳（macOS 风格）**：单击启动后向上弹跳，幅度逐次减弱。横向拖动、长按拖动和右键不会触发。
- **常态显示 → 镜面反射**：在图标下方显示向屏幕外倾斜的倒影，向下逐渐模糊、渐隐；不添加背景托盘。

效果适用于 Dock 与快捷栏，继续使用现有图标大小、悬停放大和边缘渐隐设置。快捷栏启动项目后仍自动收起。选择“默认”即可分别关闭效果。反射图像预先生成并缓存；静止时停止动画计算。

In **Settings → App Icons**, choose click feedback and resting appearance independently. Both defaults preserve the existing behavior, and changes save automatically:

- **Click feedback → Bounce (macOS style)** adds diminishing upward hops after a launch click. Dragging and right-clicks do not trigger it.
- **Resting appearance → Mirror reflection** adds a reflection below the icon, widening towards the viewer and progressively blurring and fading downwards, without a background tray.

Effects apply to the Dock and Quick Panel while retaining icon size, hover zoom, and edge fading. The Quick Panel still closes after launching an item. Choose Default to disable either effect. Reflections are cached; animation processing stops when idle.

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

远程仓库为 [GitHub 公开开源仓库](https://github.com/fengyuke710-bit/EurekaDock)；推送权限由 GitHub 账号权限管理，登录由 GitHub CLI 与当前用户凭据存储管理。
