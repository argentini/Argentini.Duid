if (Test-Path ".\src\nupkg") { Remove-Item ".\src\nupkg" -Recurse -Force }
. ./clean.ps1
Set-Location src
dotnet pack --configuration Release
Set-Location ..
