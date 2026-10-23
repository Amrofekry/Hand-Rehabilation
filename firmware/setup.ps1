param([string]$ArduinoCli='arduino-cli')
$ErrorActionPreference='Stop'
$taskFirmware=$PSScriptRoot.Replace('\','/')
$taskConfig=Join-Path $PSScriptRoot 'arduino-cli.yaml'
@"
directories:
  data: $taskFirmware/.arduino-data
  downloads: $taskFirmware/.arduino-downloads
  user: $taskFirmware/.arduino-user
updater:
  enable_notification: false
"@ | Set-Content -LiteralPath $taskConfig
& $ArduinoCli core update-index --config-file $taskConfig
if ($LASTEXITCODE -ne 0) { throw 'Arduino index update failed' }
& $ArduinoCli core install 'arduino:samd@1.8.14' --config-file $taskConfig
if ($LASTEXITCODE -ne 0) { throw 'Arduino core installation failed' }
& $ArduinoCli lib install 'ArduinoBLE@2.1.0' 'Arduino_SpiNINA@0.0.2' 'FlashStorage@1.0.0' --config-file $taskConfig
if ($LASTEXITCODE -ne 0) { throw 'Arduino library installation failed' }
