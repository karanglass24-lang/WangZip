# WangZip app icon generator (flat vector mascot, GDI+)
# Output: resources\wangzip-icon.png (1024), resources\wangzip-icon.ico
# Usage: powershell -ExecutionPolicy Bypass -File .\resources\make-icon.ps1
param(
    [string]$OutDir = "$PSScriptRoot"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Color([int]$a, [int]$r, [int]$g, [int]$b) {
    [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $gp.AddArc($x, $y, $d, $d, 180, 90)
    $gp.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $gp.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $gp.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $gp.CloseFigure()
    $gp
}

function New-Ellipse([float]$cx, [float]$cy, [float]$rx, [float]$ry) {
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $gp.AddEllipse($cx - $rx, $cy - $ry, $rx * 2, $ry * 2)
    $gp
}

function New-Polygon([object[]]$pts) {
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $gp.StartFigure()
    $gp.AddPolygon([System.Drawing.PointF[]]$pts)
    $gp
}

$W = 1024; $H = 1024
$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

$ink    = New-Color 255 40 34 46
$badge  = New-Color 255 242 158 62    # warm orange
$gold   = New-Color 255 242 194 59    # crown / tooth
$gem    = New-Color 255 214 69 69
$coat   = New-Color 255 50 45 58      # dark pinstripe coat
$skin   = New-Color 255 252 215 168
$cream  = New-Color 255 242 234 216   # bandage / shirt
$blush  = New-Color 130 245 169 176
$hair   = New-Color 255 43 35 48

$pen14 = New-Object System.Drawing.Pen($ink, 14)
$pen14.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$pen14.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen14.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

function Fill([System.Drawing.Drawing2D.GraphicsPath]$path, [System.Drawing.Color]$color) {
    $g.FillPath((New-Object System.Drawing.SolidBrush($color)), $path)
}
function Stroke([System.Drawing.Drawing2D.GraphicsPath]$path) {
    try {
        $g.DrawPath($script:pen14, $path)
    } catch {
        Write-Host "Stroke failed: path-null=$($null -eq $path) points=$(@{ $true = -1; $false = $path.PointCount }[$null -eq $path]) pen-null=$($null -eq $script:pen14)"
        throw
    }
    $path.Dispose()
}
function Line([float]$x1, [float]$y1, [float]$x2, [float]$y2) {
    $g.DrawLine($script:pen14, $x1, $y1, $x2, $y2)
}

# --- badge (rounded square) ---
$badgePath = New-RoundedRect 40 40 944 944 212
Fill $badgePath $badge

$clipPath = New-RoundedRect 40 40 944 944 212
$g.SetClip($clipPath)

# --- coat with popped collar ---
$coatPts = @(
    [System.Drawing.PointF]::new(214, 1080)
    [System.Drawing.PointF]::new(286, 838)
    [System.Drawing.PointF]::new(346, 712)
    [System.Drawing.PointF]::new(378, 686)
    [System.Drawing.PointF]::new(512, 740)
    [System.Drawing.PointF]::new(646, 686)
    [System.Drawing.PointF]::new(678, 712)
    [System.Drawing.PointF]::new(738, 838)
    [System.Drawing.PointF]::new(810, 1080)
)
Fill (New-Polygon $coatPts) $coat

# shirt V + red tie
Fill (New-Polygon @(
    [System.Drawing.PointF]::new(456, 744)
    [System.Drawing.PointF]::new(568, 744)
    [System.Drawing.PointF]::new(512, 900)
)) $cream
$g.FillPath((New-Object System.Drawing.SolidBrush($gem)), (New-Polygon @(
    [System.Drawing.PointF]::new(534, 754)
    [System.Drawing.PointF]::new(490, 754)
    [System.Drawing.PointF]::new(498, 784)
    [System.Drawing.PointF]::new(512, 902)
    [System.Drawing.PointF]::new(526, 784)
)))
Stroke (New-Polygon $coatPts)

# --- ears ---
$earPath = New-Ellipse 282 612 24 34; Fill $earPath $skin; Stroke $earPath
$earPath = New-Ellipse 742 612 24 34; Fill $earPath $skin; Stroke $earPath

# --- face ---
$facePath = New-Ellipse 512 586 228 196
Fill $facePath $skin
Stroke $facePath

# --- hair (slicked back, hairline above brows) ---
$hairPath = New-Ellipse 512 398 244 204
Fill $hairPath $hair
Stroke $hairPath

# widow's peak (middle-parted fringe)
Fill (New-Polygon @(
    [System.Drawing.PointF]::new(468, 396)
    [System.Drawing.PointF]::new(512, 440)
    [System.Drawing.PointF]::new(556, 396)
)) $hair

# --- brows (confident, angled down toward center) ---
$pen14.Width = 15
Line 356 640 448 672
Line 668 640 576 672
$pen14.Width = 14

# --- eyes (round, with glints) ---
$eye = New-Ellipse 398 700 17 17; Fill $eye $ink
$eye = New-Ellipse 626 700 17 17; Fill $eye $ink
$glint = New-Ellipse 393 694 6 6; Fill $glint (New-Color 255 255 255 255)
$glint = New-Ellipse 621 694 6 6; Fill $glint (New-Color 255 255 255 255)

# --- blush ---
$cheek = New-Ellipse 352 726 32 20; Fill $cheek $blush
$cheek = New-Ellipse 672 726 32 20; Fill $cheek $blush

# --- smirk + gold tooth ---
$smile = New-Object System.Drawing.Drawing2D.GraphicsPath
$smile.AddBezier(
    [System.Drawing.PointF]::new(468, 746),
    [System.Drawing.PointF]::new(532, 776),
    [System.Drawing.PointF]::new(560, 740),
    [System.Drawing.PointF]::new(586, 728)
)
$g.DrawPath((New-Object System.Drawing.Pen($ink, 12)), $smile)
$smile.Dispose()
$tooth = New-RoundedRect 540 726 24 22 6
Fill $tooth $gold

# --- bandage on jaw (taped, cute touch) ---
$g.TranslateTransform(426, 772); $g.RotateTransform(-18)
$band = New-RoundedRect -46 -19 92 38 8
Fill $band $cream
$g.DrawPath((New-Object System.Drawing.Pen($ink, 8)), $band)
$band.Dispose()
$g.ResetTransform()

# --- crown (tilted) ---
$g.TranslateTransform(512, 232); $g.RotateTransform(-8); $g.TranslateTransform(-512, -232)
Fill (New-Polygon @(
    [System.Drawing.PointF]::new(414, 240)
    [System.Drawing.PointF]::new(414, 126)
    [System.Drawing.PointF]::new(470, 194)
    [System.Drawing.PointF]::new(512, 120)
    [System.Drawing.PointF]::new(554, 194)
    [System.Drawing.PointF]::new(610, 126)
    [System.Drawing.PointF]::new(610, 240)
)) $gold
Stroke (New-Polygon @(
    [System.Drawing.PointF]::new(414, 240)
    [System.Drawing.PointF]::new(414, 126)
    [System.Drawing.PointF]::new(470, 194)
    [System.Drawing.PointF]::new(512, 120)
    [System.Drawing.PointF]::new(554, 194)
    [System.Drawing.PointF]::new(610, 126)
    [System.Drawing.PointF]::new(610, 240)
))
$gemCircle = New-Ellipse 512 220 13 13
$g.FillPath((New-Object System.Drawing.SolidBrush($gem)), $gemCircle)
$gemCircle.Dispose()
$g.ResetTransform()

# --- badge outline on top ---
$g.ResetClip()
$outlinePath = New-RoundedRect 40 40 944 944 212
$g.DrawPath($pen14, $outlinePath)
$outlinePath.Dispose()

$g.Dispose()
$outPng = Join-Path $OutDir 'wangzip-icon.png'
$bmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)

# --- build multi-size ICO ---
function Resize([System.Drawing.Bitmap]$src, [int]$size) {
    $dst = New-Object System.Drawing.Bitmap($size, $size)
    $dg = [System.Drawing.Graphics]::FromImage($dst)
    $dg.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $dg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $dg.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $size, $size)))
    $dg.Dispose()
    $dst
}

