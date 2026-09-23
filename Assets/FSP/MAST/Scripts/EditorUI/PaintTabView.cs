using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        // UI Toolkit version of the Paint tab: paint/restore toolbar + material palette.
        // Selecting a material arms the Paint Material tool; the actual painting logic
        // stays in MAST.Painting.Painter, driven from the SceneView handler.
        public class PaintTabView : VisualElement
        {
            private readonly System.Func<float> getIconSize;

            private readonly VisualElement toolbar;
            private readonly PaletteGridView palette;

            private readonly Button[] toolButtons = new Button[3];
            private static readonly PaintTool[] toolTools = { PaintTool.Paint, PaintTool.Restore, PaintTool.ReplaceAll };
            private readonly Button loadButton;

            private float lastIconSize = -1;
            private ToolbarPos lastToolbarPos = (ToolbarPos)(-1);

            public PaintTabView(System.Func<float> getIconSize)
            {
                this.getIconSize = getIconSize;

                AddToClassList("mast-tab-body");

                // ------------------------------------------------------------------
                // Toolbar
                // ------------------------------------------------------------------
                toolbar = new VisualElement();
                toolbar.AddToClassList("mast-toolbar");

                // Paint tool group: paint material, restore material
                var toolGroup = new VisualElement();
                toolGroup.AddToClassList("mast-toolbar-group");
                toolbar.Add(toolGroup);

                string[] toolIcons = { "Paint_Brush.png", "Cleaning_Brush.png", "Paint_Bucket.png" };
                string[] toolTips = { "Paint Material Tool", "Restore Material Tool",
                    "Replace All Instances Tool - click a face to swap every instance of its material across the same MAST Holder" };
                for (int i = 0; i < 3; i++)
                {
                    PaintTool tool = toolTools[i];
                    toolButtons[i] = new Button(() => ToolState.TogglePaintTool(tool)) { text = "" };
                    toolButtons[i].AddToClassList("mast-tool-button");
                    Texture2D icon = MAST.LoadingHelper.GetImage(toolIcons[i]);
                    if (icon != null)
                        toolButtons[i].style.backgroundImage = new StyleBackground(icon);
                    toolButtons[i].tooltip = toolTips[i];
                    toolGroup.Add(toolButtons[i]);
                }

                // Misc group: load material folder
                var miscGroup = new VisualElement();
                miscGroup.AddToClassList("mast-toolbar-group");
                toolbar.Add(miscGroup);

                loadButton = new Button(LoadMaterialFolder) { text = "" };
                loadButton.AddToClassList("mast-tool-button");
                Texture2D loadIcon = MAST.LoadingHelper.GetImage("Load_From_Folder.png");
                if (loadIcon != null)
                    loadButton.style.backgroundImage = new StyleBackground(loadIcon);
                loadButton.tooltip = "Load materials from a project folder";
                miscGroup.Add(loadButton);

                Add(toolbar);

                // ------------------------------------------------------------------
                // Material palette
                // ------------------------------------------------------------------
                palette = new PaletteGridView(new PaletteGridView.Config
                {
                    emptyMessage = "No materials to display!  Select your materials folder and click Load Materials",
                    isReady = MAST.Painting.Palette.MaterialPalette.IsReady,
                    getFolderNames = MAST.Painting.Palette.MaterialPalette.GetFolderNameArray,
                    getFolderIndex = () => MAST.Painting.Palette.MaterialPalette.selectedFolderIndex,
                    onFolderChanged = index =>
                    {
                        ToolState.DeselectMaterial();
                        MAST.Painting.Palette.MaterialPalette.ChangeActivePaletteFolder(index);
                    },
                    getItems = MAST.Painting.Palette.MaterialPalette.GetGUIContentArray,
                    getSelectedIndex = () => ToolState.SelectedMaterialIndex,
                    // Re-clicking the selected material deselects; otherwise select it
                    // and arm the Paint Material tool
                    onItemClicked = ToolState.ToggleMaterialSelection,
                    getColumnCount = () => MAST.EditorUI.DataManager.state.materialPaletteColumnCount,
                    setColumnCount = value => MAST.EditorUI.DataManager.state.materialPaletteColumnCount = value,
                    getBGColor = () => MAST.Settings.Data.gui.palette.bgColor
                });
                Add(palette);

                schedule.Execute(Sync).Every(150);
                Sync();
            }

            private void Sync()
            {
                float iconSize = Mathf.Max(24f, getIconSize());
                if (!Mathf.Approximately(iconSize, lastIconSize))
                {
                    lastIconSize = iconSize;
                    foreach (var button in toolButtons)
                    {
                        button.style.width = iconSize;
                        button.style.height = iconSize;
                    }
                    loadButton.style.width = iconSize;
                    loadButton.style.height = iconSize;
                }

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

                PaintTool activeTool = ToolState.ActivePaintTool;
                for (int i = 0; i < toolButtons.Length; i++)
                    toolButtons[i].EnableInClassList("mast-tool-button--active", toolTools[i] == activeTool);
            }

            private static void LoadMaterialFolder()
            {
                string chosenPath = EditorUtility.OpenFolderPanel(
                    "Choose the Folder that Contains your Materials",
                    MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(MAST.EditorUI.DataManager.state.materialPath), "");

                if (chosenPath != "")
                {
                    // A selection index from the old folder would silently point
                    // at a different material "or turn Paint into Restore"
                    ToolState.DeselectMaterial(clearAnyPaintTool: true);

                    MAST.Painting.Palette.MaterialPalette.GenerateThumbnailsAndLoadMaterials(
                        chosenPath, 0, MAST.Settings.Data.gui.palette.overwriteThumbnails);

                    AssetDatabase.Refresh();

                    MAST.EditorUI.DataManager.state.materialPath =
                        MAST.LoadingHelper.ConvertAbsolutePathToProjectPath(chosenPath);
                    MAST.EditorUI.DataManager.state.selectedMaterialPaletteFolderIndex =
                        MAST.Painting.Palette.MaterialPalette.selectedFolderIndex;

                    MAST.EditorUI.DataManager.Save_Changes_To_Disk();
                }
            }
        }
    }
}
