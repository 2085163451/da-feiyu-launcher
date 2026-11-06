# 一键编译：把 src 里的 C# 编成单文件 exe
# 不需要装 .NET SDK / Visual Studio，只用 Windows 自带的 csc.exe（.NET Framework 4.x）
# 用法：右键 → 使用 PowerShell 运行，或 powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$src = Join-Path $root "src"
$out = Join-Path $root "大肥鱼启动器.exe"
$icon = Join-Path $root "app.ico"
$avatar = Join-Path $root "WhaleAvatar.png"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) { throw "找不到 csc.exe，系统可能缺少 .NET Framework 4.x" }

# 源码统一存成 UTF-8 BOM，csc 才会正确读中文
Get-ChildItem (Join-Path $src "*.cs") | ForEach-Object {
    $t = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($_.FullName, $t, (New-Object System.Text.UTF8Encoding($true)))
}

# 旧程序还在跑的时候 exe 被占用，先关掉再编
$running = Get-Process | Where-Object { $_.Path -eq $out }
if ($running) { Write-Host "程序正在运行，先关掉…"; $running | Stop-Process -Force; Start-Sleep -Milliseconds 800 }

$files = Get-ChildItem (Join-Path $src "*.cs") | ForEach-Object { $_.FullName }
$cscArgs = @("/nologo", "/target:winexe", "/optimize+", "/out:$out",
          "/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll",
          "/r:System.Windows.Forms.dll", "/r:System.Management.dll")
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }
# 看板娘立绘嵌进 exe 里，程序单文件就能带着图走
if (Test-Path $avatar) { $cscArgs += "/resource:$avatar,WhaleAvatar.png" }
$cscArgs += $files

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败（退出码 $LASTEXITCODE）" }
Write-Host "已生成：$out"
Get-Item $out | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
