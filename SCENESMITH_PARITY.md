# SceneSmith parity boundary

SceneCraft AI follows the broad planning/retrieval/placement/critique workflow of SceneSmith in a smaller Unity portfolio demo. It removes local neural asset generation to target consumer hardware, but does not claim research-system parity or measured 8 GB VRAM compliance.

## Implemented locally

- Unified house representation: a single room is a one-room house; multi-room prompts create room specs, room-scoped objects, doors/open connections and serializable topology.
- Floor-plan stage: deterministic room dimensions, adjacency, exterior entrance, windows and paired shared openings.
- Furniture stage: exact catalog IDs, source Prefab names, dimensions, room-local placement, anchor-first ordering and repeatable assets.
- Wall stage: wall-mounted art and opening-aware wall geometry.
- Ceiling stage: ceiling-mounted lights with height validation.
- Manipuland stage: books, vases, table lamps and decor bowls placed on a named support object with footprint/height checks.
- Scene observation approximation: authoritative scene state contains room, object, position, yaw, rotation-expanded axis-aligned bounds, relation, anchor and support data.
- Geometric tools: move, rotate, delete, clear, wall snap, free-space search, face semantic anchor, orient-and-snap to anchor, undo and per-object layout locking.
- Validation/repair: room bounds, 3D intersections, door/window approach clearance, object-front operational clearance, relation scoring, facing, wall contact, per-room flood-fill circulation, house graph connectivity and support-surface validation.
- Planner/Critic loop: DeepSeek produces normalized JSON; Unity evaluates four deterministic variants; one bounded Critic pass receives four deterministic geometry projections and may repair semantic relations without changing the object set or room assignment.
- Prompt contract: explicit counts/exclusions are reconciled after either planner, while `只放/only` prompts become strict category allowlists so contextual defaults cannot leak into the scene.
- Presentation layer: per-room plan preview, runtime audit warnings and 20-prompt regression matrix for repeatable portfolio demonstrations.
- Residential auto-design layer: a room-only prompt expands into a dense five-room lifestyle program with role-specific master/secondary bedrooms, kitchen and bathroom fixtures, deterministic size variation and a staggered non-rectangular whole-house footprint.
- Checkpoint equivalent: complete JSON save/load plus alternative-layout regeneration.

## Lightweight approximations

- SceneSmith's VLM image observation is represented by deterministic geometry/state observation, four text projections and numeric scoring. It does not send rendered screenshots to a vision model.
- SceneSmith's Drake physics validation is approximated by rotation-expanded axis-aligned bounds, clearance zones, support checks and graph/grid reachability. Rigidbody settling and fallen-object simulation are not run during generation.
- SceneSmith's learned/open-set retrieval is represented by a local identity/alias/tag registry with exact Prefab/FBX locks.
- The main showcase uses a deterministic staggered multi-wing topology rather than the old connected chain. Rooms are still orthogonal zones; arbitrary concave room polygons and SceneSmith's general free-form floor-plan search are not implemented.
- Materials use lightweight runtime palette variants rather than AmbientCG PBR retrieval/generation.

## Not included in this portfolio build

- SAM3D open-set asset generation (the SceneSmith README states its production backend needs 32 GB GPU memory).
- Hunyuan3D-2 local generation (the documented proof-of-concept backend needs 24 GB GPU memory).
- Blender render-server orchestration, Drake/SDF simulation, articulated ArtVIP assets, robot evaluation, USD/MuJoCo export and multi-GPU workers.
- Full autonomous VLM tool-call loops for wall, ceiling and manipuland agents.

These omitted systems are possible future extensions, not completed or guaranteed drop-in integrations. Upstream backend requirements were checked against the [SceneSmith README](https://github.com/nepfaff/scenesmith#readme) on 2026-10-03.
