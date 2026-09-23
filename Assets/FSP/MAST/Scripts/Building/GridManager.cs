using System;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Building
    {
        [Serializable]
        public static class GridManager
        {
            // ---------------------------------------------------------------------------
            #region Variable Declaration
            // ---------------------------------------------------------------------------
            
            // Grid Appearance
            [SerializeField] public static bool gridExists = false;
            
            // Grid in Scene
            [SerializeField] private static GameObject gridGameObject;
            [SerializeField] private static Material gridMaterial;
            //[SerializeField] private static GameObject gridParent; // hidden in inspector with child grid left visible so it still draws gizmolines
            
            #endregion
            // ---------------------------------------------------------------------------
            
            // ---------------------------------------------------------------------------
            // Initialize
            // ---------------------------------------------------------------------------
            public static void Initialize()
            {
                
            }
            
            // ---------------------------------------------------------------------------
            #region Grid Location
            // ---------------------------------------------------------------------------
            public static void MoveGridUp()
            {
                if (DoesGridExist())
                {
                    // Move Grid Up
                    Settings.Data.gui.grid.gridHeight += 1;
                    MoveGridToNewHeight();

                    // The hide-above filter follows the grid level
                    if (ViewFilter.IsActive)
                        ViewFilter.Apply();
                }
            }

            public static void MoveGridDown()
            {
                if (DoesGridExist())
                {
                    // Move Grid Down
                    Settings.Data.gui.grid.gridHeight -= 1;
                    MoveGridToNewHeight();

                    // The hide-above filter follows the grid level
                    if (ViewFilter.IsActive)
                        ViewFilter.Apply();
                }
            }
            
            private static void MoveGridToNewHeight()
            {
                // Relink to the scene grid after domain reloads "static references
                // don't survive a recompile, which left the grid unmovable until it
                // was toggled off and on"
                if (gridGameObject == null)
                    gridGameObject = HiddenSceneObjects.FindNamed(Const.Grid.defaultName);

                // If the grid object is genuinely gone, rebuild it "CreateGrid ends
                // up back here once with a valid reference"
                if (gridGameObject == null)
                {
                    CreateGrid();
                    return;
                }

                FollowHolder();
            }

            // The grid is a plane of the MAST Holder's local space: keep its
            // pose glued to the holder's position and rotation at the current
            // grid level.  Called every scene event — the compare makes the
            // common no-change case free
            public static void FollowHolder()
            {
                // Runs every scene event, BEFORE hotkey processing — it must
                // never throw, or every scene hotkey dies with it.  Settings can
                // be transiently null during domain reloads
                if (Settings.Data.gui == null)
                    return;

                // Guard on the reference, not the gridExists flag — during
                // CreateGrid the object exists before the flag flips true
                if (gridGameObject == null)
                {
                    if (!gridExists)
                        return;

                    gridGameObject = HiddenSceneObjects.FindNamed(Const.Grid.defaultName);
                    if (gridGameObject == null)
                        return;
                }

                float gridY = Settings.Data.gui.grid.gridHeight * Settings.Data.gui.grid.yUnitSize
                    + Const.Grid.yOffsetToAvoidTearing;

                Vector3 position = GridSpace.GridToWorld(new Vector3(0f, gridY, 0f));
                Quaternion rotation = GridSpace.Rotation;

                if (gridGameObject.transform.position != position ||
                    gridGameObject.transform.rotation != rotation)
                    gridGameObject.transform.SetPositionAndRotation(position, rotation);

                // The plane's world size tracks the holder's scale "cells are
                // unit sizes times the holder scale"
                float planeScale = Settings.Data.gui.grid.cellCount * Settings.Data.gui.grid.xzUnitSize / 5f;
                var scale = new Vector3(planeScale * GridSpace.Scale.x, 1f, planeScale * GridSpace.Scale.z);

                if (gridGameObject.transform.localScale != scale)
                    gridGameObject.transform.localScale = scale;
            }
            #endregion
            // ---------------------------------------------------------------------------
            
            // ---------------------------------------------------------------------------
            #region Create/Destroy Grid
            // ---------------------------------------------------------------------------
            
            // Return if grid reference exists.  Also self-heals the static flag when
            // a domain reload cleared it while a grid still stands in the scene, so
            // the grid buttons keep working even if the window restore was skipped
            public static bool DoesGridExist()
            {
                if (!gridExists && HiddenSceneObjects.FindNamed(Const.Grid.defaultName) != null)
                    gridExists = true;

                return gridExists;
            }
            
            // Change grid visibility
            public static void ChangeGridVisibility()
            {
                if (gridExists)
                {
                    CreateGrid();
                }
                else
                {
                    DestroyGrid();
                    
                    // Deselect draw tool and palette item and destroy any active prefab visualizer
                    MAST.EditorUI.ToolState.DeselectPrefab();
                }
            }
            
            // Destroy any existing grid.  DontSave objects aren't in scene root
            // lists, so this searches all in-memory objects — it also covers
            // grids stranded in other loaded scenes
            public static void DestroyGrid()
            {
                HiddenSceneObjects.DestroyAllNamed(Const.Grid.defaultName);
                gridGameObject = null;

                // Remove locked layer
                UnityEditor.Tools.lockedLayers &= ~(1 << Const.Grid.gridLayer);

                gridExists = false;
            }

            // Rescue hatch for grids left behind by a crash or an old MAST version
            [MenuItem("Tools/MAST/Remove Leftover Grid", false, 17)]
            private static void RemoveLeftoverGrid()
            {
                DestroyGrid();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            // Create grid
            public static void CreateGrid()
            {
                CreateLinkToGrid();

                // Lock the layer the grid is on "preserving other locked layers"
                UnityEditor.Tools.lockedLayers |= 1 << Const.Grid.gridLayer;

                gridExists = true;
            }
            
            // Create link to any grid that exists, or create a new grid
            private static void CreateLinkToGrid()
            {
                DestroyGrid();
                CreateNewGrid();
            }
            
            // ---------------------------------------------------------------------------
            // Create a New Grid in the Hierarchy from the Grid Prefab
            // ---------------------------------------------------------------------------
            static void CreateNewGrid()
            {
                // Create new Grid GameObject
                gridGameObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                gridGameObject.transform.position = new Vector3(0f, 0f, 0f);
                gridGameObject.name = Const.Grid.defaultName;
                gridGameObject.layer = Const.Grid.gridLayer;
                
                // Configure Grid GameObject MeshRenderer
                MeshRenderer gridMeshRenderer = gridGameObject.GetComponent<MeshRenderer>();
                gridMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                gridMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                gridMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gridMeshRenderer.receiveShadows = false;
                
                // Configure Grid GameObject Material
                if (gridMaterial == null)
                {
                    gridMaterial = LoadingHelper.GetGridMaterial();
                }
                gridMaterial.SetColor("_Color", Settings.Data.gui.grid.tintColor);
                gridMeshRenderer.material = gridMaterial;
                
                // Add MAST_Grid_Component script to grid and pass grid preferences to it
                UpdateGridSettings();
                
                // Return the grid to its last saved height
                MoveGridToNewHeight();
                
                // Hidden from the hierarchy and NEVER saved into the user's
                // scene "HideInHierarchy alone still serializes"
                gridGameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }
            #endregion
            // ---------------------------------------------------------------------------
            
            // ---------------------------------------------------------------------------
            #region Grid Settings
            // ---------------------------------------------------------------------------
            public static void UpdateGridSettings()
            {
                if (gridGameObject != null)
                {
                    // Scale plane and texture to match new grid size "the holder
                    // scale multiplies the plane; the cell count is unchanged"
                    float cellCount = Settings.Data.gui.grid.cellCount;
                    float planeScale = cellCount * Settings.Data.gui.grid.xzUnitSize / 5f;
                    gridGameObject.transform.localScale =
                        new Vector3(planeScale * GridSpace.Scale.x,
                        1f,
                        planeScale * GridSpace.Scale.z);
                    gridMaterial.SetTextureScale("_GridTexture", new Vector2(cellCount / 2f, cellCount / 2f));
                    
                    // Update grid color tint
                    gridMaterial.SetColor("_Tint", Settings.Data.gui.grid.tintColor);
                    
                    // Apply updated grid material
                    MeshRenderer gridMeshRenderer = gridGameObject.GetComponent<MeshRenderer>();
                    gridMeshRenderer.sharedMaterial = gridMaterial;
                }
            }
            #endregion
            // ---------------------------------------------------------------------------
        }
    }
}

