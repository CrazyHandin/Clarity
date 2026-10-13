param([string]$OutputPath = (Join-Path $PSScriptRoot 'ClearSpace.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconImages = @()
foreach ($iconSize in @(32, 48, 64, 128, 256)) {
    $bitmap = New-Object System.Drawing.Bitmap($iconSize, $iconSize)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $tealBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#39C5BB'))
    $darkPen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#103C3B'), ($iconSize * 0.075))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, ($iconSize * 0.065))
    $darkPen.StartCap = $darkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $whitePen.StartCap = $whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.FillEllipse($tealBrush, ($iconSize * 0.045), ($iconSize * 0.045), ($iconSize * 0.91), ($iconSize * 0.91))
    $graphics.DrawArc($darkPen, ($iconSize * 0.245), ($iconSize * 0.235), ($iconSize * 0.51), ($iconSize * 0.51), 45, 270)
    $graphics.DrawLine($whitePen, ($iconSize * 0.40), ($iconSize * 0.50), ($iconSize * 0.51), ($iconSize * 0.61))
    $graphics.DrawLine($whitePen, ($iconSize * 0.51), ($iconSize * 0.61), ($iconSize * 0.80), ($iconSize * 0.33))
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $iconImages += ,@($iconSize, $memory.ToArray())
    $memory.Dispose()
    $whitePen.Dispose()
    $darkPen.Dispose()
    $tealBrush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
$stream = [System.IO.File]::Create($OutputPath)
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$iconImages.Count)
    $offset = 6 + 16 * $iconImages.Count
    foreach ($entry in $iconImages) {
        $dimension = if ($entry[0] -eq 256) { 0 } else { $entry[0] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$entry[1].Length)
        $writer.Write([uint32]$offset)
        $offset += $entry[1].Length
    }
    foreach ($entry in $iconImages) { $writer.Write([byte[]]$entry[1]) }
} finally {
    $writer.Dispose()
    $stream.Dispose()
}
