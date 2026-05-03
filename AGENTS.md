# WarSimulation Agent Notes

## Combat Map State

- `MapData` represents the current combat map state, not only the initial generated state.
- `MapData.Height` stores terrain height. Combat-time biome and ground changes are assumed not to change height, colliders, or baked NavMesh.
- `MapData.GroundStates` stores the current ground state for each cell. Combat-time changes such as snow, swamp, water/ice logic, or other ground-state changes should update this data through a system API.
- `MapData` also owns current map-side metadata such as biome IDs, forest regions, lakes, rivers, and placed features.
- `CombatMapSystem` is the scene-level access point for map queries and updates. Gameplay code should ask `CombatMapSystem` for terrain information instead of reaching into map internals from many places.
- `CombatMapSystem.TryGetTerrainInfo(...)` reads `MapData` and returns a snapshot result for one queried position. `TerrainInfo` is not the source of truth.
- Visual systems should read the current `MapData` / `CombatMapSystem` state and refresh terrain colors, overlays, decals, or meshes when the current map state changes.
- Do not assume combat-time biome or ground-state changes require Terrain height regeneration or NavMesh rebaking unless the design explicitly changes.
