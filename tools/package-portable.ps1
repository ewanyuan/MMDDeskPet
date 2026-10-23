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

默认包不含模型、动作和音乐。把 PMX 拖进窗口，或在右键菜单里打开。
The default package does not include models, motions, or music. Drop a PMX file onto the window, or open one from the right-click menu.

无人值守检查（不需要拖文件，跑完会自己退出，不会弹窗）：
MMDDeskPet.exe --model "C:\path\model.pmx" --motion "C:\path\motion.vmd" --smoke-test --exit-after 90
结果在启动目录的 smoke-test-result.json 和 smoke-test.png，日志在 mmd-runtime-self-test-trace.txt。
Unattended check (no drag-and-drop, exits by itself, no message boxes):
MMDDeskPet.exe --model "C:\path\model.pmx" --motion "C:\path\motion.vmd" --smoke-test --exit-after 90
Results are smoke-test-result.json and smoke-test.png next to the trace log mmd-runtime-self-test-trace.txt.

第三方素材仍受原始条款约束，请勿擅自再分发。
Third-party assets remain subject to their original terms. Do not redistribute them without permission.
"@
Set-Content -LiteralPath (Join-Path $OutputDirectory "使用说明.txt") -Value $packageReadme -Encoding utf8

$zipPath = "$OutputDirectory.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $OutputDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "Portable folder: $OutputDirectory"
Write-Output "Portable zip: $zipPath"
