using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Building
    {
        // Cell-accurate occupancy map of everything placed under the MAST Holder,
        // borrowed from the DoaB scene builder — with one deliberate change: DoaB
        // spawned an invisible BoxCollider GameObject per occupied cell for its
        // placement raycasts; here the raycast is pure math against the map, so
        // nothing is ever added to the user's scene.
        //
        // The map is rebuilt lazily: any hierarchy change or undo marks it dirty,
        // and the next query rescans the holder's children.  Footprints come from
        // OccupancyFootprint's cache, so a rebuild is cheap after the first scan.
        public static class OccupancyMap
        {
            // Cell → does its occupant permit overlap "MASTPrefabSettings.allowOverlap"
            private static readonly Dictionary<Vector3Int, bool> occupiedCells =
                new Dictionary<Vector3Int, bool>();

            // Cells of the most recently placed model: face targeting ignores
            // them "falling back to the grid" until the pointer moves off the
            // piece — otherwise a held Draw Continuous face-places onto its own
            // fresh piece every event and stacks models toward the camera
            private static readonly HashSet<Vector3Int> shieldedCells =
                new HashSet<Vector3Int>();

            private static bool dirty = true;
            private static bool hooksInstalled = false;

            // ---------------------------------------------------------------------------
            #region Rebuild
            // ---------------------------------------------------------------------------

            public static void MarkDirty()
            {
                dirty = true;
            }

            // Placing, erasing, undoing, or any manual scene edit invalidates the
            // map; the next query rebuilds it
            private static void InstallHooks()
            {
                if (hooksInstalled)
                    return;
                hooksInstalled = true;

                EditorApplication.hierarchyChanged += MarkDirty;
                Undo.undoRedoPerformed += MarkDirty;

                // Plain transform moves fire NEITHER of the above — manually
                // dragging a placed prefab used to leave the map stale until
                // the next add/delete/undo
                ObjectChangeEvents.changesPublished += OnObjectChanges;
            }

            private static void OnObjectChanges(ref ObjectChangeEventStream stream)
            {
                MarkDirty();
            }

            private static void RebuildIfNeeded()
            {
                InstallHooks();

                if (!dirty)
                    return;

                occupiedCells.Clear();

                // Find the holder without creating one "querying the map must
                // never modify the scene".  While no holder exists the map stays
                // dirty, so it fills in as soon as one appears
                GameObject holder = Placement.targetParent;
                if (holder == null)
                {
                    try { holder = GameObject.FindGameObjectWithTag(Const.Placement.defaultTargetParentTag); }
                    catch { return; }
                }
                if (holder == null)
                    return;

                foreach (Transform child in holder.transform)
                    AddInstance(child.gameObject);

                dirty = false;
            }

            private static void AddInstance(GameObject instance)
            {
                // Solid unless the prefab opts into overlap — a missing
                // MASTPrefabSettings must not silently disable blocking
                bool allowOverlap = false;
                Component.MASTPrefabSettings settings =
                    instance.GetComponentInChildren<Component.MASTPrefabSettings>();
                if (settings != null)
                    allowOverlap = settings.allowOverlap;

                // Greeble-placed pieces are always occupancy-exempt, whatever
                // their prefab says "they attach to faces, not cells".  Plain
                // string compare: CompareTag LOGS a console error (it doesn't
                // throw) for every query while the tag isn't defined yet
                if (instance.tag == Const.Placement.greebleTag)
                    allowOverlap = true;

                Vector3Int pivotCell = PositionToPivotCell(instance.transform.position);

                foreach (Vector3Int offset in OccupancyFootprint.GetCells(instance))
                {
                    Vector3Int cell = pivotCell + offset;

                    // A blocking occupant is never overwritten by a permissive one
                    if (!occupiedCells.TryGetValue(cell, out bool existingAllows) || existingAllows)
                        occupiedCells[cell] = allowOverlap;
                }
            }

            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Placement queries
            // ---------------------------------------------------------------------------

            // Would placing this model here overlap something already placed?
            // Overlap is allowed when either party permits it, matching the old
            // per-prefab allowOverlap behavior
            public static bool WouldBlockPlacement(GameObject ghost, Object cacheIdentity, bool placingAllowsOverlap)
            {
                // Master switch "Settings > Placement > Occupancy"
                if (!Settings.Data.placement.enableOccupancy)
                    return false;

                if (placingAllowsOverlap)
                    return false;

                RebuildIfNeeded();

                if (occupiedCells.Count == 0)
                    return false;

                Vector3Int pivotCell = PositionToPivotCell(ghost.transform.position);

                foreach (Vector3Int offset in OccupancyFootprint.GetCells(ghost, cacheIdentity))
                    if (occupiedCells.TryGetValue(pivotCell + offset, out bool occupantAllows) &&
                        !occupantAllows)
                        return true;

                return false;
            }

            // The ghost's cells with per-cell blocked state — drives the
            // occupancy visualization "and doubles as diagnostics"
            public static void GetPlacementCells(GameObject ghost, Object cacheIdentity,
                List<(Vector3Int cell, bool blocked)> results)
            {
                results.Clear();
                RebuildIfNeeded();

                Vector3Int pivotCell = PositionToPivotCell(ghost.transform.position);

                foreach (Vector3Int offset in OccupancyFootprint.GetCells(ghost, cacheIdentity))
                {
                    Vector3Int cell = pivotCell + offset;
                    bool blocked = occupiedCells.TryGetValue(cell, out bool occupantAllows) && !occupantAllows;
                    results.Add((cell, blocked));
                }
            }

            // True while the closest thing under the pointer is a freshly
            // placed, still-shielded piece.  The ghost hides there: it used to
            // fall back to the grid cell INSIDE the fresh piece — invisible in
            // identical geometry, so rotate hotkeys looked completely dead
            // right after every placement
            public static bool PointerOverShieldedPlacement { get; private set; }

            // Shield a freshly placed model's cells from face targeting.  The
            // shield lifts on its own when the pointer leaves the piece — enter
            // it again by moving, and it face-places like anything else.
            // During a Draw Continuous drag the shield ACCUMULATES every piece
            // placed in that drag: it used to hold only the newest one, so the
            // pointer sweeping back over the drag's own trail re-armed
            // face-placement and stacked pieces toward the camera
            public static void ShieldPlacement(GameObject placedInstance, Object cacheIdentity)
            {
                if (MAST.EditorUI.ToolState.ActiveBuildDrag != BuildDrag.Draw)
                    shieldedCells.Clear();

                Vector3Int pivotCell = PositionToPivotCell(placedInstance.transform.position);

                foreach (Vector3Int offset in OccupancyFootprint.GetCells(placedInstance, cacheIdentity))
                    shieldedCells.Add(pivotCell + offset);
            }

            // World position that places a model's pivot in a cell: XZ center of
            // the cell, Y at its bottom — the same convention grid placement uses.
            // Cells live in grid space, so this converts back to world at the end
            public static Vector3 CellToPlacementPosition(Vector3Int cell)
            {
                float xzUnitSize = Settings.Data.gui.grid.xzUnitSize;
                float yUnitSize = Settings.Data.gui.grid.yUnitSize;

                return GridSpace.GridToWorld(new Vector3(
                    (cell.x + 0.5f) * xzUnitSize,
                    cell.y * yUnitSize,
                    (cell.z + 0.5f) * xzUnitSize));
            }

            // The cell a pivot at this world position belongs to.  The Y epsilon
            // keeps float noise from flipping pivots that sit exactly on a cell
            // floor into the cell below
            private static Vector3Int PositionToPivotCell(Vector3 worldPosition)
            {
                float xzUnitSize = Settings.Data.gui.grid.xzUnitSize;
                float yUnitSize = Settings.Data.gui.grid.yUnitSize;

                Vector3 gridPosition = GridSpace.WorldToGrid(worldPosition);

                return new Vector3Int(
                    Mathf.FloorToInt(gridPosition.x / xzUnitSize),
                    Mathf.FloorToInt(gridPosition.y / yUnitSize + 0.001f),
                    Mathf.FloorToInt(gridPosition.z / xzUnitSize));
            }

            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Occupancy raycast (math only — no colliders)
            // ---------------------------------------------------------------------------

            // Find the closest occupied cell along the ray.  The face the ray
            // entered through gives the adjacent cell, which is where a model
            // placed "against" the hit model belongs
            public static bool TryRaycast(Ray ray, out Vector3Int hitCell,
                out Vector3Int adjacentCell, out float hitDistance)
            {
                hitCell = Vector3Int.zero;
                adjacentCell = Vector3Int.zero;
                hitDistance = float.MaxValue;

                RebuildIfNeeded();

                if (occupiedCells.Count == 0)
                    return false;

                // Cells are axis-aligned in grid space — transform the ray there
                // instead of the cells.  Distances along the grid ray are in
                // grid metric "the holder's scale divides them out", so the
                // winning hit converts back to a world distance at the end
                Ray gridRay = GridSpace.WorldToGridRay(ray);

                float xzUnitSize = Settings.Data.gui.grid.xzUnitSize;
                float yUnitSize = Settings.Data.gui.grid.yUnitSize;
                var cellSize = new Vector3(xzUnitSize, yUnitSize, xzUnitSize);

                bool found = false;
                float closestGridDistance = float.MaxValue;

                foreach (KeyValuePair<Vector3Int, bool> occupied in occupiedCells)
                {
                    // Overlap-allowed occupants are transparent to placement:
                    // the ray passes through to the grid (or the first blocking
                    // cell), so overlapping placement always lands on the grid
                    if (occupied.Value)
                        continue;

                    Vector3Int cell = occupied.Key;
                    var boxMin = new Vector3(cell.x * xzUnitSize, cell.y * yUnitSize, cell.z * xzUnitSize);
                    Vector3 boxMax = boxMin + cellSize;

                    if (RayIntersectsBox(gridRay, boxMin, boxMax,
                            out float entryDistance, out int entryAxis, out int entrySign) &&
                        entryDistance < closestGridDistance)
                    {
                        closestGridDistance = entryDistance;
                        hitCell = cell;

                        Vector3Int faceOffset = Vector3Int.zero;
                        faceOffset[entryAxis] = entrySign;
                        adjacentCell = cell + faceOffset;

                        found = true;
                    }
                }

                // Shield check: while the closest thing under the pointer is
                // still a freshly placed piece, report no hit "the grid takes
                // over".  The moment the pointer finds something else — another
                // piece or empty grid — the shield lifts.  EXCEPT during a
                // Draw Continuous drag: the pointer legitimately crosses empty
                // cells between placements, and lifting there re-armed
                // face-placement onto the drag's own trail.  Pieces placed
                // BEFORE the drag aren't shielded, so building a second layer
                // on old geometry still works mid-drag
                PointerOverShieldedPlacement = false;
                if (shieldedCells.Count > 0)
                {
                    if (found && shieldedCells.Contains(hitCell))
                    {
                        PointerOverShieldedPlacement = true;
                        return false;
                    }

                    if (MAST.EditorUI.ToolState.ActiveBuildDrag != BuildDrag.Draw)
                        shieldedCells.Clear();
                }

                if (found)
                    hitDistance = Vector3.Distance(ray.origin,
                        GridSpace.GridToWorld(gridRay.GetPoint(closestGridDistance)));

                return found;
            }

            // Slab-method ray/box intersection that also reports which face the
            // ray entered through "axis index plus outward sign"
            private static bool RayIntersectsBox(Ray ray, Vector3 boxMin, Vector3 boxMax,
                out float entryDistance, out int entryAxis, out int entrySign)
            {
                entryDistance = 0f;
                entryAxis = 1;
                entrySign = 1;

                float entryT = float.MinValue;
                float exitT = float.MaxValue;

                for (int axis = 0; axis < 3; axis++)
                {
                    float origin = ray.origin[axis];
                    float direction = ray.direction[axis];

                    // Ray parallel to this slab: inside it or a clean miss
                    if (Mathf.Abs(direction) < 1e-9f)
                    {
                        if (origin < boxMin[axis] || origin > boxMax[axis])
                            return false;
                        continue;
                    }

                    float inverseDirection = 1f / direction;
                    float nearT = (boxMin[axis] - origin) * inverseDirection;
                    float farT = (boxMax[axis] - origin) * inverseDirection;

                    if (nearT > farT)
                    {
                        float swap = nearT;
                        nearT = farT;
                        farT = swap;
                    }

                    if (nearT > entryT)
                    {
                        entryT = nearT;
                        entryAxis = axis;

                        // The entered face's outward normal points against the
                        // ray's travel along this axis
                        entrySign = direction > 0f ? -1 : 1;
                    }

                    if (farT < exitT)
                        exitT = farT;

                    if (entryT > exitT)
                        return false;
                }

                // Behind the camera, or the ray starts inside the box "no face
                // was entered, so there is nothing to place against"
                if (entryT < 0f)
                    return false;

                entryDistance = entryT;
                return true;
            }

            #endregion
            // ---------------------------------------------------------------------------
        }
    }
}
