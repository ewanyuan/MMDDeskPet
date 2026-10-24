# MMD Desk Pet

无需转换成 VRM。让 PMX 模型在电脑桌面上跟着 VMD 跳舞。

No VRM conversion needed. Put your PMX model straight onto the Windows desktop and have it dance to VMD.

VRM への変換は不要です。PMX モデルをそのまま Windows のデスクトップに置き、VMD に合わせて踊らせます。

<img src="docs/demo.gif" width="360" alt="MMDDeskPet demo">

Demo model: 公式MMDモデル 雛乃木まや (©雛乃木まや/KAZUYA/金子卵黄), not included in this repo. Motion: original.

## 快速开始 | Quick start | クイックスタート

1. 下载 [v0.1.1](https://github.com/ewanyuan/MMDDeskPet/releases/download/v0.1.1/MMDDeskPet-v0.1.1-win-x64.zip) 并双击 `MMDDeskPet.exe`。 Download [v0.1.1](https://github.com/ewanyuan/MMDDeskPet/releases/download/v0.1.1/MMDDeskPet-v0.1.1-win-x64.zip) and double-click `MMDDeskPet.exe`. [v0.1.1](https://github.com/ewanyuan/MMDDeskPet/releases/download/v0.1.1/MMDDeskPet-v0.1.1-win-x64.zip) をダウンロードして `MMDDeskPet.exe` をダブルクリックする。
2. 把模型放进 models 文件夹，或直接拖进窗口。 Put the model in the models folder, or drop it into the window. モデルを models フォルダに入れるか、ウィンドウにドロップする。
3. 动作放进 motions 文件夹，或把 `.vmd` 拖进窗口。 Put a motion in the motions folder, or drop a `.vmd` into the window. モーションは motions フォルダに入れるか、`.vmd` をウィンドウにドラッグする。

## 系统要求 | System requirements | 動作環境

Windows 10/11 x64，以及 Microsoft Edge WebView2 Runtime。

Windows 10/11 x64 and the Microsoft Edge WebView2 Runtime.

Windows 10/11 x64 と Microsoft Edge WebView2 Runtime。

## 素材与内容 | Assets and content | 素材と内容

除了下一节的默认模型和舞蹈，本程序不附带其他模型、动作或音乐。请只使用你有权使用的素材，并遵守原作者的条款。这是公开的全年龄版本。

Aside from the default model and dance in the next section, this program ships with no other models, motions, or music. Use assets you have rights to, and follow the original authors' terms. This is the public all-ages version.

次の節の既定モデルとダンス以外に、モデル、モーション、音楽は同梱していません。権利のある素材だけを使い、原作者の利用条件に従ってください。これは公開用の全年齢版です。

## 随包素材 | Bundled assets | 同梱素材

`assets/default` 里的文件不适用 GPL-3.0，各自带有自己的许可。

模型是 CG-CA Uka：`CG-CA Uka (c) 2023-2024 by Nagoya Institute of Technology, Moonshot R&D Goal 1 Avatar Symbiotic Society`，[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)。changes: textures resized/compressed。默认舞蹈 `mmddeskpet_groove_20s.vmd` 是 Ewan Yuan 的原创，[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。待机动作 `stand.vmd` 与模型同属 CC BY 4.0，使用上面的署名，文件本身没有改动。旁边的 models 文件夹里没有模型、也没有上次打开的模型时，程序会自动加载 Uka，并循环播放这段舞蹈。右键菜单里有一项「默认角色 Uka」。如果你放了自己的模型，仍会先打开上次使用的模型，否则按原来的规则挑选。

Files in `assets/default` are not under GPL-3.0. Each one has its own license.

The model is CG-CA Uka: `CG-CA Uka (c) 2023-2024 by Nagoya Institute of Technology, Moonshot R&D Goal 1 Avatar Symbiotic Society`, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). changes: textures resized/compressed. The default dance `mmddeskpet_groove_20s.vmd` is an original motion by Ewan Yuan, [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). The idle motion `stand.vmd` is under the same CC BY 4.0 credit as the model, and that file is unmodified. When the models folder has no model and there is no last-opened model, the app loads Uka and loops this dance. The right-click menu has one entry, “Default character Uka”. If you add your own models, the app still opens the last model first, then uses the existing pick rules.

`assets/default` のファイルは GPL-3.0 の対象外で、それぞれ独自のライセンスです。

モデルは CG-CA Uka です。`CG-CA Uka (c) 2023-2024 by Nagoya Institute of Technology, Moonshot R&D Goal 1 Avatar Symbiotic Society`。[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)。changes: textures resized/compressed。既定のダンス `mmddeskpet_groove_20s.vmd` は Ewan Yuan のオリジナルで、[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) です。待機モーション `stand.vmd` はモデルと同じ CC BY 4.0 で、上のクレジットを使い、ファイル自体は改変していません。models フォルダにモデルが無く、前回開いたモデルも無いときは、Uka を読み込み、このダンスをループ再生します。右クリックメニューには「Default character Uka」（中国語環境では「默认角色 Uka」）が一つだけ出ます。自分のモデルを置いた場合は、これまでどおり前回のモデルを優先し、無ければ既存の選択規則を使います。

## 致谢 | Credits | クレジット

画面与 MMD 播放用到这些库：

- [babylon-mmd](https://github.com/noname0310/babylon-mmd)（noname0310）读取 PMX / VMD，并提供 MMD 运行时与物理。
- [Babylon.js](https://github.com/BabylonJS/Babylon.js)（`@babylonjs/core`、`@babylonjs/loaders`）负责三维渲染。
- [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) 把画面放进 WPF 窗口。
- [esbuild](https://esbuild.github.io/) 在构建时打包运行时脚本。

Rendering and MMD playback use these libraries:

- [babylon-mmd](https://github.com/noname0310/babylon-mmd) (noname0310) loads PMX / VMD and provides the MMD runtime and physics.
- [Babylon.js](https://github.com/BabylonJS/Babylon.js) (`@babylonjs/core`, `@babylonjs/loaders`) does the 3D rendering.
- [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) hosts the view inside the WPF window.
- [esbuild](https://esbuild.github.io/) bundles the runtime script at build time.

描画と MMD の再生には次のライブラリを使っています。

- [babylon-mmd](https://github.com/noname0310/babylon-mmd)（noname0310）が PMX / VMD を読み、MMD のランタイムと物理演算を担います。
- [Babylon.js](https://github.com/BabylonJS/Babylon.js)（`@babylonjs/core`、`@babylonjs/loaders`）が 3D 描画を担います。
- [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) が WPF ウィンドウの中で画面を表示します。
- [esbuild](https://esbuild.github.io/) がビルド時にランタイムのスクリプトをまとめます。

## 版本 | Status | バージョン

当前版本 v0.1.1。[下载](https://github.com/ewanyuan/MMDDeskPet/releases/download/v0.1.1/MMDDeskPet-v0.1.1-win-x64.zip)

Current version: v0.1.1. [Download](https://github.com/ewanyuan/MMDDeskPet/releases/download/v0.1.1/MMDDeskPet-v0.1.1-win-x64.zip)

現在のバージョンは v0.1.1 です。[ダウンロード](https://github.com/ewanyuan/MMDDeskPet/releases/download/v0.1.1/MMDDeskPet-v0.1.1-win-x64.zip)

这是早期版本，已在作者自己的 Windows 电脑上测试。

This is an early version, tested on the author's own Windows PC.

初期バージョンです。作者の Windows PC で動作確認済み。

## 许可 | License | ライセンス

Copyright (C) 2026 Ewan Yuan

本程序以 GNU General Public License v3.0 only（GPL-3.0-only）授权。[LICENSE](LICENSE) 是 GNU 官网的 GPL-3.0 全文，许可正文未作改动。

This program is licensed under the GNU General Public License v3.0 only (GPL-3.0-only). [LICENSE](LICENSE) is the official GPL-3.0 text from gnu.org. The license body is unchanged.

本プログラムは GNU General Public License v3.0 only（GPL-3.0-only）でライセンスします。[LICENSE](LICENSE) は gnu.org の GPL-3.0 公式全文であり、ライセンス本文は改変していません。

商业闭源使用请联系作者获取单独授权。

For commercial closed-source use, contact the author for a separate license.

商用のクローズドソース利用については、作者に連絡して個別の許諾を取得してください。

## 从源码构建 | Build from source | ソースからビルド

构建需要 Windows 10/11 x64、.NET 10 SDK、Node.js、pnpm，以及 Microsoft Edge WebView2 Runtime。

Building requires Windows 10/11 x64, the .NET 10 SDK, Node.js, pnpm, and the Microsoft Edge WebView2 Runtime.

ビルドには Windows 10/11 x64、.NET 10 SDK、Node.js、pnpm、Microsoft Edge WebView2 Runtime が必要です。

在本目录运行：

Run from this folder:

このフォルダで実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\run-native.ps1
```

或先打便携包，再双击 `MMDDeskPet.exe`：

Or build a portable package, then double-click `MMDDeskPet.exe`:

またはポータブル版を作り、`MMDDeskPet.exe` をダブルクリックします。

```powershell
pnpm --dir .\mmd-runtime install
powershell -ExecutionPolicy Bypass -File .\tools\package-portable.ps1
```

便携包写在 `dist/portable`。里面有空的 `models`、`motions`、`music` 文件夹，不会复制本机的模型或动作，但会带上 `assets/default` 里的 Uka 和默认舞蹈。把你自己的 PMX 放进 `models`（每个模型可以单独放在子文件夹里，并带上贴图），或直接拖进窗口。右键菜单会列出找到的模型和动作，另外有一项「默认角色 Uka」。`settings.json` 里的 `libraryFolder` 可以改库的位置，默认是 exe 所在的文件夹。库里没有模型、也没有上次打开的模型时，会自动加载 Uka 并循环播放默认舞蹈。`--settings-dir <目录>` 把 `settings.json` 写到这个目录，而不是 `%APPDATA%\MMDDeskPet\settings.json`。测试时请带上它，这样不会改到本机的桌宠设置。

The portable package is written to `dist/portable`. It includes empty `models`, `motions`, and `music` folders and does not copy models or motions from the machine, and it does include Uka and the default dance from `assets/default`. Put your own PMX in `models` (each model may sit in its own subfolder with its textures), or drop it on the window. The right-click menu lists the models and motions it finds, plus one entry, “Default character Uka”. `libraryFolder` in `settings.json` can point the library somewhere else; the default is the folder that contains the exe. With no library model and no last-opened model, it loads Uka and loops the default dance. `--settings-dir <directory>` writes `settings.json` in that folder instead of `%APPDATA%\MMDDeskPet\settings.json`. Pass it from tests so they do not change the desktop pet settings on the machine.

ポータブル版は `dist/portable` に出力されます。空の `models`、`motions`、`music` フォルダがあり、手元のモデルやモーションはコピーしませんが、`assets/default` の Uka と既定ダンスは含まれます。自分の PMX は `models` に置くか（モデルごとにサブフォルダへテクスチャと一緒に置けます）、ウィンドウへドロップします。右クリックメニューに見つかったモデルとモーションが出て、加えて既定キャラクターの項目が一つあります。`settings.json` の `libraryFolder` で場所を変えられ、既定は exe のあるフォルダです。ライブラリにモデルが無く、前回のモデルも無いときは、Uka を読み込んで既定ダンスをループします。`--settings-dir <フォルダ>` を付けると `settings.json` はそのフォルダに書き込まれ、`%APPDATA%\MMDDeskPet\settings.json` は変更されません。テストではこれを指定し、手元の設定を上書きしないでください。

觉得有用就点个 Star，遇到问题欢迎提 issue。

If you find it useful, please star the repo. Issues are welcome if you run into a problem.

役に立ったら Star を付けてください。問題があれば issue をどうぞ。
