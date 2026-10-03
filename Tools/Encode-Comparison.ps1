param(
    [Parameter(Mandatory = $true)][string]$FramesPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [Parameter(Mandatory = $true)][string]$FfmpegPath
)

$ErrorActionPreference = 'Stop'
$result = Get-Content -LiteralPath (Join-Path $FramesPath 'comparison-result.json') -Raw | ConvertFrom-Json
if (-not ($result.roomsUnchanged -and $result.inventoryUnchanged -and $result.lockPreserved) -or $result.errors.Count -ne 0 -or $result.highlightedDistance -lt 0.25 -or $result.livingHighlightedDistance -lt 0.25) {
    throw 'Comparison checks failed, refusing to publish misleading annotations'
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$video = Join-Path $OutputPath 'scenecraft-layout.mp4'
& $FfmpegPath -hide_banner -loglevel warning -y -framerate 15 -start_number 0 -i (Join-Path $FramesPath 'comparison-%04d.png') -an -c:v libx264 -preset slow -crf 19 -pix_fmt yuv420p -movflags +faststart -map_metadata -1 $video
if ($LASTEXITCODE -ne 0) { throw 'Comparison video encoding failed' }
$gif = Join-Path $OutputPath 'scenecraft-layout-comparison.gif'
& $FfmpegPath -hide_banner -loglevel warning -y -i $video -filter_complex 'fps=8,scale=960:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=160:stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a' -an -loop 0 -map_metadata -1 $gif
if ($LASTEXITCODE -ne 0) { throw 'Comparison GIF encoding failed' }
Copy-Item -LiteralPath $gif -Destination (Join-Path $OutputPath 'scenecraft-layout.gif') -Force
foreach ($name in @('layout-lock-comparison.png', 'layout-move-comparison.png', 'comparison-result.json')) {
    Copy-Item -LiteralPath (Join-Path $FramesPath $name) -Destination (Join-Path $OutputPath $name) -Force
}
& $FfmpegPath -hide_banner -loglevel warning -y -f concat -safe 0 -i (Join-Path $OutputPath 'clips.txt') -c copy -movflags +faststart -map_metadata -1 (Join-Path $OutputPath 'scenecraft-demo.mp4')
if ($LASTEXITCODE -ne 0) { throw 'Updated demonstration encoding failed' }
Write-Output ('Published annotated room comparison, actual refrigerator movement: ' + [math]::Round($result.highlightedDistance, 2) + ' m')
