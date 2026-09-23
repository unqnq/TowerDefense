using UnityEditor;
using UnityEngine;


namespace MAST
{
    namespace Building
    {
        // Pointer-to-placement targeting: where on the grid (or against a
        // placed model's face, or on a raycast-hit surface) the piece under
        // the mouse should go
        public static class GridPointer
        {
            // Layer that the MAST grid is set to "computed live — the layer is
            // a setting now, and a cached mask went stale when it changed"
            private static int gridLayerMask => 1 << Const.Grid.gridLayer;

            // Placement position for the current pointer, snapped in GRID
            // SPACE "the holder's local frame".  Also maintains
            // Visualizer.visualizerOnGrid, the it-can-be-placed-here gate
            public static Vector3 GetPlacementPosition()
            {
                // The backface flag is PROJECT-GLOBAL "it changes the user's own
                // runtime physics queries" — set it only around this raycast
                bool previousBackfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;

                // Create a ray starting from the current point the mouse is
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                // Raycast to grid layer
                Visualizer.visualizerOnGrid =
                    Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit,
                        Mathf.Infinity, gridLayerMask);

                Physics.queriesHitBackfaces = previousBackfaces;

                // Snap in grid space so the grid can sit anywhere at any angle
                // and cells still line up
                Vector3 gridPoint = GridSpace.WorldToGrid(hit.point);

                float xPos, zPos;
                if (Settings.Data.placement.snapToGrid)
                {
                    xPos = RoundToNearestGridCenter(gridPoint.x);
                    zPos = RoundToNearestGridCenter(gridPoint.z);
                }
                else
                {
                    xPos = gridPoint.x;
                    zPos = gridPoint.z;
                }

                if (xPos == Mathf.Infinity || zPos == Mathf.Infinity)
                {
                    xPos = 0;
                    zPos = 0;
                }

                // Calculate current placement position on the grid
                Vector3 placementPosition = GridSpace.GridToWorld(
                    new Vector3(xPos, Settings.Data.gui.grid.gridHeight * Settings.Data.gui.grid.yUnitSize, zPos));

                // Prefabs using the Placement Raycast are free-form surface
                // placements — occupancy has nothing useful to say about them.
                // Face targeting used to preempt the raycast (props floated at
                // cell height instead of hugging the surface they aim for)
                if (PrefabProfile.PlacementRaycast.GetUseRaycast())
                    return GetRaycastPosition(placementPosition);

                // Face-adjacent placement: when the pointer is over an already
                // placed model closer than the grid, target a cell relative to
                // the face under the pointer "occupancy raycast is pure math —
                // no colliders involved".
                //
                // SHIFT changes the target by the SHIFT Placement setting:
                //   Grid          — occupancy is ignored entirely; the piece
                //                   targets the grid plane as if nothing were
                //                   placed (the original bypass)
                //   OccupiedCell  — the piece EMBEDS into the pointed cell
                //                   itself instead of the cell against its
                //                   face: overlap placement at any height,
                //                   aimed at the piece instead of the grid
                bool shiftHeld = Event.current != null && Event.current.shift;
                bool occupancyBypassed = shiftHeld &&
                    Settings.Data.placement.shiftPlacement == ShiftPlacement.Grid;

                if (Settings.Data.placement.enableOccupancy &&
                    Settings.Data.placement.placeOnFaces &&
                    !occupancyBypassed)
                {
                    if (OccupancyMap.TryRaycast(ray, out Vector3Int hitCell, out Vector3Int adjacentCell, out float occupancyDistance))
                    {
                        if (!Visualizer.visualizerOnGrid || occupancyDistance < hit.distance)
                        {
                            Vector3Int targetCell = shiftHeld ? hitCell : adjacentCell;
                            placementPosition = OccupancyMap.CellToPlacementPosition(targetCell);
                            Visualizer.visualizerOnGrid = true;
                        }
                    }
                    // While the pointer is still on the piece just placed, the
                    // ghost HIDES instead of falling back to the grid cell
                    // inside that piece "invisible in identical geometry — the
                    // rotate hotkey looked dead after every placement".  It
                    // reappears the moment the pointer moves off
                    else if (OccupancyMap.PointerOverShieldedPlacement)
                    {
                        Visualizer.visualizerOnGrid = false;
                    }
                }

                return placementPosition;
            }

            // Calculate closest position to the grid - offset to grid center
            private static float RoundToNearestGridCenter(float positionOnAxis)
            {
                return (Mathf.Floor(positionOnAxis / Settings.Data.gui.grid.xzUnitSize) + 0.5f)
                    * Settings.Data.gui.grid.xzUnitSize;
            }

            // Get placement position based on raycast operation
            private static Vector3 GetRaycastPosition(Vector3 placementPosition)
            {
                // Get Raycast Direction "grid-relative: Down means toward the
                // grid plane, however the holder is rotated"
                Vector3 raycastDirection = Vector3.down;
                switch (PrefabProfile.PlacementRaycast.GetDirection())
                {
                    case DirectionVector.Down:      raycastDirection = Vector3.down;    break;
                    case DirectionVector.Up:        raycastDirection = Vector3.up;      break;
                    case DirectionVector.Left:      raycastDirection = Vector3.left;    break;
                    case DirectionVector.Right:     raycastDirection = Vector3.right;   break;
                    case DirectionVector.Forward:   raycastDirection = Vector3.forward; break;
                    case DirectionVector.Back:      raycastDirection = Vector3.back;    break;
                }
                raycastDirection = GridSpace.GridToWorldDirection(raycastDirection);

                // Create a layer mask that excludes the grid and the ghost
                int rayCastLayers = ~(1 << Const.Grid.gridLayer | 1 << Const.Placement.visualizerLayer);

                if (Physics.Raycast(
                        placementPosition + GridSpace.GridToWorldDirection(PrefabProfile.PlacementRaycast.GetStartOffset()),
                        raycastDirection, out RaycastHit hit, Mathf.Infinity, rayCastLayers))
                {
                    if (hit.point.x != Mathf.Infinity && hit.point.y != Mathf.Infinity && hit.point.z != Mathf.Infinity)
                        placementPosition = hit.point;
                }

                return placementPosition;
            }
        }
    }
}
