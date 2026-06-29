param(
    [string]$SambaServer = "\\192.168.x.x\share\HCOffice",
    [string]$SambaUser = "****",
    [string]$SambaPass = "****"
)

$existing = net use 2>$null | Select-String -SimpleMatch $SambaServer
if (-not $existing) {
    net use $SambaServer /user:$SambaUser $SambaPass 2>$null
}

$username = $env:USERNAME
$date = Get-Date -Format "yyyy-MM-dd"
$time = Get-Date -Format "HHmmss"

$outputDir = Join-Path $SambaServer "$username\$date"
$outputFile = Join-Path $outputDir "$time.png"

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [System.Windows.Forms.Screen]::PrimaryScreen
$bounds = $screen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)

$bitmap.Save($outputFile, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
