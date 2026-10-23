param([ValidateSet('setup','acquire','train','test','all')][string]$Task='test')
$ErrorActionPreference='Stop'
$aiRoot=$PSScriptRoot
$projectRoot=Split-Path $aiRoot -Parent
$pythonAi=Join-Path $aiRoot '.venv-ai\Scripts\python.exe'
$env:PYTHONPATH=Join-Path $aiRoot 'src'
$env:PYTHONIOENCODING='utf-8'
$env:TEMP=Join-Path $projectRoot 'Logs\ai-temp'
$env:TMP=$env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
if($Task -eq 'setup'){
    $bootstrap=Join-Path $projectRoot '.venv\Scripts\python.exe'
    & $bootstrap -m venv (Join-Path $aiRoot '.venv-ai')
    if($LASTEXITCODE -ne 0){throw 'venv creation failed'}
    & $pythonAi -m pip install -r (Join-Path $aiRoot 'requirements-lock.txt')
    if($LASTEXITCODE -ne 0){throw 'dependency installation failed'}
    exit
}
if(-not(Test-Path -LiteralPath $pythonAi)){throw 'Run setup first'}
if($Task -in @('acquire','all')){
    & $pythonAi -c "from fatigue_ai.data import acquire; acquire(r'$aiRoot')"
    if($LASTEXITCODE -ne 0){throw 'Acquisition failed; see reports/acquisition.jsonl'}
}
if($Task -in @('train','all')){
    & $pythonAi -m fatigue_ai.train --root $aiRoot
    if($LASTEXITCODE -ne 0){throw 'Training/export failed'}
    & $pythonAi (Join-Path $aiRoot 'export_unity.py')
    if($LASTEXITCODE -ne 0){throw 'Unity artifact export failed'}
}
if($Task -in @('test','all')){
    & $pythonAi -m pytest (Join-Path $aiRoot 'tests') -q --basetemp (Join-Path $projectRoot 'Logs\ai-test-reproduce')
    if($LASTEXITCODE -ne 0){throw 'AI tests failed'}
}
