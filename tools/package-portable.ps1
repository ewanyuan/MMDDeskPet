param(
    [string]$OutputDirectory = "",
    [switch]$IncludeLocalPrivateAssets
)

$ErrorActionPreference = "Stop"

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectDirectory = Split-Path -Parent $toolDirectory
$repositoryDirectory = Split-Path -Parent $projectDirectory
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

$packageAssetsDirectory = Join-Path $OutputDirectory "assets"
$creditsSource = Join-Path $projectDirectory "assets/motions/MOTION-CREDITS.md"
$creditsDestination = Join-Path $packageAssetsDirectory "motions/MOTION-CREDITS.md"
New-Item -ItemType Directory -Path (Split-Path -Parent $creditsDestination) -Force | Out-Null
Copy-Item -LiteralPath $creditsSource -Destination $creditsDestination -Force

# Default packaging copies no PMX, VMD, music, or textures.
# -IncludeLocalPrivateAssets is opt-in and reads a local private model folder
# that is not part of this repository.
if ($IncludeLocalPrivateAssets) {
    $modelsSource = Join-Path $repositoryDirectory "HimeMikotoDesktop/assets/Hime_&_Mikoto"
    $modelsDestination = Join-Path $packageAssetsDirectory "Hime_&_Mikoto"
    if (-not (Test-Path -LiteralPath $modelsSource)) {
        throw "The local model directory was not found: $modelsSource"
    }
    Copy-Item -LiteralPath $modelsSource -Destination $modelsDestination -Recurse -Force

    $motionsSource = Join-Path $projectDirectory "assets/motions"
    $motionsDestination = Join-Path $packageAssetsDirectory "motions"
    New-Item -ItemType Directory -Path $motionsDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $motionsSource -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $motionsDestination $_.Name) -Recurse -Force
    }

    $musicSource = Join-Path $projectDirectory "assets/music"
    if (Test-Path -LiteralPath $musicSource) {
        Copy-Item -LiteralPath $musicSource -Destination (Join-Path $packageAssetsDirectory "music") -Recurse -Force
    }
}

if (-not $IncludeLocalPrivateAssets) {
    $forbidden = Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File |
        Where-Object { $_.Extension.ToLowerInvariant() -in @(".pmx", ".pmd", ".vmd", ".vpd", ".tga", ".dds", ".spa", ".sph", ".bmp") }
    if ($forbidden) {
        $names = $forbidden | ForEach-Object { $_.FullName }
        throw "Refusing to pack third-party model, motion, or texture files without -IncludeLocalPrivateAssets:`n$($names -join "`n")"
    }
}

$packageReadme = @"
MMD Desk Pet

双击 MMDDeskPet.exe 即可启动桌宠。
Double-click MMDDeskPet.exe to start.

这是便携版：不要只复制 exe，请保留整个文件夹。
This is a portable build: keep the whole folder together.

如果程序提示缺少 WebView2，请安装 Microsoft Edge WebView2 Runtime 后重试。
If the app reports that WebView2 is missing, install the Microsoft Edge WebView2 Runtime and try again.

默认包不含模型、动作和音乐。把 PMX 放进 models 文件夹（每个模型可以单独放在子文件夹里，并带上贴图），或拖进窗口。动作放进 motions，音乐放进 music。右键菜单里的「模型」「动作」会列出扫描结果。
The default package does not include models, motions, or music. Put a PMX in the models folder (each model may sit in its own subfolder with its textures), or drop it onto the window. Put motions in motions and audio in music. The Models and Motions items in the right-click menu list what was scanned.

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

$zipPath = "$OutputDirectory.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "Portable folder: $OutputDirectory"
Write-Output "Portable zip: $zipPath"
