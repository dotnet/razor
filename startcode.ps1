
# In the case the .dotnet folder exists, need to assume it's possibly where the
# dotnet sdk listed by global.json exists. Rather than parsing out global.json,
# checking for matches in that directory (non-trivial), just put it first on the
# path, enable mulit-level lookup and start code.
$dotnetPath = Join-Path (Get-Location) ".dotnet"
if (Test-Path $dotnetPath) {
  $env:DOTNET_MULTILEVEL_LOOKUP=1
  $env:PATH="$dotnetPath;$env:PATH"
}

code .