using UnityEngine;


namespace MAST
{
    namespace Building
    {
        public static class PaintArea
        {
            [SerializeField] private static bool paintingArea = false;

            // Area corners are tracked in GRID SPACE "the holder's local frame"
            // so the area stays a grid-aligned rectangle at any holder rotation
            [SerializeField] private static Vector3 paintAreaStart = new Vector3(0f, 0f, 0f);
            [SerializeField] private static GameObject paintAreaVisualizer;
            [SerializeField] private static Material paintAreaMaterial;

            // Start paint area
            public static void StartPaintArea()
            {
                if (Visualizer.GetGameObject() != null)
                {
                    // Set painting area to true
                    paintingArea = true;

                    // Record paint area start location
                    paintAreaStart = GridSpace.WorldToGrid(Visualizer.GetGameObject().transform.position);
                    paintAreaStart.y = Settings.Data.gui.grid.gridHeight *
                        Settings.Data.gui.grid.yUnitSize + Const.Grid.yOffsetToAvoidTearing;
                    
                    // Create new Paint Area Visualizer
                    paintAreaVisualizer = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    paintAreaVisualizer.transform.position = new Vector3(0f, 0f, 0f);
                    paintAreaVisualizer.name = "MAST_Paint_Area_Visualizer";
                    
                    // Configure Paint Area Visualizer MeshRenderer
                    MeshRenderer paintAreaMeshRenderer = paintAreaVisualizer.GetComponent<MeshRenderer>();
                    paintAreaMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    paintAreaMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    paintAreaMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    paintAreaMeshRenderer.receiveShadows = false;
                    
                    // Configure Paint Area Visualizer Material
                    if (paintAreaMaterial == null)
                        paintAreaMaterial = LoadingHelper.GetPaintAreaMaterial();
                    paintAreaMeshRenderer.material = paintAreaMaterial;
                    
                    // Hidden from the hierarchy and NEVER saved into the user's
                    // scene "HideInHierarchy alone still serializes"
                    paintAreaVisualizer.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                    
                    // Update the paint area
                    UpdatePaintArea();
                }
            }
            
            // Update paint area
            public static void UpdatePaintArea()
            {
                // If painting area
                if (paintingArea)
                {
                    // Get current mouse position on grid "in grid space"
                    Vector3 paintAreaEnd = GridSpace.WorldToGrid(GridPointer.GetPlacementPosition());
                    paintAreaEnd.y = Settings.Data.gui.grid.gridHeight *
                        Settings.Data.gui.grid.yUnitSize + Const.Grid.yOffsetToAvoidTearing;

                    // Make sure paint area start is at the current grid height, incase the grid was moved
                    paintAreaStart.y = Settings.Data.gui.grid.gridHeight *
                        Settings.Data.gui.grid.yUnitSize + Const.Grid.yOffsetToAvoidTearing;

                    // Get dimensions of paint area "grid units, so the world
                    // size scales with the holder"
                    Vector3 scale = new Vector3(
                        (Mathf.Abs((paintAreaStart.x - paintAreaEnd.x) / 10f) + (Settings.Data.gui.grid.xzUnitSize / 10))
                            * GridSpace.Scale.x,
                        1,
                        (Mathf.Abs((paintAreaStart.z - paintAreaEnd.z) / 10f) + (Settings.Data.gui.grid.xzUnitSize / 10))
                            * GridSpace.Scale.z);

                    // Update paint area visualizer to sit between the start and
                    // end points on the holder's plane, oriented with the grid
                    paintAreaVisualizer.transform.SetPositionAndRotation(
                        GridSpace.GridToWorld((paintAreaStart + paintAreaEnd) / 2),
                        GridSpace.Rotation);

                    // Update paint area visualizer x and z scale
                    paintAreaVisualizer.transform.localScale = scale;

                }
            }
            
