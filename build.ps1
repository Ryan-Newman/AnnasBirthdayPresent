# Rebuilds the Banana Bunch font and the birthday page.
# Needs the .NET 10 SDK. Run from this folder:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet run source/BuildBananaBunch.cs -- build
if ($LASTEXITCODE -ne 0) { throw 'Font build failed.' }

Copy-Item build/BananaBunch.ttf . -Force
$template = Get-Content source/index.template.html -Raw -Encoding UTF8
$glyphs = Get-Content build/glyphs.json -Raw -Encoding UTF8
[IO.File]::WriteAllText("$PSScriptRoot\index.html", $template.Replace('<!--FONT_JSON-->', $glyphs.Trim()), (New-Object Text.UTF8Encoding $false))

Write-Host 'Built BananaBunch.ttf and index.html. Open build/font-test.html to check the font.'
