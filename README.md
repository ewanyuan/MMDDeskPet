# MMD Desk Pet

Windows 桌面宠物。加载你自己选择的 PMX 模型和 VMD 动作，透明、无边框地浮在桌面上。这个仓库不附带模型、动作或音乐。

A Windows desktop pet. It loads a PMX model and VMD motion you choose, and floats them in a transparent, borderless window. This repository ships with no models, motions, or music.

## 准备 | Requirements

Windows 10/11 x64，以及 .NET 10 SDK、Node.js、pnpm、Microsoft Edge WebView2 Runtime。

Windows 10/11 x64, plus the .NET 10 SDK, Node.js, pnpm, and the Microsoft Edge WebView2 Runtime.

## 运行 | Run

在本目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\run-native.ps1
```

或打一个便携包，再双击 `MMDDeskPet.exe`：

```powershell
pnpm --dir .\mmd-runtime install
powershell -ExecutionPolicy Bypass -File .\tools\package-portable.ps1
```

便携包在 `dist/portable`。默认只含程序，不会复制本机的模型或动作。

Run `run-native.ps1` from this folder. Or build a portable package with the commands above and double-click `MMDDeskPet.exe`. The package is written to `dist/portable` and contains the program only.

## 使用 | Use

右键菜单可以打开模型或动作，也可以把 PMX / VMD 文件拖进窗口。还没有模型时，窗口会提示「拖入 PMX 开始」。

外观菜单里的成人内容开关默认关闭。

Use the right-click menu to open a model or motion, or drop a PMX / VMD file onto the window. With no model loaded, the window asks you to drop a PMX file to start.

The adult-content option in the appearance menu is off by default.
