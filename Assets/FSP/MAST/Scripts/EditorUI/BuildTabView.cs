using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        // UI Toolkit version of the Build tab: grid/draw/manipulate toolbar + prefab palette.
        // Selecting a palette item drives the placement pipeline (grid on, draw tool, visualizer)
        // through ToolState, the single owner of tool and selection state.
        public class BuildTabView : VisualElement
        {
            private readonly System.Func<float> getIconSize;

            private readonly VisualElement toolbar;
            private readonly PaletteGridView palette;

            private readonly Button gridUpButton;
            private readonly Button gridToggleButton;
            private readonly Button gridDownButton;
            private readonly Button snapButton;
            private readonly Button hideAboveButton;
            private readonly Button[] toolButtons = new Button[6];
            private static readonly BuildMode[] toolModes =
            {
                BuildMode.DrawSingle, BuildMode.DrawContinuous, BuildMode.PaintArea,
                BuildMode.Randomize, BuildMode.Greeble, BuildMode.Erase
            };
            private readonly Button rotateButton;
            private readonly Button rotateAxisButton;
            private readonly Button flipButton;
            private readonly Button flipAxisButton;
            private readonly Button loadButton;

            private readonly Texture2D iconAxisX;
            private readonly Texture2D iconAxisY;
            private readonly Texture2D iconAxisZ;

            private float lastIconSize = -1;
            private ToolbarPos lastToolbarPos = (ToolbarPos)(-1);

            public BuildTabView(System.Func<float> getIconSize)
            {
                this.getIconSize = getIconSize;

                AddToClassList("mast-tab-body");

                iconAxisX = MAST.LoadingHelper.GetImage("Axis_X.png");
                iconAxisY = MAST.LoadingHelper.GetImage("Axis_Y.png");
                iconAxisZ = MAST.LoadingHelper.GetImage("Axis_Z.png");

                // ------------------------------------------------------------------
                // Toolbar
                // ------------------------------------------------------------------
                toolbar = new VisualElement();
                toolbar.AddToClassList("mast-toolbar");

                // Grid group: up / toggle / down
                var gridGroup = NewGroup();
                gridUpButton = IconButton(gridGroup, "Grid_Up.png", MAST.Building.GridManager.MoveGridUp, half: true);
                gridToggleButton = IconButton(gridGroup, "Grid_Toggle.png", () =>
                {
                    MAST.Building.GridManager.gridExists = !MAST.Building.GridManager.gridExists;
                    MAST.Building.GridManager.ChangeGridVisibility();
                });
                gridDownButton = IconButton(gridGroup, "Grid_Down.png", MAST.Building.GridManager.MoveGridDown, half: true);

                // Grid snap group
                var snapGroup = NewGroup();
                snapButton = IconButton(snapGroup, "Grid_Snap.png", () =>
                {
                    MAST.Settings.Data.placement.snapToGrid = !MAST.Settings.Data.placement.snapToGrid;
                });

                // View group: hide prefabs above the cutoff height
                var viewGroup = NewGroup();
                hideAboveButton = IconButton(viewGroup, "Hide_Above.png", MAST.Building.ViewFilter.Toggle);

                // Draw tool group: single, continuous, paint area, randomizer, greeble, eraser
                var toolGroup = NewGroup();
                string[] toolIcons = { "Pencil.png", "Paint_Roller.png", "Paint_Bucket.png", "Randomizer.png", "Greeble.png", "Eraser.png" };
                string[] toolTips = { "Draw Single Tool", "Draw Continuous Tool", "Paint Area Tool", "Randomizer Tool", "Greeble Tool - place on model faces", "Eraser Tool" };
                for (int i = 0; i < toolModes.Length; i++)
                {
                    BuildMode mode = toolModes[i];
                    toolButtons[i] = IconButton(toolGroup, toolIcons[i], () => ToolState.ToggleBuildTool(mode));
                    toolButtons[i].tooltip = toolTips[i];
                }

                // Manipulate group: rotate + axis, flip + axis
                var manipulateGroup = NewGroup();
                rotateButton = IconButton(manipulateGroup, "Rotate.png", () => MAST.Building.Manipulate.RotateObject());
                rotateButton.tooltip = "Rotate Prefab/Selection";
                rotateAxisButton = IconButton(manipulateGroup, "Axis_Y.png", MAST.Building.Manipulate.ToggleRotateAxis, half: true);
                rotateAxisButton.tooltip = "Change Rotate Axis";
                flipButton = IconButton(manipulateGroup, "Flip.png", () => MAST.Building.Manipulate.FlipObject());
                flipButton.tooltip = "Flip Prefab/Selection";
                flipAxisButton = IconButton(manipulateGroup, "Axis_X.png", MAST.Building.Manipulate.ToggleFlipAxis, half: true);
                flipAxisButton.tooltip = "Change Flip Axis";

                // Misc group: load prefab folder
                var miscGroup = NewGroup();
                loadButton = IconButton(miscGroup, "Load_From_Folder.png", LoadPrefabFolder);
                loadButton.tooltip = "Load prefabs from a project folder";

                Add(toolbar);

                // ------------------------------------------------------------------
                // Prefab palette
                // ------------------------------------------------------------------
                palette = new PaletteGridView(new PaletteGridView.Config
                {
                    emptyMessage = "No prefabs to display!  Select your prefabs folder and click Load Prefabs",
                    isReady = MAST.Building.Palette.PrefabPalette.IsReady,
                    getFolderNames = MAST.Building.Palette.PrefabPalette.GetFolderNameArray,
                    getFolderIndex = () => MAST.Building.Palette.PrefabPalette.selectedFolderIndex,
                    onFolderChanged = index =>
                    {
                        ToolState.DeselectPrefab();
                        MAST.Building.Palette.PrefabPalette.ChangeActivePaletteFolder(index);
                    },
                    getItems = MAST.Building.Palette.PrefabPalette.GetGUIContentArray,
                    getSelectedIndex = () => ToolState.SelectedPrefabIndex,
                    // Re-clicking the selected prefab deselects; otherwise select and
                    // kick off the placement pipeline (grid, tool, visualizer)
                    onItemClicked = ToolState.TogglePrefabSelection,
                    getColumnCount = () => MAST.EditorUI.DataManager.state.prefabPaletteColumnCount,
                    setColumnCount = value => MAST.EditorUI.DataManager.state.prefabPaletteColumnCount = value,
                    getBGColor = () => MAST.Settings.Data.gui.palette.bgColor
                });
                Add(palette);

                schedule.Execute(Sync).Every(150);
                Sync();
            }

            // ----------------------------------------------------------------------
            // Reconcile visuals with tool state that can change from hotkeys/scene
            // ----------------------------------------------------------------------
            private void Sync()
            {
                // Icon sizing follows window height and the toolbar scale setting
                float iconSize = Mathf.Max(24f, getIconSize());
                if (!Mathf.Approximately(iconSize, lastIconSize))
                {
                    lastIconSize = iconSize;
                    SetButtonSize(gridUpButton, iconSize, half: true);
                    SetButtonSize(gridToggleButton, iconSize);
                    SetButtonSize(gridDownButton, iconSize, half: true);
                    SetButtonSize(snapButton, iconSize);
                    foreach (var button in toolButtons)
                        SetButtonSize(button, iconSize);
                    SetButtonSize(rotateButton, iconSize);
                    SetButtonSize(rotateAxisButton, iconSize, half: true);
                    SetButtonSize(flipButton, iconSize);
                    SetButtonSize(flipAxisButton, iconSize, half: true);
                    SetButtonSize(loadButton, iconSize);
                    SetButtonSize(hideAboveButton, iconSize);
                }

                // Toolbar side (Settings > GUI > toolbar position)
                ToolbarPos toolbarPos = MAST.Settings.Data.gui.toolbar.position;
                if (toolbarPos != lastToolbarPos)
                {
                    lastToolbarPos = toolbarPos;
                    toolbar.RemoveFromHierarchy();
                    if (toolbarPos == ToolbarPos.Left)
                        Insert(0, toolbar);
                    else
                        Add(toolbar);
                }

                // Grid buttons
                int gridHeight = MAST.Settings.Data.gui.grid.gridHeight;
                gridUpButton.tooltip = "Move Grid Up to " + (gridHeight + 1);
                gridToggleButton.tooltip = "Toggle Scene Grid - Current Level " + gridHeight;
                gridDownButton.tooltip = "Move Grid Down to " + (gridHeight - 1);
                gridToggleButton.EnableInClassList("mast-tool-button--active", MAST.Building.GridManager.gridExists);

                // Snap toggle
                bool snap = MAST.Settings.Data.placement.snapToGrid;
                snapButton.tooltip = snap ? "Turn OFF Grid Snap" : "Turn ON Grid Snap";
                snapButton.EnableInClassList("mast-tool-button--active", snap);

                // Hide-above toggle
                bool hideAbove = MAST.Building.ViewFilter.IsActive;
                hideAboveButton.tooltip = (hideAbove ? "Show" : "Hide") + " prefabs above the current grid level ("
                    + MAST.Settings.Data.gui.grid.gridHeight + ")  -  offset in Settings > GUI > View Settings";
                hideAboveButton.EnableInClassList("mast-tool-button--active", hideAbove);

                // Draw tool selection
                BuildMode activeTool = ToolState.ActiveBuildTool;
                for (int i = 0; i < toolButtons.Length; i++)
                    toolButtons[i].EnableInClassList("mast-tool-button--active", toolModes[i] == activeTool);

                // Rotate/flip axis icons
                rotateAxisButton.style.backgroundImage =
                    new StyleBackground(AxisIcon(MAST.Building.Manipulate.GetCurrentRotateAxis()));
                flipAxisButton.style.backgroundImage =
                    new StyleBackground(AxisIcon(MAST.Building.Manipulate.GetCurrentFlipAxis()));
            }

            private static void LoadPrefabFolder()
            {
                string chosenPath = EditorUtility.OpenFolderPanel(
                    "Choose the Folder that Contains your Prefabs",
                    MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(MAST.EditorUI.DataManager.state.prefabPath), "");

                if (chosenPath != "")
                {
                    // A selection index from the old folder would place a
                    // different prefab than the palette highlights
                    ToolState.DeselectPrefab();

                    MAST.Building.Palette.PrefabPalette.GenerateThumbnailsAndLoadPrefabs(
                        chosenPath, 0, MAST.Settings.Data.gui.palette.overwriteThumbnails);

                    AssetDatabase.Refresh();

                    MAST.EditorUI.DataManager.state.prefabPath =
                        MAST.LoadingHelper.ConvertAbsolutePathToProjectPath(chosenPath);
                    MAST.EditorUI.DataManager.state.selectedPrefabPaletteFolderIndex =
                        MAST.Building.Palette.PrefabPalette.selectedFolderIndex;

                    MAST.EditorUI.DataManager.Save_Changes_To_Disk();
                }
            }

            private Texture2D AxisIcon(Axis axis)
            {
                switch (axis)
                {
                    case Axis.X: return iconAxisX;
                    case Axis.Z: return iconAxisZ;
                    default: return iconAxisY;
                }
            }

            private VisualElement NewGroup()
            {
                var group = new VisualElement();
                group.AddToClassList("mast-toolbar-group");
                toolbar.Add(group);
                return group;
            }

            private static Button IconButton(VisualElement parent, string iconFile, System.Action onClick, bool half = false)
            {
                var button = new Button(onClick) { text = "" };
                button.AddToClassList("mast-tool-button");
                Texture2D icon = MAST.LoadingHelper.GetImage(iconFile);
                if (icon != null)
                    button.style.backgroundImage = new StyleBackground(icon);
                parent.Add(button);
                return button;
            }

            private static void SetButtonSize(Button button, float iconSize, bool half = false)
            {
                button.style.width = iconSize;
                button.style.height = half ? iconSize / 2f : iconSize;
            }
        }
    }
}
