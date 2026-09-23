using UnityEditor;


namespace MAST
{
    namespace EditorUI
    {
        // Single owner of MAST's tool, palette-selection, and drag state.
        //
        // Storage stays in the existing serialized fields (Settings.Data.gui.toolbar.*,
        // DataManager.state.previous*ToolIndex, palette selectedItemIndex) so saved
        // state and assets are unchanged — but every read/write now goes through here, the
        // int<->enum mapping exists in exactly one place, and each transition performs its
        // side effects (placement mode, visualizer, previews, drag cleanup, repaint) once.
        //
        // Nothing outside this class should touch selectedDrawToolIndex,
        // selectedPaintToolIndex, or previous*ToolIndex directly.
        public static class ToolState
        {
            // ------------------------------------------------------------------
            // Active build tool (Build tab)
            // ------------------------------------------------------------------

            // selectedDrawToolIndex is 0-based (0=DrawSingle..5=Greeble, -1=none);
            // BuildMode is the same order shifted by one (None=0, DrawSingle=1..)
            public static BuildMode ActiveBuildTool
            {
                get
                {
                    int index = MAST.Settings.Data.gui.toolbar.selectedDrawToolIndex;
                    return (index < 0 || index > 5) ? BuildMode.None : (BuildMode)(index + 1);
                }
            }

            public static void SetBuildTool(BuildMode mode)
            {
                MAST.Settings.Data.gui.toolbar.selectedDrawToolIndex = (int)mode - 1;
                MAST.Building.ToolTransitions.ApplyBuildToolChange(mode);

                if (mode == BuildMode.None)
                {
                    CancelBuildDrag();

                    // Turning the tool off deselects the palette item too — a
                    // highlighted prefab with no tool is a half-armed state.
                    // Set the index directly: DeselectPrefab calls back here
                    MAST.Building.Palette.PrefabPalette.selectedItemIndex = -1;
                }

                SceneView.RepaintAll();
            }

            // Click/hotkey semantics: choosing the active tool again deselects it
            public static void ToggleBuildTool(BuildMode mode)
            {
                SetBuildTool(ActiveBuildTool == mode ? BuildMode.None : mode);
            }

            // ------------------------------------------------------------------
            // Active paint tool (Paint tab)
            // ------------------------------------------------------------------

            public static PaintTool ActivePaintTool
            {
                get
                {
                    int index = MAST.Settings.Data.gui.toolbar.selectedPaintToolIndex;
                    return (index < (int)PaintTool.None || index > (int)PaintTool.ReplaceAll)
                        ? PaintTool.None : (PaintTool)index;
                }
            }

            public static void SetPaintTool(PaintTool tool)
            {
                MAST.Settings.Data.gui.toolbar.selectedPaintToolIndex = (int)tool;

                if (tool == PaintTool.None)
                {
                    CancelPaintDrag();
                    MAST.Painting.Painter.ClearCurrentMaterialPaintPreview();

                    // Same rule as the build tools: turning the tool off
                    // deselects the palette material
                    MAST.Painting.Palette.MaterialPalette.selectedItemIndex = -1;
                }

                SceneView.RepaintAll();
            }

            public static void TogglePaintTool(PaintTool tool)
            {
                SetPaintTool(ActivePaintTool == tool ? PaintTool.None : tool);
            }

            // ------------------------------------------------------------------
            // Prefab palette selection (drives the placement pipeline)
            // ------------------------------------------------------------------

            public static int SelectedPrefabIndex => MAST.Building.Palette.PrefabPalette.selectedItemIndex;

            public static void SelectPrefab(int index)
            {
                MAST.Building.Palette.PrefabPalette.selectedItemIndex = index;

                // With no tool (or the eraser) active, enable the grid and arm Draw Single
                if (ActiveBuildTool == BuildMode.None || ActiveBuildTool == BuildMode.Erase)
                {
                    if (!MAST.Building.GridManager.DoesGridExist())
                    {
                        MAST.Building.GridManager.gridExists = true;
                        MAST.Building.GridManager.ChangeGridVisibility();
                    }

                    SetBuildTool(BuildMode.DrawSingle);
                }

                // The randomizer rolls a fresh seed for the new prefab without replacing
                if (ActiveBuildTool == BuildMode.Randomize)
                    MAST.Building.Randomizer.GenerateNewRandomSeed(true);

                // Show the new prefab in the placement visualizer
                if (ActiveBuildTool != BuildMode.Erase)
                    MAST.Building.ToolTransitions.ApplySelectedPrefabChange();

                SceneView.RepaintAll();
            }

