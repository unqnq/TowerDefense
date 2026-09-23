using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Building
    {
        public static class Placement
        {
            // GameObject reference for target parent of all placed prefeabs
            [SerializeField] public static GameObject targetParent = null;
            
            // Placement
            [SerializeField] public static Vector3 lastPosition;
            
            public static GameObject PlacePrefabInScene(bool ignoreOccupancy = false)
            {
                // Make sure target parent is referenced
                ReferenceTargetParent();

                if (Visualizer.visualizerOnGrid && Visualizer.GetGameObject() != null &&
                    Visualizer.GetGameObject() != null)
                {
                    // If drawing continuous and an object was just drawn here, exit without drawing
                    if (MAST.EditorUI.ToolState.ActiveBuildTool == BuildMode.DrawContinuous)
                        if (lastPosition == Visualizer.GetGameObject().transform.position)
                            return null;

                    // If placed models already occupy these cells, exit without
                    // placing "stretched Paint Area pieces opt out entirely"
                    if (!ignoreOccupancy && IsPlacementBlocked())
                        return null;
                    
                    // Instantiate the prefab
                    //GameObject newPrefab = GameObject.Instantiate(MAST_Palette.GetSelectedPrefab());
                    GameObject newPrefab = (GameObject) PrefabUtility.InstantiatePrefab(Palette.PrefabPalette.GetSelectedPrefab());
                    
                    // Correct GameObject transform and name "to remove (clone)"
                    newPrefab.transform.rotation = Visualizer.GetGameObject().transform.rotation;
                    newPrefab.transform.localScale = Visualizer.GetGameObject().transform.localScale;
                    newPrefab.transform.position = Visualizer.GetGameObject().transform.position;
                    newPrefab.name = Palette.PrefabPalette.GetSelectedPrefab().name;
                    
                    // Make new prefab child of the target parent
                    newPrefab.transform.parent = targetParent.transform;
                    
                    // Randomize the seed after placement
                    if (MAST.EditorUI.ToolState.ActiveBuildTool == BuildMode.Randomize)
                        Randomizer.GenerateNewRandomSeed();
                    
                    // Save this GameObject position
                    lastPosition = Visualizer.GetGameObject().transform.position;
                    
                    // Make this an Undo point, just after placing the prefab
                    Undo.RegisterCreatedObjectUndo(newPrefab, "Placed new Prefab");

                    // Face targeting ignores the fresh piece until the pointer
                    // leaves it — otherwise a held Draw Continuous face-places
                    // onto its own placement and stacks models toward the camera
                    OccupancyMap.ShieldPlacement(newPrefab, Palette.PrefabPalette.GetSelectedPrefab());

                    // Newly placed prefabs respect the hide-above view filter
                    if (ViewFilter.IsActive)
                        ViewFilter.Apply();

                    // Return with newly created GameObject
                    return newPrefab;
                }
                
                return null;
            }
            
            // Check if placed models already occupy the grid cells this model
            // would fill "the visualizer ghost already carries the final
            // position/rotation/scale, so its footprint is the placement's".
            // Cell-accurate occupancy replaced the interim bounds check, which
            // couldn't tell an L-shaped piece from its bounding box
            public static bool IsPlacementBlocked()
            {
                // Placement Raycast prefabs are free-form surface placements —
                // their footprint overlaps whatever surface they land on by
                // design, so occupancy never blocks them
                if (PrefabProfile.PlacementRaycast.GetUseRaycast())
                    return false;

                // Holding SHIFT bypasses occupancy for this placement "fence
                // posts and rails share a cell"
                if (Event.current != null && Event.current.shift)
                    return false;

                GameObject visualizer = Visualizer.GetGameObject();

                if (visualizer == null || !Visualizer.visualizerOnGrid)
                    return false;

                return OccupancyMap.WouldBlockPlacement(
                    visualizer,
                    Palette.PrefabPalette.GetSelectedPrefab(),
                    PrefabProfile.GetAllowOverlap());
            }

            private static readonly System.Collections.Generic.List<(Vector3Int cell, bool blocked)>
                ghostCells = new System.Collections.Generic.List<(Vector3Int, bool)>();

            // Draw the ghost's occupancy cells as wire cubes "green = free,
            // red = blocked" — shows exactly which cells this placement claims
            public static void DrawOccupancyCells()
            {
                if (MAST.Settings.Data.placement == null ||
                    !MAST.Settings.Data.placement.showOccupancyCells ||
                    !MAST.Settings.Data.placement.enableOccupancy)
                    return;

                // Raycast placements ignore occupancy entirely — drawing their
                // cells would suggest rules that don't apply
                if (PrefabProfile.PlacementRaycast.GetUseRaycast())
                    return;

                GameObject visualizer = Visualizer.GetGameObject();
                if (visualizer == null || !Visualizer.visualizerOnGrid || !visualizer.activeSelf)
                    return;

                OccupancyMap.GetPlacementCells(visualizer,
                    Palette.PrefabPalette.GetSelectedPrefab(), ghostCells);

                float xzUnitSize = MAST.Settings.Data.gui.grid.xzUnitSize;
                float yUnitSize = MAST.Settings.Data.gui.grid.yUnitSize;
                var cellSize = new Vector3(xzUnitSize, yUnitSize, xzUnitSize);

                // Draw in grid space so the cubes follow the holder's rotation
                // and scale
                Matrix4x4 previousMatrix = Handles.matrix;
                Color previousColor = Handles.color;
                Handles.matrix = Matrix4x4.TRS(GridSpace.Origin, GridSpace.Rotation, GridSpace.Scale);

                // The cells this placement claims
                foreach ((Vector3Int cell, bool blocked) in ghostCells)
                {
                    Handles.color = blocked ? Color.red : Color.green;
                    Handles.DrawWireCube(CellCenter(cell, xzUnitSize, yUnitSize), cellSize);
                }

                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
            }

            private static Vector3 CellCenter(Vector3Int cell, float xzUnitSize, float yUnitSize)
            {
                return new Vector3(
                    (cell.x + 0.5f) * xzUnitSize,
                    (cell.y + 0.5f) * yUnitSize,
                    (cell.z + 0.5f) * xzUnitSize);
            }

            // Red wire outline around the visualizer when placement is blocked,
            // matching the eraser's targeting highlight
            public static void DrawBlockedHighlight()
            {
                if (!IsPlacementBlocked())
                    return;

                GameObject visualizer = Visualizer.GetGameObject();
                Renderer[] renderers = visualizer.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return;

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                Color previousColor = Handles.color;
                Handles.color = Color.red;
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.color = previousColor;
            }
            
            // Find an existing holder WITHOUT creating one "window UI must not
            // modify the scene just for being open"
            public static void FindExistingTargetParent()
            {
                try
                {
                    GameObject taggedGameObject =
                        GameObject.FindGameObjectWithTag(MAST.Const.Placement.defaultTargetParentTag);
                    if (taggedGameObject != null)
                        targetParent = taggedGameObject;
                }
                catch
                {
                    // Tag doesn't exist yet — nothing to find
                }
            }

            // Make sure target parent is referenced
            public static void ReferenceTargetParent()
            {
                // Create Tag for target parent if it doesn't already exist
                MAST.TagsAndLayers.AddTag(MAST.Const.Placement.defaultTargetParentTag);
                
                // Try to find a GameObject with the MAST_Holder tag
                GameObject taggedGameObject = GameObject.FindGameObjectWithTag(MAST.Const.Placement.defaultTargetParentTag);
                
                // If GameObject with MAST_Holder tag exists, then use it
                if (taggedGameObject != null)
                {
                    targetParent = taggedGameObject;
                }
                
                // If GameObject with MAST_Holder tag does not exist, then create a new GameObject
                else
                {
                    targetParent = new GameObject();
                    targetParent.transform.position = new Vector3(0, 0, 0);
                    targetParent.name = Const.Placement.defaultTargetParentName;
                    targetParent.tag = MAST.Const.Placement.defaultTargetParentTag;
                }
                
                // Get target parent from saved target parent name
                //targetParent = GameObject.Find(Settings.Data.placement.targetParentName);
                //targetParent = (GameObject)EditorUtility.InstanceIDToObject(Settings.Data.placement.targetParentInstanceID);
                
                // If target parent doesn't exist, create it and named it "MAST_Holder"
                //if (!targetParent)
                //{
                //    targetParent = new GameObject();
                //    targetParent.transform.position = new Vector3(0, 0, 0);
                //    Settings.Data.placement.targetParentInstanceID = targetParent.GetInstanceID();
                //    //Settings.Data.placement.targetParentName = Const.placement.defaultTargetParentName;
                //    //targetParent.name = Settings.Data.placement.targetParentName;
                //    targetParent.name = Const.Placement.defaultTargetParentName;
                //}
            }
            
        }
    }
}

