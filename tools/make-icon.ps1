# 把 ComfyUI 出的图做成：app.ico（多尺寸）+ WhaleAvatar.png（圆形头像用的脸部特写）
# 用法：powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 -Source <png> [-Dest <exe同目录>]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Dest = ''
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not $Dest) {
    # 脚本在 开发\tools\ 里，图标要写到再上一层（和 exe 放一起）
    $Dest = Split-Path -Parent $PSScriptRoot
    if (-not (Test-Path (Join-Path $Dest 'app.ico'))) { $Dest = Split-Path -Parent $Dest }
}

$Source = (Resolve-Path $Source).Path
$Dest = (Resolve-Path $Dest).Path
$icoPath = Join-Path $Dest 'app.ico'
$avatarPath = Join-Path $Dest 'WhaleAvatar.png'

$src = [System.Drawing.Image]::FromFile($Source)

# ---------------- 1. 头像特写：裁头部那一块 ----------------
# 原图 1024 见方，脸大概在中间偏上；裁 0.66 宽的正方形，圆心放在 (0.5, 0.45)
$side = [int]($src.Width * 0.66)
$x = [int]($src.Width * 0.5 - $side / 2)
$y = [int]($src.Height * 0.45 - $side / 2)
$avatar = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($avatar)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, 256, 256),
    (New-Object System.Drawing.Rectangle $x, $y, $side, $side), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$avatar.Save($avatarPath, [System.Drawing.Imaging.ImageFormat]::Png)
$avatar.Dispose()
Write-Host ("头像：$avatarPath")

# ---------------- 2. 多尺寸 ico ----------------
# 小尺寸（16/24/32）用脸部特写：全身图缩到 16 像素会糊成一团蓝
$face = [System.Drawing.Image]::FromFile($avatarPath)
$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $gg = [System.Drawing.Graphics]::FromImage($bmp)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gg.Clear([System.Drawing.Color]::Transparent)
    $art = if ($s -le 32) { [System.Drawing.Image]$face } else { [System.Drawing.Image]$src }
    $gg.DrawImage($art, (New-Object System.Drawing.Rectangle 0, 0, $s, $s))
    $gg.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @{ Size = $s; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}
$face.Dispose()
$src.Dispose()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0)                 # reserved
$bw.Write([uint16]1)                 # type = icon
$bw.Write([uint16]$frames.Count)     # count
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([byte]$dim)            # width
    $bw.Write([byte]$dim)            # height
    $bw.Write([byte]0)               # palette
    $bw.Write([byte]0)               # reserved
    $bw.Write([uint16]1)             # planes
    $bw.Write([uint16]32)            # bpp
    $bw.Write([uint32]$f.Bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $bw.Write($f.Bytes) }
$bw.Flush()
$bw.Dispose()
$fs.Dispose()

$ico = Get-Item $icoPath
Write-Host ("图标：$($ico.FullName)  " + [math]::Round($ico.Length / 1KB, 1) + " KB, " + $frames.Count + " 个尺寸")
