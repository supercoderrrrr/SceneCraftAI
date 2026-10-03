# Development Archive / 开发归档

[English](#english) | [简体中文](#简体中文)

## English

### Importing an Existing Unity Project

Development was recorded in Unity Version Control / Plastic SCM before this GitHub publication. The first Git commit imports the reviewed publication snapshot, marked `legacy-import`; it is not the first day of implementation. Git commit dates and feature order are not reconstructed or backdated.

[HISTORY.md](docs/scm/HISTORY.md) and [changesets.json](docs/scm/changesets.json) preserve genuine IDs, timestamps, original check-in comments and repository-relative changed paths. Account emails, cloud identifiers and absolute workspace paths are excluded. This is a metadata archive, not a complete source-revision export. Historical comments describe their time, not the current feature checklist.

### Recorded Work

| Record | Date (UTC+08:00) | Work | Current entry points |
| --- | --- | --- | --- |
| CS0 | 2026-08-17 | SCM repository initialization, without implementation files | [SCM history](docs/scm/HISTORY.md) |
| CS1 | 2026-10-03 | Existing multi-room home demo checkpoint: local/cloud planning, shared-wall openings, interactive editing, scoring and camera pan | [SceneRuntimeController](Assets/SceneCraftAI/Scripts/Runtime/SceneRuntimeController.cs), [HousePromptPlanner](Assets/SceneCraftAI/Scripts/Planning/HousePromptPlanner.cs) |
| CS2 | 2026-10-03 | Bilingual quantity/room constraints, cloud-plan preservation, normalized room geometry, shared-wall ownership and regression tests | [PromptRules](Assets/SceneCraftAI/Scripts/Planning/PromptRules.cs), [RoomGeometry](Assets/SceneCraftAI/Scripts/Domain/RoomGeometry.cs) |
| CS3 | 2026-10-03 | Bilingual scene controls, saved language preference and real event-driven build workflow | [SceneCraftHud](Assets/SceneCraftAI/Scripts/UI/SceneCraftHud.cs), [UiText](Assets/SceneCraftAI/Scripts/UI/UiText.cs) |
| Git publication | 2026-10-03 | Public dependency copy, sanitized cloud settings, real player capture tooling, media and technical documentation | [SceneCapture](Assets/SceneCraftAI/Scripts/Runtime/SceneCapture.cs), [PortfolioBuild](Assets/SceneCraftAI/Editor/PortfolioBuild.cs), [validation](docs/VALIDATION.md) |

The current implementation was developed with AI coding assistance. SceneSmith supplied workflow inspiration; the Unity runtime, deterministic constraints and interactive editing are the project's implementation. This attribution does not claim a legal originality audit or full SceneSmith parity.

### Publication Boundary

The original SCM workspace remains intact. This repository contains a separate full Unity copy with the required scenes, settings, source, generated fixtures and curated CC0 furniture. Local caches, player binaries, source capture frames, private environment configuration and `.plastic` workspace metadata are not published. Unity cloud project/organization fields are blank in the publication copy.

During the fresh import/build, Unity upgraded three URP settings assets and the Burst AOT settings to the installed package serialization versions. These package-managed changes are retained in the validated publication copy; existing asset GUIDs, scenes, Prefabs and gameplay source remain unchanged.

Capture tools are gated by `-sceneCapture` and only compiled in the Editor or Development Builds. Normal startup retains manual camera controls and the ordinary scene generation flow. Capture runs local generation, checks real editing results and exits; output videos are encoded from the generated PNG frame sequence.

### Validation and Next Steps

The inherited implementation has 112 EditMode and 22 PlayMode tests. Publication build and capture results are recorded in [VALIDATION.md](docs/VALIDATION.md), with source evidence in `docs/media/capture-result.json`.

Next steps include better dense-layout repair, measured performance/VRAM baselines, broader semantic regression, richer licensed asset variants and optional visual-model feedback. Arbitrary polygonal room search, image-based VLM critique, rigid-body settling and local neural 3D generation are not completed features.

Use ordinary, current Git commits for later changes. Do not describe unimplemented roadmap items as shipped or replace genuine earlier SCM records with fabricated feature commits.

---

## 简体中文

### 迁移已有工程

本项目公开前使用 Unity Version Control / Plastic SCM 管理。首次 Git 提交导入审核后的发布快照，`legacy-import` 标记这次迁移，不代表从零开始实现的时间。没有重建功能顺序或回填过去的 Git 日期。

[HISTORY.md](docs/scm/HISTORY.md) 与 [changesets.json](docs/scm/changesets.json) 保存真实编号、时间、原始日志和仓库相对变更路径，未公开账户邮箱、云标识及本机路径。它们是元数据归档，不是完整历史源码导出；历史说明也不等同于当前功能清单。

### 已记录的演进

| 记录 | 时间（UTC+08:00） | 内容 | 当前代码入口 |
| --- | --- | --- | --- |
| CS0 | 2026-08-17 | 初始化 SCM 仓库，没有实现文件 | [真实历史](docs/scm/HISTORY.md) |
| CS1 | 2026-10-03 | 多房间住宅演示基线，本地/云端规划、共享墙门窗、家具编辑、评分和镜头平移 | [运行时控制](Assets/SceneCraftAI/Scripts/Runtime/SceneRuntimeController.cs)、[住宅规划](Assets/SceneCraftAI/Scripts/Planning/HousePromptPlanner.cs) |
| CS2 | 2026-10-03 | 中英文数量与房间约束、保留云端规划、统一几何归一化、共享墙所有权与回归 | [提示词约束](Assets/SceneCraftAI/Scripts/Planning/PromptRules.cs)、[房间几何](Assets/SceneCraftAI/Scripts/Domain/RoomGeometry.cs) |
| CS3 | 2026-10-03 | 双语界面、语言偏好保存、事件驱动真实生成进度 | [HUD](Assets/SceneCraftAI/Scripts/UI/SceneCraftHud.cs)、[界面文案](Assets/SceneCraftAI/Scripts/UI/UiText.cs) |
| Git 发布 | 2026-10-03 | 独立公开工程副本、移除云标识、播放器录制、展示素材与技术说明 | [录制工具](Assets/SceneCraftAI/Scripts/Runtime/SceneCapture.cs)、[验证](docs/VALIDATION.md) |

开发过程中使用了 AI 编程辅助。SceneSmith 提供流程灵感，本项目实现 Unity 运行时、确定性约束和交互编辑；不宣称已经完成法律层面的原创性审计或完整复现 SceneSmith。

### 公开范围

原 SCM 工作区保持不变。公开仓库是独立完整 Unity 副本，包含必要场景、设置、代码、程序化设施及 CC0 家具。缓存、播放器二进制、原始录制帧、私有环境配置和 `.plastic` 元数据未公开；发布副本的 Unity 云项目与组织字段留空。

首次导入和构建时，Unity 自动将三个 URP 设置资产和 Burst AOT 设置迁移到当前包的序列化版本，发布副本保留这些已验证的包管理变更。原有资产 GUID、场景、Prefab 和业务源码未修改。

录制工具只在编辑器或 Development Build 中编译，并且必须传入 `-sceneCapture` 才会启动。正常运行仍使用原有相机交互和生成流程。录制采用本地生成，核验真实编辑结果，完成后退出，再由 PNG 帧序列编码为视频。

### 验证与后续维护

既有实现包含 112 项 EditMode 和 22 项 PlayMode 测试。发布构建与录制结果见 [验证记录](docs/VALIDATION.md)，原始结构化证据为 `docs/media/capture-result.json`。

后续方向包括密集布局修复、性能/显存测量、更广语义回归、资产变体和可选视觉反馈。任意多边形房间搜索、图像 VLM 检查、刚体稳定性及本地神经 3D 生成均未完成。后续使用真实当前 Git 提交记录，不将路线图写成已实现，也不伪造早期历史。
