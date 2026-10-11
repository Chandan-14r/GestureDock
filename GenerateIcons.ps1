Add-Type -AssemblyName System.Drawing

function Create-IcoFromBitmap($bmp, $outputPath) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $ms.ToArray()
    $ms.Close()

    $fs = New-Object System.IO.FileStream($outputPath, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR
    $bw.Write([int16]0) # reserved
    $bw.Write([int16]1) # icon type
    $bw.Write([int16]1) # 1 image

    # ICONDIRENTRY
    $w = if ($bmp.Width -ge 256) { [byte]0 } else { [byte]$bmp.Width }
    $h = if ($bmp.Height -ge 256) { [byte]0 } else { [byte]$bmp.Height }
    $bw.Write($w)
    $bw.Write($h)
    $bw.Write([byte]0) # color count
    $bw.Write([byte]0) # reserved
    $bw.Write([int16]1) # color planes
    $bw.Write([int16]32) # bit depth
    $bw.Write([int32]$pngBytes.Length) # size
    $bw.Write([int32]22) # offset

    # PNG bytes
    $bw.Write($pngBytes)

    $bw.Close()
    $fs.Close()
}

function New-RoundedPath($rect, $radius) {
    $diameter = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

# --- 1. HOME ICON (Radiant Azure Blue with Modern House) ---
function Generate-HomeIcon($outPath) {
    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Soft ambient drop shadow
    $shadowRect = New-Object System.Drawing.Rectangle(20, 26, 216, 216)
    $shadowPath = New-RoundedPath $shadowRect 52
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 0, 30, 80))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # Rounded tile with Azure blue gradient
    $tileRect = New-Object System.Drawing.Rectangle(20, 20, 216, 216)
    $tilePath = New-RoundedPath $tileRect 50
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tileRect, [System.Drawing.Color]::FromArgb(0, 150, 255), [System.Drawing.Color]::FromArgb(0, 75, 200), 65)
    $g.FillPath($grad, $tilePath)

    # Subtle inner border
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140, 255, 255, 255), 3.5)
    $g.DrawPath($borderPen, $tilePath)
    $borderPen.Dispose()
    $grad.Dispose()
    $tilePath.Dispose()

    # White Home Silhouette
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    
    # Roof
    [System.Drawing.PointF[]]$pts = @(
        [System.Drawing.PointF]::new(128, 64),
        [System.Drawing.PointF]::new(60, 126),
        [System.Drawing.PointF]::new(75, 138),
        [System.Drawing.PointF]::new(128, 90),
        [System.Drawing.PointF]::new(181, 138),
        [System.Drawing.PointF]::new(196, 126)
    )
    $g.FillPolygon($whiteBrush, $pts)

    # Chimney
    $g.FillRectangle($whiteBrush, 160, 78, 18, 30)

    # House Body
    $bodyRect = New-Object System.Drawing.Rectangle(82, 122, 92, 80)
    $bodyPath = New-RoundedPath $bodyRect 8
    $g.FillPath($whiteBrush, $bodyPath)
    $bodyPath.Dispose()

    # Cutout Door
    $doorBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 85, 210))
    $doorRect = New-Object System.Drawing.Rectangle(114, 150, 28, 52)
    $doorPath = New-RoundedPath $doorRect 6
    $g.FillPath($doorBrush, $doorPath)
    $doorPath.Dispose()
    $doorBrush.Dispose()
    $whiteBrush.Dispose()

    $g.Dispose()
    Create-IcoFromBitmap $bmp $outPath
    $bmp.Dispose()
}

# --- 2. CLOSE ALL ICON (Vibrant Coral Red with Multi-Window 'X') ---
function Generate-CloseAllIcon($outPath) {
    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Soft ambient drop shadow
    $shadowRect = New-Object System.Drawing.Rectangle(20, 26, 216, 216)
    $shadowPath = New-RoundedPath $shadowRect 52
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 80, 10, 10))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # Rounded tile with Crimson/Coral gradient
    $tileRect = New-Object System.Drawing.Rectangle(20, 20, 216, 216)
    $tilePath = New-RoundedPath $tileRect 50
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tileRect, [System.Drawing.Color]::FromArgb(255, 75, 75), [System.Drawing.Color]::FromArgb(190, 20, 20), 65)
    $g.FillPath($grad, $tilePath)

    # Subtle inner border
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140, 255, 255, 255), 3.5)
    $g.DrawPath($borderPen, $tilePath)
    $borderPen.Dispose()
    $grad.Dispose()
    $tilePath.Dispose()

    # Back window outline
    $backWinRect = New-Object System.Drawing.Rectangle(62, 60, 114, 110)
    $backWinPath = New-RoundedPath $backWinRect 14
    $backPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 255, 255, 255), 4)
    $g.DrawPath($backPen, $backWinPath)
    $g.DrawLine($backPen, 62, 85, 176, 85)
    $backPen.Dispose()
    $backWinPath.Dispose()

    # Front window
    $frontWinRect = New-Object System.Drawing.Rectangle(80, 82, 114, 110)
    $frontWinPath = New-RoundedPath $frontWinRect 14
    $frontPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 255, 255, 255), 5)
    $frontFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 180, 20, 20))
    $g.FillPath($frontFill, $frontWinPath)
    $g.DrawPath($frontPen, $frontWinPath)
    $g.DrawLine($frontPen, 80, 107, 194, 107)
    $frontPen.Dispose()
    $frontFill.Dispose()
    $frontWinPath.Dispose()

    # Bold white 'X' in center
    $xPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 15)
    $xPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $xPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($xPen, 114, 128, 160, 172)
    $g.DrawLine($xPen, 160, 128, 114, 172)
    $xPen.Dispose()

    $g.Dispose()
    Create-IcoFromBitmap $bmp $outPath
    $bmp.Dispose()
}

