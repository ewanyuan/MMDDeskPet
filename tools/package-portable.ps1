param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectDirectory = Split-Path -Parent $toolDirectory
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectDirectory "dist/portable"
}

$bundlePath = Join-Path $projectDirectory "web/mmd.bundle.js"
$physicsOutputPath = Join-Path $projectDirectory "web/mmd-spr.wasm"
if (-not (Test-Path -LiteralPath $bundlePath) -or -not (Test-Path -LiteralPath $physicsOutputPath)) {
    $bundleScript = Join-Path $projectDirectory "mmd-runtime/build.ps1"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $bundleScript
    if ($LASTEXITCODE -ne 0) {
        throw "The browser runtime bundle could not be built."
    }
}

$dotnet = "C:/Program Files/dotnet/dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnetCommand = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) {
        throw "The .NET SDK was not found."
    }
    $dotnet = $dotnetCommand.Source
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$projectPath = Join-Path $projectDirectory "MMDDeskPet.csproj"
& $dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputDirectory `
    --no-restore `
    --nologo `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "The self-contained desktop pet publish failed."
}

$publishedAssets = Join-Path $OutputDirectory "assets"
if (Test-Path -LiteralPath $publishedAssets) {
    Remove-Item -LiteralPath $publishedAssets -Recurse -Force
}

$defaultSource = Join-Path $projectDirectory "assets/default"
if (-not (Test-Path -LiteralPath $defaultSource)) {
    throw "The bundled default assets were not found: $defaultSource"
}
Copy-Item -LiteralPath $defaultSource -Destination (Join-Path $OutputDirectory "assets/default") -Recurse -Force

$licenseSource = Join-Path $projectDirectory "LICENSE"
if (-not (Test-Path -LiteralPath $licenseSource)) {
    throw "The repository LICENSE was not found: $licenseSource"
}
Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $OutputDirectory "LICENSE") -Force

$packageReadme = @"
MMD Desk Pet

双击 MMDDeskPet.exe 即可启动桌宠。
Double-click MMDDeskPet.exe to start.

这是便携版：不要只复制 exe，请保留整个文件夹。
This is a portable build: keep the whole folder together.

如果程序提示缺少 WebView2，请安装 Microsoft Edge WebView2 Runtime 后重试。
If the app reports that WebView2 is missing, install the Microsoft Edge WebView2 Runtime and try again.

安装后若 models 文件夹是空的，会自动加载随包的 Uka 和默认舞蹈并循环播放。右键菜单有一项「默认角色 Uka」。把自己的 PMX 放进 models 文件夹（每个模型可以单独放在子文件夹里，并带上贴图），或拖进窗口。动作放进 motions，音乐放进 music。右键菜单里的「模型」「动作」会列出扫描结果。assets/default 里的素材不使用 GPL，许可写在各自的 LICENSE.txt。
If the models folder is empty after install, the bundled Uka model and default dance load automatically and loop. The right-click menu has one entry, Default character Uka. Put your own PMX in the models folder (each model may sit in its own subfolder with its textures), or drop it onto the window. Put motions in motions and audio in music. The Models and Motions items in the right-click menu list what was scanned. Files in assets/default are not under the GPL; their licenses are in LICENSE.txt.

无人值守检查（不需要拖文件，跑完会自己退出，不会弹窗）：
MMDDeskPet.exe --model "C:\path\model.pmx" --motion "C:\path\motion.vmd" --smoke-test --exit-after 90
MMDDeskPet.exe --library "D:\path\to\folder" --smoke-test --exit-after 90
--library 指向包含 models 和 motions 的文件夹，不带 --model 时会加载排在第一的模型和动作。
结果在启动目录的 smoke-test-result.json 和 smoke-test.png，日志在 mmd-runtime-self-test-trace.txt。
Unattended check (no drag-and-drop, exits by itself, no message boxes):
MMDDeskPet.exe --model "C:\path\model.pmx" --motion "C:\path\motion.vmd" --smoke-test --exit-after 90
MMDDeskPet.exe --library "D:\path\to\folder" --smoke-test --exit-after 90
--library is the folder that contains models and motions. Without --model it loads the first model and the first motion.
Results are smoke-test-result.json and smoke-test.png next to the trace log mmd-runtime-self-test-trace.txt. libraryModels and libraryMotions in the JSON are the scan counts.

第三方素材仍受原始条款约束，请勿擅自再分发。
Third-party assets remain subject to their original terms. Do not redistribute them without permission.
"@
Set-Content -LiteralPath (Join-Path $OutputDirectory "使用说明.txt") -Value $packageReadme -Encoding utf8

$modelsReadme = @"
把 PMX 放进这个文件夹。每个模型可以单独放在子文件夹里，并带上它的贴图。
动作（VMD）放进 motions 文件夹，音乐（mp3、wav、ogg）放进 music 文件夹。

Put PMX files in this folder. Each model may live in its own subfolder together with its textures.
Put VMD motions in the motions folder, and mp3, wav, or ogg audio in the music folder.
"@
$motionsReadme = @"
把 VMD 动作文件放进这个文件夹。子文件夹也可以。

Put VMD motion files in this folder. Subfolders are fine.
"@
$musicReadme = @"
把 mp3、wav 或 ogg 音乐放进这个文件夹。

Put mp3, wav, or ogg music in this folder.
"@
foreach ($libraryFolder in @(
        @{ Name = "models"; Readme = $modelsReadme },
        @{ Name = "motions"; Readme = $motionsReadme },
        @{ Name = "music"; Readme = $musicReadme }
    )) {
    $directory = Join-Path $OutputDirectory $libraryFolder.Name
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $directory "README.txt") -Value $libraryFolder.Readme -Encoding utf8
}

$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path.TrimEnd('\', '/')
$rejected = @()
Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($outputRoot.Length).TrimStart('\', '/').Replace('\', '/')
    $allowed = $false
    if ($relative -in @("LICENSE", "使用说明.txt", "models/README.txt", "motions/README.txt", "music/README.txt")) {
        $allowed = $true
    } elseif ($relative.StartsWith("web/") -or $relative.StartsWith("assets/default/")) {
        $allowed = $true
    } elseif ($relative -notmatch "/") {
        $extension = $_.Extension.ToLowerInvariant()
        if ($extension -in @(".exe", ".dll", ".json", ".xml", ".config")) {
            $allowed = $true
        }
    } elseif ($relative -match '^[a-z]{2}(-[A-Za-z]+)?/[^/]+\.resources\.dll$') {
        $allowed = $true
    } elseif ($relative -match '^runtimes/[^/]+/native/[^/]+\.dll$') {
        $allowed = $true
    }

    if (-not $allowed) {
        $rejected += $relative
    }
}
if ($rejected.Count -gt 0) {
    throw "The package contains files outside the public allowlist:`n$($rejected -join "`n")"
}

$zipPath = "$OutputDirectory.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "Portable folder: $OutputDirectory"
Write-Output "Portable zip: $zipPath"
