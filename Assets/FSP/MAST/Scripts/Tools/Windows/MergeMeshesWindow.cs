using System.Collections.Generic;
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
            // Merge Meshes window (design: docs/design/MERGE-MESHES-WINDOW.md).
            // Per-material merge actions and edge handling; saving is handled by the
            // headless MergeMeshesOperation.
            public class MergeMeshesWindow : EditorWindow
            {
                private readonly MergeMeshesOperation operation = new MergeMeshesOperation();
                private readonly Dictionary<Material, int> triangleCounts = new Dictionary<Material, int>();

                private Label sourceLabel;
                private Label statsLabel;
                private VisualElement materialRows;
                private Toggle weldToggle;
                private FloatField weldToleranceField;
                private Toggle colliderToggle;
                private EnumField pivotField;
                private Label estimateLabel;
                private Button mergeButton;

                public static void Open()
                {
                    var window = GetWindow<MergeMeshesWindow>(false, "MAST Merge Meshes");
                    window.minSize = new Vector2(460, 420);
                    window.UseSelection();
                }

                private void CreateGUI()
                {
                    var root = rootVisualElement;
                    EditorUI.WindowChrome.InitializeRoot(root);

                    // ----------------------------------------------
                    // Source
                    // ----------------------------------------------
                    var sourceSection = EditorUI.WindowChrome.Section(root);
                    sourceSection.Add(EditorUI.WindowChrome.BoldLabel("Source"));

                    var useSelectedButton = new Button(UseSelection) { text = "Use Selected GameObject" };
                    sourceSection.Add(useSelectedButton);

                    sourceLabel = new Label("(nothing selected)");
                    sourceLabel.AddToClassList("mast-wrap-label");
                    sourceSection.Add(sourceLabel);

                    statsLabel = new Label("");
                    statsLabel.AddToClassList("mast-wrap-label");
                    sourceSection.Add(statsLabel);

                    // ----------------------------------------------
                    // Materials
                    // ----------------------------------------------
                    var materialsSection = EditorUI.WindowChrome.Section(root);
                    materialsSection.style.flexGrow = 1;
                    materialsSection.style.flexShrink = 1;   // the scrolling section absorbs shortage

                    var materialsHeader = new VisualElement();
                    materialsHeader.style.flexDirection = FlexDirection.Row;
                    materialsHeader.style.justifyContent = Justify.SpaceBetween;
                    materialsHeader.Add(EditorUI.WindowChrome.BoldLabel("Materials"));
                    materialsHeader.Add(new Button(Rescan) { text = "Get Materials" });
                    materialsSection.Add(materialsHeader);

                    var scroll = new ScrollView(ScrollViewMode.Vertical);
                    scroll.style.flexGrow = 1;
                    materialRows = new VisualElement();
                    scroll.Add(materialRows);
                    materialsSection.Add(scroll);

                    var bulkRow = new VisualElement();
                    bulkRow.style.flexDirection = FlexDirection.Row;
                    bulkRow.Add(new Button(() => SetAllActions(MergeAction.MergeWithAll)) { text = "Set All: Merge with All" });
                    bulkRow.Add(new Button(() => SetAllEdges(EdgeMode.Keep)) { text = "Set All Edges: Keep" });
                    materialsSection.Add(bulkRow);

                    // ----------------------------------------------
                    // Options
                    // ----------------------------------------------
                    var optionsSection = EditorUI.WindowChrome.Section(root);
                    optionsSection.Add(EditorUI.WindowChrome.BoldLabel("Options"));

                    var weldRow = new VisualElement();
                    weldRow.style.flexDirection = FlexDirection.Row;
                    weldToggle = new Toggle("Weld duplicate vertices") { value = operation.weldVertices };
                    weldToggle.RegisterValueChangedCallback(evt =>
                    {
                        operation.weldVertices = evt.newValue;
                        weldToleranceField.SetEnabled(evt.newValue);
                    });
                    weldRow.Add(weldToggle);

                    weldToleranceField = new FloatField("Tolerance") { value = operation.weldTolerance };
                    weldToleranceField.RegisterValueChangedCallback(evt =>
                        operation.weldTolerance = Mathf.Max(0.00001f, evt.newValue));
                    weldRow.Add(weldToleranceField);
                    optionsSection.Add(weldRow);

                    colliderToggle = new Toggle("Add MeshCollider") { value = operation.addMeshCollider };
                    colliderToggle.RegisterValueChangedCallback(evt => operation.addMeshCollider = evt.newValue);
                    optionsSection.Add(colliderToggle);

                    pivotField = new EnumField("Pivot", operation.pivot);
                    pivotField.RegisterValueChangedCallback(evt => operation.pivot = (MergePivot)evt.newValue);
                    optionsSection.Add(pivotField);

                    // ----------------------------------------------
                    // Footer
                    // ----------------------------------------------
                    var footer = EditorUI.WindowChrome.Section(root);

                    estimateLabel = new Label("");
                    estimateLabel.AddToClassList("mast-wrap-label");
                    footer.Add(estimateLabel);

                    mergeButton = new Button(PerformMerge) { text = "Perform Merge…" };
                    footer.Add(mergeButton);

                    RefreshAll();
                }

                // ------------------------------------------------------------------
                // Data
                // ------------------------------------------------------------------

                private void UseSelection()
                {
                    // Only scene objects: a prefab selected in the Project window
                    // is an asset, and merging it would edit the asset itself
                    GameObject selected = Selection.activeGameObject;
                    if (selected != null && !selected.scene.IsValid())
                    {
                        EditorUtility.DisplayDialog("Merge Meshes",
                            "That selection is a prefab asset.  Select the instance in the Hierarchy instead.", "OK");
                        return;
                    }

                    operation.source = selected;
                    Rescan();
                }

                private void Rescan()
                {
                    operation.rules.Clear();
                    triangleCounts.Clear();

                    if (operation.source != null)
                    {
                        foreach (Material material in MergeMeshesOperation.GetUniqueMaterials(operation.source))
                        {
                            if (material == null)
                                continue;

                            operation.rules.Add(new MergeMeshesOperation.MaterialRule { material = material });
                            triangleCounts[material] = 0;
                        }

                        // Per-material triangle counts across included renderers
                        foreach (MeshRenderer renderer in MergeMeshesOperation.GetIncludedRenderers(operation.source))
                        {
                            var filter = renderer.GetComponent<MeshFilter>();
                            if (filter == null || filter.sharedMesh == null)
                                continue;

                            Material[] sharedMaterials = renderer.sharedMaterials;
                            int subMeshCount = Mathf.Min(sharedMaterials.Length, filter.sharedMesh.subMeshCount);
                            for (int s = 0; s < subMeshCount; s++)
                            {
                                if (sharedMaterials[s] != null && triangleCounts.ContainsKey(sharedMaterials[s]))
                                    triangleCounts[sharedMaterials[s]] += (int)(filter.sharedMesh.GetIndexCount(s) / 3);
                            }
                        }
                    }

                    RefreshAll();
                }

                // ------------------------------------------------------------------
                // UI refresh
                // ------------------------------------------------------------------

                private void RefreshAll()
                {
                    if (sourceLabel == null)
                        return;

                    if (operation.source == null)
                    {
                        sourceLabel.text = "(nothing selected — select a GameObject in the Hierarchy and click the button above)";
                        statsLabel.text = "";
                    }
                    else
                    {
                        sourceLabel.text = "▸  " + operation.source.name;

                        int childCount = operation.source.GetComponentsInChildren<Transform>().Length - 1;
                        int totalTris = 0;
                        foreach (int count in triangleCounts.Values)
                            totalTris += count;

                        statsLabel.text = childCount + " children  ·  " + totalTris.ToString("N0")
                            + " tris  ·  " + operation.rules.Count + " materials";
                    }

                    RebuildMaterialRows();
                    RefreshEstimate();
                }

                private void RebuildMaterialRows()
                {
                    materialRows.Clear();

                    foreach (MergeMeshesOperation.MaterialRule rule in operation.rules)
                    {
                        MergeMeshesOperation.MaterialRule capturedRule = rule;

                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.alignItems = Align.Center;
                        row.style.marginBottom = 2;

                        var icon = new Image
                        {
                            image = AssetPreview.GetMiniThumbnail(rule.material),
                            scaleMode = ScaleMode.ScaleToFit
                        };
                        icon.style.width = 18;
                        icon.style.height = 18;
                        icon.style.flexShrink = 0;
                        row.Add(icon);

                        var nameLabel = new Label(rule.material.name);
                        nameLabel.style.flexGrow = 1;
                        nameLabel.style.overflow = Overflow.Hidden;
                        nameLabel.style.marginLeft = 4;
                        row.Add(nameLabel);

                        int tris = triangleCounts.TryGetValue(rule.material, out int count) ? count : 0;
                        var trisLabel = new Label(tris.ToString("N0") + " tris");
                        trisLabel.style.flexShrink = 0;
                        trisLabel.style.marginRight = 6;
                        trisLabel.style.opacity = 0.7f;
                        row.Add(trisLabel);

                        var edgesField = new EnumField(capturedRule.edges);
                        var actionField = new EnumField(capturedRule.action);

                        actionField.style.width = 130;
                        actionField.style.flexShrink = 0;
                        actionField.RegisterValueChangedCallback(evt =>
                        {
                            capturedRule.action = (MergeAction)evt.newValue;
                            edgesField.SetEnabled(capturedRule.action != MergeAction.Delete);
                            RefreshEstimate();
                        });
                        row.Add(actionField);

                        edgesField.style.width = 80;
                        edgesField.style.flexShrink = 0;
                        edgesField.SetEnabled(capturedRule.action != MergeAction.Delete);
                        edgesField.RegisterValueChangedCallback(evt =>
                            capturedRule.edges = (EdgeMode)evt.newValue);
                        row.Add(edgesField);

                        materialRows.Add(row);
                    }
                }

                private void RefreshEstimate()
                {
                    int mergeAll = 0, separate = 0, deleted = 0;
                    foreach (MergeMeshesOperation.MaterialRule rule in operation.rules)
                    {
                        switch (rule.action)
                        {
                            case MergeAction.MergeWithAll: mergeAll++; break;
                            case MergeAction.MergeSeparately: separate++; break;
                            case MergeAction.Delete: deleted++; break;
                        }
                    }

                    bool canMerge = operation.source != null && (mergeAll + separate) > 0;
                    mergeButton.SetEnabled(canMerge);

                    if (operation.source == null)
                    {
                        estimateLabel.text = "Select a source GameObject to begin.";
                    }
                    else if (!canMerge)
                    {
                        estimateLabel.text = "Every material is set to Delete — nothing would be merged.";
                    }
                    else
                    {
                        int prefabCount = (mergeAll > 0 ? 1 : 0) + separate;
                        estimateLabel.text = "Result:  " + (mergeAll > 0 ? "1 merged mesh (" + mergeAll + " submeshes)" : "no main mesh")
                            + (separate > 0 ? "  +  " + separate + " separate prefab(s)" : "")
                            + (deleted > 0 ? "  ·  " + deleted + " material(s) deleted" : "")
                            + "  →  " + prefabCount + " prefab(s), ~" + (mergeAll + separate) + " draw call(s)";
                    }
                }

                private void SetAllActions(MergeAction action)
                {
                    foreach (MergeMeshesOperation.MaterialRule rule in operation.rules)
                        rule.action = action;
                    RebuildMaterialRows();
                    RefreshEstimate();
                }

                private void SetAllEdges(EdgeMode edges)
                {
                    foreach (MergeMeshesOperation.MaterialRule rule in operation.rules)
                        rule.edges = edges;
                    RebuildMaterialRows();
                    RefreshEstimate();
                }

                // ------------------------------------------------------------------
                // Merge
                // ------------------------------------------------------------------

                private void PerformMerge()
                {
                    if (operation.source == null)
                        return;

                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save Merged Prefab",
                        operation.source.name + "_Merged",
                        "prefab",
                        "Choose where to save the merged prefab.  Separately-merged materials save beside it as <name>_<material>.prefab");

                    if (string.IsNullOrEmpty(path))
                        return;

                    bool success = operation.Run(path, out string report);
                    EditorUtility.DisplayDialog("MAST Merge Meshes", report, "OK");

                    if (success)
                        RefreshAll();
                }

            }
        }
    }
}
