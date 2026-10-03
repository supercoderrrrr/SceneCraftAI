# Validation / 验证记录

## English

Validation date: **2026-10-03**. The checks below ran against the separate GitHub publication copy, with Unity **2022.3.62f2** on Windows. No live DeepSeek request was made for publication or capture.

| Check | Result | Scope |
| --- | --- | --- |
| EditMode tests | 112 passed, 0 failed/skipped | Prompt constraints, asset identity, room geometry, placement, scoring, serialization and UI text |
| PlayMode tests | 22 passed, 0 failed/skipped | Shared walls/door visuals, runtime interactions, HUD lifecycle and scene-preserving language switching |
| Standalone Windows x64 Development Build | Succeeded, 0 build errors | SampleScene, URP rendering and the opt-in capture component |
| Capture runtime errors/exceptions | 0 | Recorded local-generation and editing sequence |
| Furniture move | Passed | Floor lamp moved to a position accepted by runtime validation |
| Undo | Passed | The lamp's original position was restored |
| Locked-object alternative | Passed | Locked coffee table kept its position; 38 objects changed position and/or yaw |
| Language switch | Passed | English → Chinese → English kept the same scene object and prompt text |
| MP4 decode | Passed | Combined video: 360 frames, 24 seconds, 1600 × 900, 15 fps |
| Visual inspection | Completed | Video contact sheet, overview, editing panel, both languages and all three room-detail screenshots |

Machine-specific XML reports and full Unity logs stay in the ignored `Validation/` folder. The public [capture report](media/capture-result.json) contains runtime counts, scores, checks and an empty error list without account details or absolute paths. Test source is committed under [EditMode](../Assets/SceneCraftAI/Tests/EditMode) and [PlayMode](../Assets/SceneCraftAI/Tests/PlayMode).

### Recorded Scene

The default English brief produced 5 rooms, requesting 56 objects and placing 55. The initial score was **91.4/100**, with **6 audit notes**. After the locked-table alternative search the score was **91.6/100**. Scores are internal heuristics, not measured architectural correctness or a comparison with SceneSmith.

These results deliberately remain visible in the screenshots and player report. A completed green workflow does **not** mean the placement audit is clean, and passing software tests does not guarantee every generated room is well furnished. Dense-scene completeness and relation repair remain improvement areas.

### Re-run Tests

Set `$unityExe` to the local Unity 2022.3.62f2 executable, open the project once to resolve packages, close its Editor, and run from the repository root:

```powershell
$projectPath = (Get-Location).Path
New-Item -ItemType Directory -Path ./Validation -Force | Out-Null
& $unityExe -batchmode -projectPath $projectPath -runTests `
    -testPlatform EditMode -testFilter SceneCraftAI.Tests `
    -testResults (Join-Path $projectPath 'Validation/editmode.xml') `
    -logFile (Join-Path $projectPath 'Validation/editmode.log') | Out-Null
& $unityExe -batchmode -projectPath $projectPath -runTests `
    -testPlatform PlayMode -testFilter SceneCraftAI.Tests `
    -testResults (Join-Path $projectPath 'Validation/playmode.xml') `
    -logFile (Join-Path $projectPath 'Validation/playmode.log') | Out-Null
```

Do not add `-quit` to the test commands: the runner needs to finish its lifecycle. [Capture instructions](media/README.md) describe the separate real-player recording and result checks.

### Unverified or Limited

- No paid-cloud authentication, current balance, network reliability or live model quality was tested in this release
- No rendered-image/VLM review, local neural 3D generation, full physics settling or arbitrary polygonal-room solver is implemented
- No cross-device VRAM, FPS or frame-time benchmark was performed; 15 fps is the recording rate only
- Rotation-expanded AABBs and the coarse circulation grid are conservative approximations
- Moving a table carrying small objects can be rejected; grouped parent/child editing is not implemented
- The default dense scene has an unplaced request and audit notes; no claim of flawless generation is made

---

## 简体中文

**2026-10-03**，在独立 GitHub 发布副本上使用 Windows / Unity **2022.3.62f2** 验证：112 项 EditMode、22 项 PlayMode 全部通过，无失败或跳过；独立播放器构建成功，构建错误为 0。

真实录制中，落地灯位置调整通过校验，撤销恢复原位置；锁定茶几后再次布局保留其位置，38 个物件的位置或角度发生变化；中英文切换没有替换场景或改写提示词。录制无运行时错误/异常，24 秒 MP4 的 360 帧完整解码，并人工检查了视频抽帧和核心功能截图。

默认英文提示词生成 5 个房间，请求 56 个物件，实际放入 55 个；初始内部评分 91.4，换布局后 91.6。画面中的 **6 项审计提醒**没有隐藏。流程变绿仅表示执行完成，测试通过也不保证所有生成布局都合理。

本机 XML 和完整日志不上传，[公开录制报告](media/capture-result.json)保留数量、分数和操作检查结果。测试源码、上方运行命令与[录制说明](media/README.md)可用于复验。

本次未调用付费 DeepSeek 接口，未测量显存/FPS，也没有完成图像 VLM 审查、任意多边形房间或完整物理稳定性。密集场景数量完整性、关系修正，以及支撑体和上方小物件的整体编辑仍需继续完善。
