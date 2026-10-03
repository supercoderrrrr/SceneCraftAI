# Capture Notes / 录制说明

## English

All images, GIFs and videos in this folder originate from the project's standalone Unity player. The scene is generated from the short default English apartment brief, using **local planning**, with no paid API calls. There is no image synthesis or replacement of the generated geometry.

The Development Build capture component observes the live scene and invokes the same runtime commands used by the interface. Camera paths and edit timing are automated; the clips are not manual mouse recordings. Captions and the camera-framing preset exist only in capture mode. The selection panel is hidden during the tour and language preview; the HUD is hidden for room-detail screenshots.

### Media

| File | Content |
| --- | --- |
| `scenecraft-demo.mp4` | Combined tour, furniture editing/undo, locked-object layout search and language switching |
| `scenecraft-tour.mp4` / `.gif` | Five connected rooms and their furnished interiors |
| `scenecraft-edit.mp4` | An actual validated furniture move followed by undo |
| `scenecraft-layout.mp4` / `.gif` | Coffee table locked while unlocked objects are replanned |
| `scenecraft-language.mp4` / `.gif` | English → 中文 → English, without replacing the scene or prompt |
| `apartment-overview.png` | Whole-apartment screenshot |
| `furniture-editing.png` | Furniture-editing controls and selected object |
| `layout-before.png`, `layout-after.png` | Before/after with the locked coffee table |
| `interface-english.png`, `interface-chinese.png` | Both interface languages |
| `bedroom-details.png`, `kitchen-details.png`, `bathroom-details.png` | Actual room close-ups |
| `capture-result.json` | Counts, scores and checked operation results from the player |

PNG frames are captured at 1600 × 900. MP4 playback is 15 fps, H.264/YUV420p with web-friendly fast-start metadata; GIF previews are 960 × 540 at 8 fps. Fixed capture timing is a presentation setting, **not an application-performance measurement**. The recordings have no audio.

### Reproduce

Open the Unity project once to resolve packages. Set `$unityExe` to a local **2022.3.62f2** editor and `$ffmpegExe` to an installed FFmpeg executable. From the repository root in PowerShell, with other Editors for this project closed:

```powershell
$projectPath = (Get-Location).Path
New-Item -ItemType Directory -Path ./Validation -Force | Out-Null
$playerPath = Join-Path $projectPath 'Validation/Player/SceneCraftAI.exe'
& $unityExe -batchmode -quit -projectPath $projectPath `
    -executeMethod SceneCraftAI.Editor.PortfolioBuild.Build `
    -sceneBuild $playerPath -logFile (Join-Path $projectPath 'Validation/build.log') | Out-Null

& $playerPath -batchmode -screen-fullscreen 0 -screen-width 1600 -screen-height 900 `
    -force-d3d11 -sceneCapture (Join-Path $projectPath 'CaptureFrames') `
    -logFile (Join-Path $projectPath 'Validation/capture.log') | Out-Null

& ./Tools/Encode-Media.ps1 -FramesPath ./CaptureFrames `
    -OutputPath ./docs/media -FfmpegPath $ffmpegExe
```

Check `SCENE_BUILD: Succeeded`, `SCENE_CAPTURE: completed, checks passed True` and the JSON report before encoding. The helper intentionally fails if an edit, undo, lock or scene-preserving language check fails. Raw frames, player binaries and machine-specific logs are excluded from Git.

See [validation](../VALIDATION.md) for unresolved layout issues and test scope.

---

## 简体中文

本目录素材均来自真实独立 Unity 播放器。使用默认英文房间列表进行**本地生成**，未调用付费 API，也没有生成式图片或替换场景几何。

录制脚本自动控制镜头和时序，调用与界面相同的家具移动、撤销、锁定、换布局和语言切换操作，并检查结果；不是人工鼠标录屏。展示字幕与镜头预设仅在录制模式启用。巡视和语言片段隐藏选中面板，房间细节截图隐藏整个 HUD。

原始截图为 1600 × 900，MP4 为 15 fps，GIF 为 960 × 540、8 fps，无音频。这些是素材播放参数，**不是运行性能测试结果**。录制结果、对象数量、布局分数与操作检查保存于 `capture-result.json`。

复现可使用上方 PowerShell 命令，需要 Unity 2022.3.62f2 与 FFmpeg。确认构建和录制成功、JSON 检查通过后再编码。原始帧、播放器和本机日志均不上传，布局仍存在的审计提醒见[验证说明](../VALIDATION.md)。
