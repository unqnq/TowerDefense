using UnityEditor;
using UnityEngine;


namespace MAST
{
    namespace Building
    {
        public static class ToolTransitions
        {
        // ---------------------------------------------------------------------------
        #region Change Placement Mode
        // ---------------------------------------------------------------------------
            // Side effects of a build-tool change "visualizer, eraser target,
            // draw state".  The tool itself has ONE owner: ToolState — this
            // class used to keep its own placementMode copy of it
            public static void ApplyBuildToolChange(BuildMode newPlacementMode)
            {
                // Remove any previous visualizer
                Visualizer.RemoveVisualizer();

                // If changed tool to Nothing or Eraser "the eraser targets by
                // raycast with a red highlight now — it has no visualizer"
                if (newPlacementMode == BuildMode.None || newPlacementMode == BuildMode.Erase)
                {
                    Eraser.ClearTarget();
                }

                // Any placing tool: show the selected prefab in the visualizer
                else
                {
                    // If a palette item is selected
                    if (Palette.PrefabPalette.selectedItemIndex != -1)
                    {
                        // Create visualizer from selected item in the palette
                        ApplySelectedPrefabChange();

                        // If changed tool to Randomizer
                        if (newPlacementMode == BuildMode.Randomize)
                        {
                            // Make a new random seed
                            Randomizer.GenerateNewRandomSeed();
                        }
                    }
                }

                // If Draw Continuous nor Paint Area tools are selected
                if (newPlacementMode != BuildMode.DrawContinuous &&
                    newPlacementMode != BuildMode.PaintArea)
                {
                    // Delete last saved position
                    Placement.lastPosition = Vector3.positiveInfinity;

                    // Remove any paint area visualization
                    PaintArea.DeletePaintArea();
                }

            }
        #endregion
            
            // Change visualizer prefab when a new item is selected in the palette menu
            public static void ApplySelectedPrefabChange()
            {
                // A remembered selection can point past the current palette
                // "folder contents changed between sessions" — drop it instead
                // of crashing the window restore
                GameObject selectedPrefab = Palette.PrefabPalette.GetSelectedPrefab();
                if (selectedPrefab == null)
                {
                    Palette.PrefabPalette.selectedItemIndex = -1;
                    Visualizer.RemoveVisualizer();
                    return;
                }

                // Remove any existing visualizer
                Visualizer.RemoveVisualizer();

                // Create a new visualizer
                Visualizer.CreateVisualizer(selectedPrefab);
            }
            
        }
    }
}

