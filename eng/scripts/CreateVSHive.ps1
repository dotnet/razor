#requires -version 5

param(
  [Parameter(Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string]
  $rootSuffix,

  [Parameter(Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string]
  $devenvExePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 1

$success=$false
for($i=0; $i -le 3; $i++)
{
  Write-Host "Searching for 'RoslynDev' under $env:LocalAppData\Microsoft"
  $roslynDevPaths = Get-ChildItem -Path "$env:LocalAppData\Microsoft" -Recurse -Filter *RoslynDev* -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
  if ($roslynDevPaths) {
    Write-Host "Found the following 'RoslynDev' paths:"
    $roslynDevPaths | ForEach-Object { Write-Host $_ }
  } else {
    Write-Host "'RoslynDev' not found under $env:LocalAppData\Microsoft"
  }

  & $devenvExePath /rootsuffix $rootSuffix /updateConfiguration
  if(Test-Path -Path $env:LocalAppData\Microsoft\VisualStudio\18.0*RoslynDev)
  {
    Write-Host "The hive 'RoslynDev' exists"
    $success=$true
    break
  }
}

if($success -eq $false){
  Write-Host "Searching for 'devenv.exe' under C:\ ..."
  $devenvPaths = Get-ChildItem -Path C:\ -Filter devenv.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
  if ($devenvPaths) {
    Write-Host "Found the following 'devenv.exe' paths:"
    $devenvPaths | ForEach-Object { Write-Host $_ }
  } else {
    Write-Host "'devenv.exe' not found under C:\"
  }

  throw "Failed to create hive"
}

$vsDir = Split-Path -Parent $devenvExePath

$vsRegEdit = Join-Path $vsDir 'VsRegEdit.exe'

&$vsRegEdit set "$vsDir" RoslynDev HKLM "Profile" DisableFirstLaunchDialog dword 1

Write-Host "-- VS Info --"
$isolationIni = Join-Path $vsDir 'devenv.isolation.ini'
Get-Content $isolationIni | Write-Host
Write-Host "-- /VS Info --"
