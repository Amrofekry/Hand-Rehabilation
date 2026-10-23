param([string]$ArduinoCli='arduino-cli')
$ErrorActionPreference='Stop'
$taskConfig=Join-Path $PSScriptRoot 'arduino-cli.yaml'
if (!(Test-Path -LiteralPath $taskConfig)) { throw 'Run firmware/setup.ps1 first' }
$taskLibrary=Join-Path $PSScriptRoot '2.3-load-cell-calibration/libraries'
foreach ($taskSketch in @('2.3-load-cell-calibration/calibration','2.4-wireless-streaming/grip_stream')) {
  $taskSource=Join-Path $PSScriptRoot $taskSketch
  $taskOutput=Join-Path $PSScriptRoot ('.build/'+(Split-Path $taskSketch -Leaf))
  & $ArduinoCli compile --fqbn 'arduino:samd:nano_33_iot' --config-file $taskConfig --libraries $taskLibrary --build-path $taskOutput $taskSource
  if ($LASTEXITCODE -ne 0) { throw "Compile failed: $taskSketch" }
}
