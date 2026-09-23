using UnityEngine;


namespace MAST
{
    namespace Building
    {
        public static class Visualizer
        {
            [SerializeField] public static GameObject visualizerGameObject = null;
            [SerializeField] public static bool visualizerOnGrid = false;
            
            // Is the mouse pointer in the sceneview
            [SerializeField] public static bool pointerInSceneview = false;
            
            public static GameObject GetGameObject()
            {
                return visualizerGameObject;
            }
            public static void SetGameObject(GameObject newVisualizer)
            {
                visualizerGameObject = newVisualizer;
            }
            
            // Create the visualizer GameObject
            public static void CreateVisualizer(GameObject selectedPrefab)
            {
                // Exit without creating, if no Prefab is selected in the palette
                if (selectedPrefab == null)
                    return;
                
                // Create a new visualizer
                visualizerGameObject = GameObject.Instantiate(selectedPrefab);
                SetLayerRecursively(visualizerGameObject.transform, Const.Placement.visualizerLayer);

                // The visualizer isn't a child of the holder, so it applies the
                // holder's scale itself — placed prefabs inherit it as children
                visualizerGameObject.transform.localScale =
                    Vector3.Scale(selectedPrefab.transform.localScale, GridSpace.Scale);
                
                // Name it "MAST_Visualizer" incase it needs to be found later for deletion
                visualizerGameObject.name = "MAST_Visualizer";
                
                // If not selecting the Eraser
                if (MAST.EditorUI.ToolState.ActiveBuildTool != BuildMode.Erase)
                    visualizerGameObject.transform.rotation = GetPlacementFrameRotation(selectedPrefab);
                
                // Unselectable, hidden from the hierarchy, and NEVER saved into
                // the user's scene "HideInHierarchy alone still serializes"
                visualizerGameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }
            
            // ---------------------------------------------------------------------------
            // Recursively loop through all children and set their layers
            // ---------------------------------------------------------------------------
            private static void SetLayerRecursively(Transform transform, int layer)
            {
                transform.gameObject.layer = layer;
                
                foreach (Transform childTransform in transform)
                    SetLayerRecursively(childTransform, layer);
            }
            
            // Visualizer orientation: the saved rotation "or the prefab's own
            // when none is saved" composed with the placement frame.  The frame
            // applies EVEN WHEN no rotation has been saved yet — the old gate
            // skipped the whole assignment, so a fresh selection ignored the
            // holder's rotation entirely even with Rotate With Holder on
            private static Quaternion GetPlacementFrameRotation(GameObject selectedPrefab)
            {
                Quaternion baseRotation = IsSavedRotationValidForVisualizer()
                    ? Manipulate.GetCurrentRotation()
                    : selectedPrefab.transform.rotation;

                return GridSpace.PlacementRotation * baseRotation;
            }

            // See if new selected Prefab will allow the saved rotation
            private static bool IsSavedRotationValidForVisualizer()
            {
                // If there is a saved rotation
                if (Manipulate.GetCurrentRotation() != null)
                {
                    // If the current saved rotation is allowed by the prefab
                    //   (If allowed rotation divides evenly into current rotation)
                    //       or (Both allowed and current rotations are set to 0)
                    if (((int)(Manipulate.GetCurrentRotation().eulerAngles.x % PrefabProfile.GetRotationStep().x) == 0)
                    || (int)Manipulate.GetCurrentRotation().eulerAngles.x == 0 && (int)PrefabProfile.GetRotationStep().x == 0)
                        if (((int)(Manipulate.GetCurrentRotation().eulerAngles.y % PrefabProfile.GetRotationStep().y) == 0)
                        || (int)Manipulate.GetCurrentRotation().eulerAngles.y == 0 && (int)PrefabProfile.GetRotationStep().y == 0)
                            if (((int)(Manipulate.GetCurrentRotation().eulerAngles.z % PrefabProfile.GetRotationStep().z) == 0)
                            || (int)Manipulate.GetCurrentRotation().eulerAngles.z == 0 && (int)PrefabProfile.GetRotationStep().z == 0)
                            {
                                // Return true, since saved rotation is allowed
                                return true;
                            }
                }
                // Return false, since saved rotation is not allowed
                return false;
            }
            
            // Destroy current visualizer GameObject.  DontSave objects don't
            // appear in scene root lists, so this searches ALL in-memory
            // objects "the old scene scan left a frozen ghost behind"
            public static void RemoveVisualizer()
            {
                HiddenSceneObjects.DestroyAllNamed("MAST_Visualizer");
                visualizerGameObject = null;
            }
            
            // Change visualizer prefab visibility
            // Make prefab visible or invisible if pointer is in scene view on grid
            public static void SetVisualizerVisibility(bool visible)
            {
                pointerInSceneview = visible;
                
                if (visualizerGameObject != null)
                    visualizerGameObject.SetActive(visible);
            }
            
            // Moves the visualizer prefab to a position based on the current mouse position
            public static void UpdateVisualizerPosition()
            {
                // If a tool is selected
                if (MAST.EditorUI.ToolState.ActiveBuildTool != BuildMode.None)
                    
                    // If visualizer exists
                    if (visualizerGameObject != null)
                    {
                        // Update visualizer position from pointer location on grid
                        visualizerGameObject.transform.position =
                            GridPointer.GetPlacementPosition();
                        
                        // If Eraser tool is not selected
                        if (MAST.EditorUI.ToolState.ActiveBuildTool != BuildMode.Erase)
                        {
                            // Keep orientation live "follows the Rotate With
                            // Holder toggle and holder rotation changes without
                            // reselecting the prefab; the Randomizer overrides
                            // it below with its own composed rotation"
                            GameObject selectedPrefab = Palette.PrefabPalette.GetSelectedPrefab();
                            if (selectedPrefab != null)
                                visualizerGameObject.transform.rotation =
                                    GetPlacementFrameRotation(selectedPrefab);

                            // Apply position offset "a grid-space vector"
                            visualizerGameObject.transform.position +=
                                GridSpace.GridToWorldDirection(PrefabProfile.GetOffsetPosition());

                            // If Randomizer is selected
                            if (MAST.EditorUI.ToolState.ActiveBuildTool == BuildMode.Randomize)
                                // If Prefab in randomizable, apply Randomizer to transform
                                if (PrefabProfile.Randomizer.GetUseRandomizer())
                                    visualizerGameObject = Randomizer.ApplyRandomizerToTransform(
                                        visualizerGameObject, Manipulate.GetCurrentRotation());
                        }
                        
                        // Set visualizer visibility based on if mouse over grid
                        if (pointerInSceneview)
                            visualizerGameObject.SetActive(visualizerOnGrid);
                    }
            }
        }
    }
}