function Render-BmpEntry([System.Drawing.Bitmap]$src, [int]$size) {
    $resized = Resize $src $size
    $bytes = $size * $size * 4
    $stride = [Math]::Ceiling($size / 32) * 4
    $maskBytes = $stride * $size

    # BITMAPINFOHEADER (height doubled: xor mask included)
    $header = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($header)
    $bw.Write([uint32]40); $bw.Write([int32]$size); $bw.Write([int32]($size * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]($bytes + $maskBytes))
    1..4 | ForEach-Object { $bw.Write([uint32]0) }
    $data = New-Object System.IO.MemoryStream -ArgumentList (,([byte[]]::new($bytes)))
    $dw = [System.IO.BinaryWriter]::new($data)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
    $pi = $resized.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $row = New-Object byte[] $bytes
    [System.Runtime.InteropServices.Marshal]::Copy($pi.Scan0, $row, 0, $bytes)
    $resized.UnlockBits($pi)
    $resized.Dispose()
    for ($y = $size - 1; $y -ge 0; $y--) {
        $dw.Write($row, $y * $size * 4, $size * 4)
    }
    $dw.Flush()
    $bw.Write($data.ToArray())
    $bw.Write([byte[]](New-Object byte[] $maskBytes))   # AND mask (opaque)
    $bw.Flush()
    $header.ToArray()
}

function Render-PngEntry([System.Drawing.Bitmap]$src, [int]$size) {
    $resized = Resize $src $size
    $ms = [System.IO.MemoryStream]::new()
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()
    $ms.ToArray()
}

$sizes = @(256, 128, 64, 48, 32, 24, 16)
$entries = @()
foreach ($s in $sizes) {
    if ($s -ge 128) { $entries += ,@($s, (Render-PngEntry $bmp $s)) }
    else            { $entries += ,@($s, (Render-BmpEntry $bmp $s)) }
}

$ico = [System.IO.MemoryStream]::new()
$bw = [System.IO.BinaryWriter]::new($ico)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $size = $e[0]; $imgBytes = $e[1]
    $bw.Write([byte]($size % 256))   # width (0 = 256)
    $bw.Write([byte]($size % 256))   # height
    $bw.Write([byte]0)               # palette size
    $bw.Write([byte]0)               # reserved
    $bw.Write([uint16]1)             # planes
    $bw.Write([uint16]32)            # bpp
    $bw.Write([uint32]$imgBytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $imgBytes.Length
}
foreach ($e in $entries) { $bw.Write([byte[]]$e[1]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $OutDir 'wangzip-icon.ico'), $ico.ToArray())

$bmp.Dispose()
"PNG: $outPng ($([math]::Round((Get-Item $outPng).Length/1KB)) KB)"
"ICO: $(Join-Path $OutDir 'wangzip-icon.ico') ($([math]::Round((Get-Item (Join-Path $OutDir 'wangzip-icon.ico')).Length/1KB)) KB)"
