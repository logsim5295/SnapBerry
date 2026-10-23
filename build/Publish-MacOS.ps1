param(
    [string]$OutputDirectory,
    [ValidateSet('osx-arm64','osx-x64')]
    [string[]]$RuntimeIdentifier=@('osx-arm64','osx-x64')
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $repo 'dist/macos' }
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
Add-Type -AssemblyName System.IO.Compression

function Write-BigEndian([IO.BinaryWriter]$writer,[int]$value) {
    $bytes=[BitConverter]::GetBytes($value)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($bytes) }
    $writer.Write($bytes)
}
foreach ($rid in $RuntimeIdentifier) {
    # 매번 별도 staging을 사용해 이전 게시의 파일이 새 번들에 섞이지 않게 한다.
    $stage=Join-Path $repo ('artifacts/macos-publish/'+$rid+'/'+[Guid]::NewGuid().ToString('N'))
    $app=Join-Path $stage 'SnapBerry.app'
    $macos=Join-Path $app 'Contents/MacOS'
    $resources=Join-Path $app 'Contents/Resources'
    New-Item -ItemType Directory -Force -Path $macos,$resources | Out-Null
    & dotnet publish (Join-Path $repo 'src/SnapBerry.Desktop/SnapBerry.Desktop.csproj') -c Release -r $rid --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false --disable-build-servers -m:1 -p:UseSharedCompilation=false -o $macos
    if ($LASTEXITCODE -ne 0) { throw "$rid 게시 실패" }
    Copy-Item -LiteralPath (Join-Path $repo 'src/SnapBerry.Desktop/Packaging/Info.plist') -Destination (Join-Path $app 'Contents/Info.plist')
    Copy-Item -LiteralPath (Join-Path $repo 'build/Finalize-MacOS.sh') -Destination (Join-Path $stage 'Finalize-MacOS.sh')
    Copy-Item -LiteralPath (Join-Path $repo 'docs/macos.md') -Destination (Join-Path $stage 'README.md')
    # 기존 ICO의 256px PNG를 변형 없이 ICNS 컨테이너에 넣는다.
    $ico=[IO.File]::ReadAllBytes((Join-Path $repo 'src/SnapBerry.App/Assets/App.ico'))
    $png=$null
    for ($i=0;$i -lt [BitConverter]::ToUInt16($ico,4);$i++) {
        $entryOffset=6+16*$i
        if ($ico[$entryOffset] -eq 0 -and $ico[$entryOffset+1] -eq 0) {
            $start=[BitConverter]::ToUInt32($ico,$entryOffset+12);$length=[BitConverter]::ToUInt32($ico,$entryOffset+8)
            $png=[byte[]]::new($length);[Array]::Copy($ico,$start,$png,0,$length);break
        }
    }
    if ($null -eq $png -or [BitConverter]::ToString($png,0,8) -ne '89-50-4E-47-0D-0A-1A-0A') { throw '256px PNG icon is missing.' }
    $iconType='ic08'
    $iconStream=[IO.File]::Create((Join-Path $resources 'App.icns'))
    $iconWriter=[IO.BinaryWriter]::new($iconStream)
    try {
        $iconWriter.Write([Text.Encoding]::ASCII.GetBytes('icns'));Write-BigEndian $iconWriter ($png.Length+16)
        $iconWriter.Write([Text.Encoding]::ASCII.GetBytes($iconType));Write-BigEndian $iconWriter ($png.Length+8);$iconWriter.Write($png)
    } finally { $iconWriter.Dispose() }
    foreach ($name in @('SnapBerry.Desktop','SnapBerry.Desktop.dll','SnapBerry.Desktop.runtimeconfig.json','libAvaloniaNative.dylib','libSkiaSharp.dylib','libhostfxr.dylib')) {
        if (-not (Test-Path -LiteralPath (Join-Path $macos $name))) { throw "번들 파일 누락: $rid/$name" }
    }
    $header=[IO.File]::ReadAllBytes((Join-Path $macos 'SnapBerry.Desktop'))
    if ([BitConverter]::ToString($header,0,4) -ne 'CF-FA-ED-FE') { throw 'macOS 실행 파일 형식이 아닙니다.' }
    $cpu=[BitConverter]::ToUInt32($header,4)
    if (($rid -eq 'osx-arm64' -and $cpu -ne 0x0100000C) -or ($rid -eq 'osx-x64' -and $cpu -ne 0x01000007)) { throw '실행 파일 CPU가 게시 대상과 다릅니다.' }
    $zipPath=Join-Path $outputRoot ("SnapBerry-macos-"+$rid.Substring(4)+"-unsigned.zip")
    $zipTemp=$zipPath+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    try {
        $zipStream=[IO.File]::Create($zipTemp);$zip=[IO.Compression.ZipArchive]::new($zipStream,[IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
                $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
                $entry=$zip.CreateEntry($relative,[IO.Compression.CompressionLevel]::Fastest)
                $entry.ExternalAttributes=[Convert]::ToInt32($(if ($file.Name -eq 'SnapBerry.Desktop' -or $file.Extension -in @('.dylib','.sh')) {'81ED0000'} else {'81A40000'}),16)
                $entryStream=$entry.Open();$inputStream=$file.OpenRead()
                try { $inputStream.CopyTo($entryStream) } finally { $inputStream.Dispose();$entryStream.Dispose() }
            }
        } finally { $zip.Dispose();$zipStream.Dispose() }
        Move-Item -LiteralPath $zipTemp -Destination $zipPath -Force
    } finally { if (Test-Path -LiteralPath $zipTemp) { Remove-Item -LiteralPath $zipTemp } }
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Format-List
}
