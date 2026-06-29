$taskName = "HTKIS-Screenshot"
$scriptPath = "C:\Scripts\capture-screen.ps1"

$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existing) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-WindowStyle Hidden -ExecutionPolicy Bypass -File `"$scriptPath`""
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 1)
$settings = New-ScheduledTaskSettingsSet

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description "HTKIS Cloud Office Screenshot Capture (every 1 minute)" -User "SYSTEM" -RunLevel Highest

$cleanupTaskName = "HTKIS-Screenshot-Cleanup"
$cleanupScript = "C:\Scripts\cleanup-screenshots.ps1"

$existingCleanup = Get-ScheduledTask -TaskName $cleanupTaskName -ErrorAction SilentlyContinue
if ($existingCleanup) {
    Unregister-ScheduledTask -TaskName $cleanupTaskName -Confirm:$false
}

$cleanupAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-WindowStyle Hidden -ExecutionPolicy Bypass -File `"$cleanupScript`""
$cleanupTrigger = New-ScheduledTaskTrigger -Daily -At "01:00"
$cleanupSettings = New-ScheduledTaskSettingsSet

Register-ScheduledTask -TaskName $cleanupTaskName -Action $cleanupAction -Trigger $cleanupTrigger -Settings $cleanupSettings -Description "HTKIS Screenshot Cleanup (delete files older than 90 days)" -User "SYSTEM" -RunLevel Highest

Write-Host "Scheduled tasks created successfully."
Write-Host "  - $taskName (every 1 minute)"
Write-Host "  - $cleanupTaskName (daily at 01:00)"