            // Complete paint area
            public static void CompletePaintArea()
            {
                // Get current mouse position on grid "in grid space"
                Vector3 paintAreaEnd = GridSpace.WorldToGrid(GridPointer.GetPlacementPosition());
                paintAreaEnd.y = Settings.Data.gui.grid.gridHeight *
                    Settings.Data.gui.grid.yUnitSize + Const.Grid.yOffsetToAvoidTearing;

                // If selected Prefab can be scaled
                if (PrefabProfile.GetPaintAreaStretch())
                {
                    // A stretched piece ALWAYS ignores occupancy — it deliberately
                    // spans whatever the area contains
                    GameObject placedPrefab = Placement.PlacePrefabInScene(ignoreOccupancy: true);

                    // Placing nothing "no visualizer" must not throw
                    if (placedPrefab == null)
                    {
                        DeletePaintArea();
                        return;
                    }

                    // Move prefab centerpoint between both paint area corners
                    float gridY = Settings.Data.gui.grid.gridHeight * Settings.Data.gui.grid.yUnitSize;
                    placedPrefab.transform.position = GridSpace.GridToWorld(new Vector3(
                        (paintAreaStart.x + paintAreaEnd.x) / 2,
                        gridY,
                        (paintAreaStart.z + paintAreaEnd.z) / 2));

                    // Apply position offset "a grid-space vector"
                    placedPrefab.transform.position += GridSpace.GridToWorldDirection(PrefabProfile.GetOffsetPosition());

                    //----------------------------------------------
                    // Scale prefab X and Z to match paint area
                    //----------------------------------------------
                    Vector3 scale;

                    // Get the visualizer's GRID-RELATIVE Y angle, divide by 90,
                    // and round.  Result will be 0-3 instead of 0-360
                    int prefabRotationQuadrant = Mathf.RoundToInt(
                        (Quaternion.Inverse(GridSpace.Rotation) * Visualizer.GetGameObject().transform.rotation)
                        .eulerAngles.y / 90f);
                    
                    // If prefab is rotated 0 or 180 degrees, then scale normally
                    if (prefabRotationQuadrant == 0 || prefabRotationQuadrant == 2)
                    {
                        scale = new Vector3(
                            Mathf.Abs(paintAreaStart.x - paintAreaEnd.x) + Settings.Data.gui.grid.xzUnitSize,
                            1,
                            Mathf.Abs(paintAreaStart.z - paintAreaEnd.z) + Settings.Data.gui.grid.xzUnitSize);
                    }
                    // If prefab is rotated 90 or 270 degrees, then swap X and Z for the scale
                    else
                    {
                        scale = new Vector3(
                            Mathf.Abs(paintAreaStart.z - paintAreaEnd.z) + Settings.Data.gui.grid.xzUnitSize,
                            1,
                            Mathf.Abs(paintAreaStart.x - paintAreaEnd.x) + Settings.Data.gui.grid.xzUnitSize);
                    }
                    
                    // Apply calculate scale
                    placedPrefab.transform.localScale = scale;
                    
                    // Apply rotation from the Visualizer
                    placedPrefab.transform.rotation = Visualizer.GetGameObject().transform.rotation;
                    
                }
                
                // If selected Prefab cannot be scaled
                else
                {
                    // Get base of rows and columns "lowest value"
                    float xBase = paintAreaStart.x < paintAreaEnd.x ? paintAreaStart.x : paintAreaEnd.x;
                    float zBase = paintAreaStart.z < paintAreaEnd.z ? paintAreaStart.z : paintAreaEnd.z;
                    
                    // Get count of rows and columns in the paint area.  Round,
                    // don't truncate: float noise "2.9999996" used to drop a
                    // row or column occasionally
                    int xCount = Mathf.RoundToInt(Mathf.Abs(paintAreaStart.x - paintAreaEnd.x) / Settings.Data.gui.grid.xzUnitSize);
                    int zCount = Mathf.RoundToInt(Mathf.Abs(paintAreaStart.z - paintAreaEnd.z) / Settings.Data.gui.grid.xzUnitSize);
                    
                    // Loop through each grid space in the area.  Occupancy is
                    // decided per piece inside PlacePrefabInScene: blocked cells
                    // are skipped and the rest still fill — and holding SHIFT
                    // bypasses the check, painting over anything
                    for (int x = 0; x <= xCount; x++)
                    {
                        for (int z = 0; z <= zCount; z++)
                        {
                            // Set visualizer position "row/column walk happens in
                            // grid space; converted to world for the transform"
                            Visualizer.GetGameObject().transform.position =
                                GridSpace.GridToWorld(new Vector3(
                                    xBase + (x * Settings.Data.gui.grid.xzUnitSize),
                                    Settings.Data.gui.grid.gridHeight * Settings.Data.gui.grid.yUnitSize,
                                    zBase + (z * Settings.Data.gui.grid.xzUnitSize)))
                                + GridSpace.GridToWorldDirection(PrefabProfile.GetOffsetPosition());

                            // Add Prefab to scene
                            Placement.PlacePrefabInScene();
                        }
                    }
                }
                
                // Delete painting area
                DeletePaintArea();
            }
            
            // Delete paint area.  DontSave objects aren't in scene root lists,
            // so this searches all in-memory objects
            public static void DeletePaintArea()
            {
                // Set painting area to false
                paintingArea = false;

                HiddenSceneObjects.DestroyAllNamed("MAST_Paint_Area_Visualizer");
                paintAreaVisualizer = null;
            }
        }
    }
}

