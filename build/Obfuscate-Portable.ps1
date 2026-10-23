param(
    [Parameter(Mandatory=$true)][string]$InputDirectory,
    [Parameter(Mandatory=$true)][string]$WorkDirectory
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name SnapBerry -ErrorAction SilentlyContinue) {
    throw 'SnapBerry is running. Exit SnapBerry through the tray menu before publishing.'
}
$inputPath = [IO.Path]::GetFullPath($InputDirectory)
$workPath = [IO.Path]::GetFullPath($WorkDirectory)
$sourcePath = Join-Path $workPath 'input'
$outputPath = Join-Path $workPath 'output'
New-Item -ItemType Directory -Force -Path $sourcePath, $outputPath | Out-Null
foreach ($name in @('SnapBerry.dll', 'SnapBerry.Core.dll')) {
    Copy-Item -LiteralPath (Join-Path $inputPath $name) -Destination $sourcePath -Force
}
# 경로의 공백과 XML 특수 문자는 XML API로 이스케이프한다.
$config = New-Object System.Xml.XmlDocument
$config.Load((Join-Path $PSScriptRoot 'obfuscar.xml'))
$config.SelectSingleNode('/Obfuscator/Var[@name="InPath"]').SetAttribute('value', $sourcePath)
$config.SelectSingleNode('/Obfuscator/Var[@name="OutPath"]').SetAttribute('value', $outputPath)
$config.SelectSingleNode('/Obfuscator/AssemblySearchPath').SetAttribute('path', $inputPath)
foreach ($module in $config.SelectNodes('/Obfuscator/Module')) {
    $module.SetAttribute('file', (Join-Path $sourcePath $module.GetAttribute('file')))
}
$configPath = Join-Path $workPath 'obfuscar.xml'
$config.Save($configPath)
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Obfuscar tool restore failed.' }
    & dotnet tool run obfuscar.console $configPath
    if ($LASTEXITCODE -ne 0) { throw 'Obfuscar failed. Publishing was stopped.' }
} finally { Pop-Location }
foreach ($name in @('SnapBerry.dll', 'SnapBerry.Core.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $outputPath $name))) { throw "Missing obfuscated assembly: $name" }
}
