# 產生多解析度應用程式圖示 Resources\app.ico (16~256px,PNG 壓縮)
# 用法:pwsh tools\make-icon.ps1
param([string]$Out = (Join-Path $PSScriptRoot "..\Resources\app.ico"),
      [string]$PngOut = (Join-Path $PSScriptRoot "..\docs\images\icon-256.png"))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    # 背景:藍色漸層圓角方塊
    $bg = RoundRect 8 8 240 240 52
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 8)), ([System.Drawing.PointF]::new(0, 248)),
        ([System.Drawing.Color]::FromArgb(255, 30, 144, 232)), ([System.Drawing.Color]::FromArgb(255, 10, 70, 160))
    $g.FillPath($grad, $bg)

    # 螢幕外框
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillPath($white, (RoundRect 40 52 176 122 14))
    # 螢幕內容
    $scr = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 64)), ([System.Drawing.PointF]::new(0, 162)),
        ([System.Drawing.Color]::FromArgb(255, 18, 52, 110)), ([System.Drawing.Color]::FromArgb(255, 8, 28, 70))
    $g.FillPath($scr, (RoundRect 52 64 152 98 6))
    # 腳架
    $g.FillRectangle($white, 114, 174, 28, 22)
    $g.FillPath($white, (RoundRect 78 194 100 16 8))

    # 雙向箭頭 (遠端連線)
    $cyan = [System.Drawing.Color]::FromArgb(255, 90, 210, 255)
    $pen = New-Object System.Drawing.Pen $cyan, 13
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(78, 96), [System.Drawing.PointF]::new(176, 96)))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(152, 78), [System.Drawing.PointF]::new(176, 96), [System.Drawing.PointF]::new(152, 114)))
    $pen2 = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 13
    $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'; $pen2.LineJoin = 'Round'
    $g.DrawLines($pen2, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(178, 134), [System.Drawing.PointF]::new(80, 134)))
    $g.DrawLines($pen2, [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(104, 116), [System.Drawing.PointF]::new(80, 134), [System.Drawing.PointF]::new(104, 152)))

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256) {
        New-Item -ItemType Directory -Force (Split-Path $PngOut) | Out-Null
        $bmp.Save($PngOut, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bmp.Dispose()
    , $ms.ToArray()
}

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $images[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $images) { $bw.Write([byte[]]$data) }
$bw.Dispose()
"Icon written: $Out"
