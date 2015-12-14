$RootDir = Split-Path -parent $PSCommandPath
$HealthMonitorCmd = "$RootDir\..\Tests\HealthMonitor\bin\Debug\HealthMonitor.exe"
$SrcConfigFile = "$RootDir\HealthMonitor.exe.config"
$DstConfigFile = "$RootDir\..\Tests\HealthMonitor\bin\Debug\HealthMonitor.exe.config"

Write-Host "Copying HealthMonitor config file"
Copy-Item $ConfigFile -Destination $DstConfigFile -Force

Write-Host $HealthMonitorCmd
Invoke-Expression $HealthMonitorCmd
