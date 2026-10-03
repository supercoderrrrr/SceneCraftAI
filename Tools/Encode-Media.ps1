param(
    [Parameter(Mandatory = $true)][string]$FramesPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$FfmpegPath
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
foreach ($clip in @('tour', 'edit', 'layout', 'language')) {
    $target = Join-Path $OutputPath ("scenecraft-$clip.mp4")
    & $FfmpegPath -hide_banner -loglevel warning -y -framerate 15 -start_number 0 -i (Join-Path $FramesPath "$clip-%04d.png") -an -c:v libx264 -preset slow -crf 19 -pix_fmt yuv420p -movflags +faststart -map_metadata -1 $target
    if ($LASTEXITCODE -ne 0) { throw "MP4 encoding failed: $clip" }
    if ($clip -in @('tour', 'layout', 'language')) {
        $gif = Join-Path $OutputPath ("scenecraft-$clip.gif")
        & $FfmpegPath -hide_banner -loglevel warning -y -i $target -filter_complex 'fps=8,scale=960:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=160:stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a' -an -loop 0 -map_metadata -1 $gif
        if ($LASTEXITCODE -ne 0) { throw "GIF encoding failed: $clip" }
    }
}
$concat = "file 'scenecraft-tour.mp4'`nfile 'scenecraft-edit.mp4'`nfile 'scenecraft-layout.mp4'`nfile 'scenecraft-language.mp4'`n"
$concatPath = Join-Path $OutputPath 'clips.txt'
[IO.File]::WriteAllText($concatPath, $concat, [Text.UTF8Encoding]::new($false))
& $FfmpegPath -hide_banner -loglevel warning -y -f concat -safe 0 -i $concatPath -c copy -movflags +faststart -map_metadata -1 (Join-Path $OutputPath 'scenecraft-demo.mp4')
if ($LASTEXITCODE -ne 0) { throw 'Full demonstration encoding failed' }
foreach ($name in @('apartment-overview', 'furniture-editing', 'layout-before', 'layout-after', 'interface-chinese', 'interface-english', 'bedroom-details', 'kitchen-details', 'bathroom-details')) {
    Copy-Item -LiteralPath (Join-Path $FramesPath "$name.png") -Destination (Join-Path $OutputPath "$name.png") -Force
}
Copy-Item -LiteralPath (Join-Path $FramesPath 'capture-result.json') -Destination (Join-Path $OutputPath 'capture-result.json') -Force
Write-Output 'Encoded the real Unity capture clips, GIF previews and screenshots'
