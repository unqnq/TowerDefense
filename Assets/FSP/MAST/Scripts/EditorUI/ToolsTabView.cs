using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        // UI Toolkit version of the Tools tab. The Prefab Creator, Assembly Creator,
        // and Merge Meshes windows are their own EditorWindows; this tab opens them
        // and hosts the one-click utilities.
        public class ToolsTabView : VisualElement
        {
            private static MAST.Tools.Windows.PrefabCreator prefabCreatorWindow;
            private static MAST.Tools.Windows.AssemblyCreator assemblyCreatorWindow;

            public ToolsTabView()
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                Add(scroll);

                AddToolSection(scroll,
                    "Generate Prefabs from your own models.  Substitute and consolidate materials used during the process.",
                    "Open Prefab Creator window",
                    "Open Prefab Creator window",
                    TogglePrefabCreatorWindow);

                AddToolSection(scroll,
                    "This will add a MAST script to each prefab.  The script is used to describe the type of object to the MAST editor.",
                    "Add MAST Script to Prefabs",
                    "Create Prefabs from all models in the selected folder.",
                    AddMASTScriptToPrefabs);

                AddToolSection(scroll,
                    "Create a Prefab (Assembly) from the current scene selection.",
                    "Open Create Assembly Window",
                    "Create a Prefab from the selection.  The final prefab will be moved to and anchored at 0,0,0",
                    ToggleAssemblyCreatorWindow);

                AddToolSection(scroll,
                    "Remove all MAST Components that were attached to the children of the selected GameObject during placement.",
                    "Remove MAST Components",
                    "Remove any MAST Component code attached to gameobjects during placement",
                    RemoveMASTComponents);

                AddToolSection(scroll,
                    "Merge the selected GameObject's meshes with per-material control:  merge, keep separate, or delete each material, with edge hardness options.  Saves the result as a prefab.",
                    "Open Merge Meshes window",
                    "Open the Merge Meshes window with the current selection",
                    MAST.Tools.Windows.MergeMeshesWindow.Open);

                AddToolSection(scroll,
                    "Replace materials across every prefab in a folder.  Pick replacements per material; anything left empty stays unchanged.",
                    "Open Replace Materials window",
                    "Open the Replace Materials window",
                    MAST.Tools.Windows.ReplaceMaterialsWindow.Open);
            }

            private static void AddToolSection(VisualElement parent, string description,
                string buttonText, string buttonTooltip, System.Action onClick)
            {
                var section = new VisualElement();
                section.AddToClassList("mast-section");

                var label = new Label(description);
                label.AddToClassList("mast-wrap-label");
                section.Add(label);

                var button = new Button(onClick) { text = buttonText, tooltip = buttonTooltip };
                section.Add(button);

                parent.Add(section);
            }

            // ----------------------------------------------------------------------
            // Tool actions (behavior unchanged from the IMGUI Tools tab, plus
            // null-selection guards so buttons can't throw)
            // ----------------------------------------------------------------------

            private static void TogglePrefabCreatorWindow()
            {
                if (prefabCreatorWindow == null)
                {
                    prefabCreatorWindow = (MAST.Tools.Windows.PrefabCreator)EditorWindow.GetWindow(
                        typeof(MAST.Tools.Windows.PrefabCreator), false, "MAST Prefab Creator");
                    prefabCreatorWindow.minSize = new Vector2(800, 250);
                }
                else
                {
                    EditorWindow.GetWindow(typeof(MAST.Tools.Windows.PrefabCreator)).Close();
                }
            }

            private static void ToggleAssemblyCreatorWindow()
            {
                if (assemblyCreatorWindow == null)
                {
                    assemblyCreatorWindow = (MAST.Tools.Windows.AssemblyCreator)EditorWindow.GetWindow(
                        typeof(MAST.Tools.Windows.AssemblyCreator), false, "MAST Assembly Creator");
                    assemblyCreatorWindow.minSize = new Vector2(400, 400);
                }
                else
                {
                    EditorWindow.GetWindow(typeof(MAST.Tools.Windows.AssemblyCreator)).Close();
                }
            }

            private static void AddMASTScriptToPrefabs()
            {
                string chosenPath = EditorUtility.OpenFolderPanel(
                    "Choose the Folder that Contains your Prefabs",
                    MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(MAST.EditorUI.DataManager.state.prefabPath), "");

                if (chosenPath != "")
                {
                    chosenPath = MAST.LoadingHelper.ConvertAbsolutePathToProjectPath(chosenPath);

                    foreach (GameObject prefab in MAST.LoadingHelper.GetPrefabsInFolder(chosenPath))
                    {
                        if (!prefab.GetComponent<MAST.Component.MASTPrefabSettings>())
                            prefab.AddComponent<MAST.Component.MASTPrefabSettings>();
                    }
                }
            }

            private static void RemoveMASTComponents()
            {
                if (Selection.activeGameObject == null)
                {
                    EditorUtility.DisplayDialog("Nothing selected",
                        "Select a GameObject in the Hierarchy first.", "OK");
                    return;
                }

                if (EditorUtility.DisplayDialog("Are you sure?",
                    "This will remove all MAST components attached to '" + Selection.activeGameObject.name + "'",
                    "Remove MAST Components", "Cancel"))
                {
                    int skippedInstanceComponents = 0;

                    foreach (MAST.Component.MASTPrefabSettings prefabComponent
                        in Selection.activeGameObject.transform.GetComponentsInChildren<MAST.Component.MASTPrefabSettings>(true))
                    {
                        // A component that belongs to a prefab ASSET can't be
                        // destroyed on the instance "Unity throws" — it has to
                        // be removed from the prefab itself
                        if (PrefabUtility.IsPartOfPrefabInstance(prefabComponent) &&
                            !PrefabUtility.IsAddedComponentOverride(prefabComponent))
                        {
                            skippedInstanceComponents++;
                            continue;
                        }

                        Undo.DestroyObjectImmediate(prefabComponent);
                    }

                    if (skippedInstanceComponents > 0)
                        EditorUtility.DisplayDialog("Some components skipped",
                            skippedInstanceComponents + " component(s) belong to prefab assets and can't be removed " +
                            "from instances.  Use 'Remove MAST scripts' on the prefab FOLDER to strip them from " +
                            "the assets themselves.", "OK");
                }
            }

        }
    }
}