            public static void DeselectPrefab()
            {
                MAST.Building.Palette.PrefabPalette.selectedItemIndex = -1;
                SetBuildTool(BuildMode.None);
                MAST.Building.Visualizer.RemoveVisualizer();
            }

            public static void TogglePrefabSelection(int index)
            {
                if (index == SelectedPrefabIndex)
                    DeselectPrefab();
                else
                    SelectPrefab(index);
            }

            // Shift + scrollwheel cycling in the SceneView.  Routed through
            // SelectPrefab so cycling gets the SAME side effects as clicking
            // "grid on, tool armed, randomizer reseeded, visualizer updated"
            public static void CyclePrefabSelection(int direction)
            {
                int count = MAST.Building.Palette.PrefabPalette.GetPrefabArray().Length;
                if (count == 0)
                    return;

                int index = SelectedPrefabIndex + direction;
                if (index >= count) index = 0;
                if (index < 0) index = count - 1;

                SelectPrefab(index);
            }

            // ------------------------------------------------------------------
            // Material palette selection (arms the paint tool)
            // ------------------------------------------------------------------

            public static int SelectedMaterialIndex => MAST.Painting.Palette.MaterialPalette.selectedItemIndex;

            public static void SelectMaterial(int index)
            {
                MAST.Painting.Palette.MaterialPalette.selectedItemIndex = index;

                // Picking a material arms Paint — unless Replace All is active,
                // which also consumes the selection "changing the replacement
                // material must not kick the user out of the tool"
                if (ActivePaintTool != PaintTool.ReplaceAll)
                    SetPaintTool(PaintTool.Paint);
                else
                    SceneView.RepaintAll();
            }

            // clearAnyPaintTool: palette deselection disarms the material-consuming
            // tools (Restore needs no material); the deselect hotkey and global
            // cleanup clear any tool.
            public static void DeselectMaterial(bool clearAnyPaintTool = false)
            {
                MAST.Painting.Palette.MaterialPalette.selectedItemIndex = -1;

                if (clearAnyPaintTool ||
                    ActivePaintTool == PaintTool.Paint ||
                    ActivePaintTool == PaintTool.ReplaceAll)
                    SetPaintTool(PaintTool.None);
            }

            public static void ToggleMaterialSelection(int index)
            {
                if (index == SelectedMaterialIndex)
                    DeselectMaterial();
                else
                    SelectMaterial(index);
            }

            // ------------------------------------------------------------------
            // Drag state (mouse held while drawing/painting/erasing in the SceneView)
            // ------------------------------------------------------------------

            // Drags are TRANSIENT: persisting them "the old serialized fields"
            // meant a crash mid paint-drag restored an armed drag next session,
            // whose first click-release collapsed the user's undo history
            private static BuildDrag activeBuildDrag = BuildDrag.None;
            private static PaintDrag activePaintDrag = PaintDrag.None;

            public static BuildDrag ActiveBuildDrag
            {
                get => activeBuildDrag;
                set => activeBuildDrag = value;
            }

            public static PaintDrag ActivePaintDrag
            {
                get => activePaintDrag;
                set => activePaintDrag = value;
            }

            public static void CancelBuildDrag()
            {
                if (ActiveBuildDrag == BuildDrag.PaintArea)
                    MAST.Building.PaintArea.DeletePaintArea();

                ActiveBuildDrag = BuildDrag.None;
            }

            public static void CancelPaintDrag()
            {
                ActivePaintDrag = PaintDrag.None;
            }

            // ------------------------------------------------------------------
            // Global cleanup (window closing, entering play mode)
            // ------------------------------------------------------------------

            public static void ClearAll()
            {
                DeselectPrefab();
                DeselectMaterial(clearAnyPaintTool: true);
                CancelBuildDrag();
                CancelPaintDrag();

                // Remove any paint area left behind even if no drag was tracked
                MAST.Building.PaintArea.DeletePaintArea();
            }
        }
    }
}
