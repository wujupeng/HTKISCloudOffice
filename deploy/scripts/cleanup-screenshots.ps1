$sambaServer = "\\192.168.x.x\share\HCOffice"
$sambaUser = "****"
$sambaPass = "****"

$existing = net use 2>$null | Select-String -SimpleMatch $sambaServer
if (-not $existing) {
    net use $sambaServer /user:$sambaUser $sambaPass 2>$null
}

$cutoffDate = (Get-Date).AddDays(-90)

Get-ChildItem -Path $sambaServer -Recurse -Filter "*.png" | Where-Object {
    $_.LastWriteTime -lt $cutoffDate
} | ForEach-Object {
    Remove-Item $_.FullName -Force
}

Get-ChildItem -Path $sambaServer -Recurse -Directory | Where-Object {
    (Get-ChildItem $_.FullName -File).Count -eq 0
} | ForEach-Object {
    Remove-Item $_.FullName -Force
}
