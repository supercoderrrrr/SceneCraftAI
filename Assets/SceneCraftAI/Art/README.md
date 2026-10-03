# SceneCraft AI Art Library

Keep imported portfolio assets inside this folder so they stay with the Unity project:

- `Models/`: source `.fbx`, `.obj`, `.glb` or `.gltf` models
- `Materials/`: Unity materials
- `Textures/`: texture images
- `Prefabs/`: normalized, reusable furniture prefabs
- `Licenses/`: source links, authors and license files for every third-party asset

The current demo uses selected lightweight Kenney Furniture Kit FBX models with procedural fallback objects. Source models, generated Prefabs, and attribution stay inside the project; the runtime does not download assets. Prefab filenames use the original FBX model identity, while display names and runtime palette variants are maintained separately in `AssetCatalog`.