# --- 3. MAIN GESTUREDOCK ICON (Acrylic Dock Pill with Cyan Home & Red Close) ---
function Generate-MainDockIcon($outPath) {
    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Ambient shadow
    $shadowRect = New-Object System.Drawing.Rectangle(20, 26, 216, 216)
    $shadowPath = New-RoundedPath $shadowRect 52
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 15, 20, 35))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # Deep slate acrylic tile
    $tileRect = New-Object System.Drawing.Rectangle(20, 20, 216, 216)
    $tilePath = New-RoundedPath $tileRect 50
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tileRect, [System.Drawing.Color]::FromArgb(45, 48, 62), [System.Drawing.Color]::FromArgb(18, 20, 28), 65)
    $g.FillPath($grad, $tilePath)

    # Tile border
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 255, 255, 255), 3.5)
    $g.DrawPath($borderPen, $tilePath)
    $borderPen.Dispose()
    $grad.Dispose()
    $tilePath.Dispose()

    # Floating capsule pill
    $pillRect = New-Object System.Drawing.Rectangle(44, 94, 168, 68)
    $pillPath = New-RoundedPath $pillRect 34
    $pillBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 65, 85))
    $pillBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 255, 255, 255), 3)
    $g.FillPath($pillBrush, $pillPath)
    $g.DrawPath($pillBorder, $pillPath)
    $pillBorder.Dispose()
    $pillBrush.Dispose()
    $pillPath.Dispose()

    # Left: Cyan Home Icon
    $cyanBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(76, 194, 255))
    [System.Drawing.PointF[]]$roofPts = @(
        [System.Drawing.PointF]::new(86, 110),
        [System.Drawing.PointF]::new(72, 122),
        [System.Drawing.PointF]::new(100, 122)
    )
    $g.FillPolygon($cyanBrush, $roofPts)
    $g.FillRectangle($cyanBrush, 76, 122, 20, 18)
    $cyanBrush.Dispose()

    # Divider
    $divPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 255, 255, 255), 2.5)
    $g.DrawLine($divPen, 128, 106, 128, 150)
    $divPen.Dispose()

    # Right: Coral Red 'X'
    $redPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 107, 107), 5)
    $redPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $redPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($redPen, 156, 116, 178, 138)
    $g.DrawLine($redPen, 178, 116, 156, 138)
    $redPen.Dispose()

    $g.Dispose()
    Create-IcoFromBitmap $bmp $outPath
    $bmp.Dispose()
}

# --- 4. CLEAR ALL ICON (Crimson Lightning Bolt for Instant Action) ---
function Generate-ClearAllIcon($outPath) {
    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Ambient drop shadow
    $shadowRect = New-Object System.Drawing.Rectangle(20, 26, 216, 216)
    $shadowPath = New-RoundedPath $shadowRect 52
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 80, 10, 20))
    $g.FillPath($shadowBrush, $shadowPath)
    $shadowBrush.Dispose()
    $shadowPath.Dispose()

    # Radiant Crimson Gradient Background
    $rect = New-Object System.Drawing.Rectangle(20, 20, 216, 216)
    $path = New-RoundedPath $rect 50
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 75, 85), [System.Drawing.Color]::FromArgb(200, 25, 45), 55)
    $g.FillPath($grad, $path)

    # Glass specular rim
    $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 255, 255, 255), 3.5)
    $g.DrawPath($rimPen, $path)
    $rimPen.Dispose()
    $grad.Dispose()
    $path.Dispose()

    # Lightning Bolt Polygon (Instant Clear)
    $boltBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    [System.Drawing.PointF[]]$boltPts = @(
        [System.Drawing.PointF]::new(140, 52),
        [System.Drawing.PointF]::new(88, 138),
        [System.Drawing.PointF]::new(128, 138),
        [System.Drawing.PointF]::new(112, 204),
        [System.Drawing.PointF]::new(172, 118),
        [System.Drawing.PointF]::new(132, 118)
    )
    $g.FillPolygon($boltBrush, $boltPts)
    $boltBrush.Dispose()

    $g.Dispose()
    Create-IcoFromBitmap $bmp $outPath
    $bmp.Dispose()
}

$dir = "C:\Users\crs14\.gemini\antigravity\scratch\GestureDock\icons"
Generate-HomeIcon "$dir\Home.ico"
Generate-CloseAllIcon "$dir\CloseAll.ico"
Generate-ClearAllIcon "$dir\ClearAll.ico"
Generate-MainDockIcon "$dir\GestureDock.ico"

Write-Host "SUCCESS: Generated all 4 icons in $dir"
