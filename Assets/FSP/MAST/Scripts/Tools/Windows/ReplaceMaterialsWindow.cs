using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace MAST
{
    namespace Tools
    {
        namespace Windows
        {
            // Replace Materials window (spec: TODO.md).  Pick a prefab folder, see
            // every unique material used across its prefabs, choose replacements
            // ("Unchanged" when empty), then finalize to rewrite all prefabs.
            public class ReplaceMaterialsWindow : EditorWindow
            {
                private readonly ReplaceMaterialsOperation operation = new ReplaceMaterialsOperation();

                private Label folderLabel;
                private VisualElement materialRows;
                private Label summaryLabel;
                private Button finalizeButton;

                public static void Open()
                {
                    var window = GetWindow<ReplaceMaterialsWindow>(false, "MAST Replace Materials");
                    window.minSize = new Vector2(460, 360);
                }

                private void CreateGUI()
                {
                    var root = rootVisualElement;
                    EditorUI.WindowChrome.InitializeRoot(root);

                    // ----------------------------------------------
                    // Source folder
                    // ----------------------------------------------
                    var folderSection = EditorUI.WindowChrome.Section(root);
                    folderSection.Add(EditorUI.WindowChrome.BoldLabel("Prefabs"));
                    folderSection.Add(new Button(SelectFolder) { text = "Select Prefabs Folder" });

                    folderLabel = new Label("(no folder selected)");
                    folderLabel.AddToClassList("mast-wrap-label");
                    folderSection.Add(folderLabel);

                    // ----------------------------------------------
                    // Materials
                    // ----------------------------------------------
                    var materialsSection = EditorUI.WindowChrome.Section(root);
                    materialsSection.style.flexGrow = 1;
                    materialsSection.style.flexShrink = 1;   // the scrolling section absorbs shortage
                    materialsSection.Add(EditorUI.WindowChrome.BoldLabel("Materials  (empty replacement = Unchanged)"));

                    var scroll = new ScrollView(ScrollViewMode.Vertical);
                    scroll.style.flexGrow = 1;
                    materialRows = new VisualElement();
                    scroll.Add(materialRows);
                    materialsSection.Add(scroll);

                    // ----------------------------------------------
                    // Footer
                    // ----------------------------------------------
                    var footer = EditorUI.WindowChrome.Section(root);

                    summaryLabel = new Label("Select a prefab folder to begin.");
                    summaryLabel.AddToClassList("mast-wrap-label");
                    footer.Add(summaryLabel);

                    finalizeButton = new Button(FinalizeReplacements) { text = "Finalize Material Replacements" };
                    footer.Add(finalizeButton);

                    RefreshAll();
                }

                // ------------------------------------------------------------------
                // Actions
                // ------------------------------------------------------------------

                private void SelectFolder()
                {
                    string chosenPath = EditorUtility.OpenFolderPanel(
                        "Choose the Folder that Contains your Prefabs",
                        MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(MAST.EditorUI.DataManager.state.prefabPath), "");

                    if (chosenPath == "")
                        return;

                    operation.prefabFolder = MAST.LoadingHelper.ConvertAbsolutePathToProjectPath(chosenPath);
                    operation.Scan();
                    RefreshAll();
                }

                private void FinalizeReplacements()
                {
                    int replacementCount = operation.ReplacementCount();

                    if (!EditorUtility.DisplayDialog("Finalize Material Replacements?",
                        "This will replace " + replacementCount + " material(s) in all "
                        + operation.PrefabCount + " prefab(s) in\n" + operation.prefabFolder
                        + "\n\nThe prefab assets are modified in place — this cannot be undone.",
                        "Replace Materials", "Cancel"))
                        return;

                    operation.Run(out string report);
                    EditorUtility.DisplayDialog("MAST Replace Materials", report, "OK");

                    // Rescan so the list reflects the new state
                    operation.Scan();
                    RefreshAll();
                }

                // ------------------------------------------------------------------
                // UI refresh
                // ------------------------------------------------------------------

                private void RefreshAll()
                {
                    if (folderLabel == null)
                        return;

                    folderLabel.text = string.IsNullOrEmpty(operation.prefabFolder)
                        ? "(no folder selected)"
                        : "▸  " + operation.prefabFolder + "   ·   " + operation.PrefabCount + " prefabs";

                    RebuildMaterialRows();
                    RefreshSummary();
                }

                private void RebuildMaterialRows()
                {
                    materialRows.Clear();

                    foreach (ReplaceMaterialsOperation.MaterialRule rule in operation.rules)
                    {
                        ReplaceMaterialsOperation.MaterialRule capturedRule = rule;

                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.alignItems = Align.Center;
                        row.style.marginBottom = 2;

                        var icon = new Image
                        {
                            image = AssetPreview.GetMiniThumbnail(rule.original),
                            scaleMode = ScaleMode.ScaleToFit
                        };
                        icon.style.width = 18;
                        icon.style.height = 18;
                        icon.style.flexShrink = 0;
                        row.Add(icon);

                        var nameLabel = new Label(rule.original.name);
                        nameLabel.style.width = Length.Percent(35);
                        nameLabel.style.overflow = Overflow.Hidden;
                        nameLabel.style.marginLeft = 4;
                        row.Add(nameLabel);

                        var arrow = new Label("→");
                        arrow.style.flexShrink = 0;
                        arrow.style.marginRight = 4;
                        row.Add(arrow);

                        var replacementField = new ObjectField
                        {
                            objectType = typeof(Material),
                            allowSceneObjects = false,
                            value = rule.replacement
                        };
                        replacementField.style.flexGrow = 1;
                        replacementField.RegisterValueChangedCallback(evt =>
                        {
                            capturedRule.replacement = evt.newValue as Material;
                            RefreshSummary();
                        });
                        row.Add(replacementField);

                        materialRows.Add(row);
                    }
                }

                private void RefreshSummary()
                {
                    if (string.IsNullOrEmpty(operation.prefabFolder))
                    {
                        summaryLabel.text = "Select a prefab folder to begin.";
                        finalizeButton.SetEnabled(false);
                        return;
                    }

                    int replacementCount = operation.ReplacementCount();
                    finalizeButton.SetEnabled(replacementCount > 0);

                    summaryLabel.text = replacementCount == 0
                        ? operation.rules.Count + " unique material(s) found.  Choose replacements to enable Finalize."
                        : replacementCount + " of " + operation.rules.Count
                            + " material(s) will be replaced across " + operation.PrefabCount + " prefab(s).";
                }

            }
        }
    }
}
