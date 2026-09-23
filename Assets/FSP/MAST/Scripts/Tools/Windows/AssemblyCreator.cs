using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace Tools
    {
        namespace Windows
        {
            // Turn a group of placed scene pieces into one reusable prefab.
            // UI Toolkit on the shared MAST chrome; the build itself preserves
            // prefab links AND instance overrides, and can swap the originals
            // for an instance of the new assembly
            public class AssemblyCreator : EditorWindow
            {
                [SerializeField] private List<GameObject> capturedSelection = new List<GameObject>();
                [SerializeField] private int anchorIndex = 0;

                [SerializeField] private string savePath = "";
                [SerializeField] private string prefabName = "New Assembly";

                [SerializeField] private bool alignToAnchorRotation = false;
                [SerializeField] private bool replaceOriginals = false;

                private ListView anchorList;
                private Label savePathLabel;
                private Button createButton;

                // The destination folder survives window closes "it used to
                // reset to empty every time"
                private void OnEnable()
                {
                    if (string.IsNullOrEmpty(savePath))
                        savePath = MAST.EditorUI.DataManager.state.assemblySavePath;
                }

                // Mirror the scene selection into the anchor list
                private void OnSelectionChange()
                {
                    if (anchorList == null)
                        return;

                    int index = capturedSelection.IndexOf(Selection.activeGameObject);
                    if (index >= 0)
                    {
                        anchorIndex = index;
                        anchorList.SetSelectionWithoutNotify(new[] { index });
                    }
                }

                private void CreateGUI()
                {
                    var root = rootVisualElement;
                    EditorUI.WindowChrome.InitializeRoot(root);

                    // --------------------------------------------------------
                    // Step 1: capture the scene selection
                    // --------------------------------------------------------
                    var captureSection = EditorUI.WindowChrome.Section(root);
                    captureSection.Add(EditorUI.WindowChrome.BoldLabel("Step 1:  Choose items to include in Assembly"));
                    captureSection.Add(EditorUI.WindowChrome.WrapLabel(
                        "Select all objects in the scene/hierarchy to include in this new Assembly (prefab)"));
                    captureSection.Add(new Button(CaptureSelection) { text = "Add Selected Items to Assembly" });

                    // --------------------------------------------------------
                    // Step 2: pick the anchor
                    // --------------------------------------------------------
                    var anchorSection = EditorUI.WindowChrome.Section(root);
                    anchorSection.style.flexGrow = 1;
                    anchorSection.style.flexShrink = 1;
                    anchorSection.Add(EditorUI.WindowChrome.BoldLabel("Step 2:  Choose GameObject to use as the Anchor"));
                    anchorSection.Add(EditorUI.WindowChrome.WrapLabel("In the final Assembly, this will be moved to 0,0,0"));

                    anchorList = new ListView
                    {
                        itemsSource = capturedSelection,
                        fixedItemHeight = 18,
                        selectionType = SelectionType.Single,
                        makeItem = () => new Label(),
                        bindItem = (element, index) =>
                        {
                            var label = (Label)element;
                            GameObject item = capturedSelection[index];
                            label.text = item != null ? item.name : "(deleted)";
                            label.style.opacity = item != null ? 1f : 0.5f;
                        }
                    };
                    anchorList.style.flexGrow = 1;
                    anchorList.selectionChanged += _ =>
                    {
                        if (anchorList.selectedIndex < 0)
                            return;

                        anchorIndex = anchorList.selectedIndex;
                        if (capturedSelection[anchorIndex] != null)
                            Selection.activeGameObject = capturedSelection[anchorIndex];
                    };
                    anchorSection.Add(anchorList);

                    // --------------------------------------------------------
                    // Step 3: destination folder
                    // --------------------------------------------------------
                    var folderSection = EditorUI.WindowChrome.Section(root);
                    folderSection.Add(EditorUI.WindowChrome.BoldLabel("Step 3:  Choose Destination Folder for Created Assembly"));
                    folderSection.Add(new Button(SelectDestinationFolder) { text = "Select Destination Folder" });
                    savePathLabel = EditorUI.WindowChrome.WrapLabel("");
                    folderSection.Add(savePathLabel);

                    // --------------------------------------------------------
                    // Step 4: name + options + create
                    // --------------------------------------------------------
                    var finishSection = EditorUI.WindowChrome.Section(root);
                    finishSection.Add(EditorUI.WindowChrome.BoldLabel("Step 4:  Name and Create"));

                    var nameField = new TextField("Assembly Name") { value = prefabName };
                    nameField.RegisterValueChangedCallback(evt => prefabName = evt.newValue);
                    finishSection.Add(nameField);

                    var alignToggle = new Toggle("Align to Anchor rotation")
                    {
                        value = alignToAnchorRotation,
                        tooltip = "Counter-rotate every piece around the Anchor so the Assembly comes out " +
                            "square, even when the pieces were built on a rotated MAST Holder"
                    };
                    alignToggle.RegisterValueChangedCallback(evt => alignToAnchorRotation = evt.newValue);
                    finishSection.Add(alignToggle);

                    var replaceToggle = new Toggle("Replace originals with Assembly instance")
                    {
                        value = replaceOriginals,
                        tooltip = "After saving, swap the selected pieces for one instance of the new " +
                            "Assembly in the same spot (one undo step)"
                    };
                    replaceToggle.RegisterValueChangedCallback(evt => replaceOriginals = evt.newValue);
                    finishSection.Add(replaceToggle);

                    createButton = new Button(CreateAssembly) { text = "Create Final Assembly" };
                    finishSection.Add(createButton);

                    // Enabled state + list labels track scene changes "objects
                    // can be renamed or deleted while the window is open"
                    root.schedule.Execute(Sync).Every(200);
                    Sync();
                }

                // ------------------------------------------------------------------
                // UI sync
                // ------------------------------------------------------------------
                private void Sync()
                {
                    savePathLabel.text = "Save Path: [" + savePath + "]";
                    createButton.SetEnabled(savePath != "" && capturedSelection.Count > 0);
                    anchorList.RefreshItems();
                }

                // ------------------------------------------------------------------
                // Step handlers
                // ------------------------------------------------------------------
                private void CaptureSelection()
                {
                    if (Selection.activeGameObject == null)
                        return;

                    GameObject[] unfilteredSelection = Selection.gameObjects;

                    // If an Ancestor and a Descendant are both selected, don't
                    // include the Descendant "the old direct-parent check let a
                    // selected grandchild in TWICE — once inside its ancestor
                    // and once standalone"
                    bool[] ignoreChild = new bool[unfilteredSelection.Length];
                    for (int i = 0; i < unfilteredSelection.Length; i++)
                    {
                        for (int j = 0; j < unfilteredSelection.Length; j++)
                        {
                            if (i == j)
                                continue;

                            for (Transform ancestor = unfilteredSelection[j].transform.parent;
                                ancestor != null; ancestor = ancestor.parent)
                            {
                                if (unfilteredSelection[i].transform == ancestor)
                                {
                                    ignoreChild[j] = true;
                                    break;
                                }
                            }
                        }
                    }

                    capturedSelection.Clear();
                    for (int i = 0; i < unfilteredSelection.Length; i++)
                        if (!ignoreChild[i])
                            capturedSelection.Add(unfilteredSelection[i]);

                    anchorIndex = 0;
                    Selection.activeGameObject = capturedSelection[0];
                    anchorList.RefreshItems();
                    anchorList.SetSelectionWithoutNotify(new[] { 0 });
                    Sync();
                }

                private void SelectDestinationFolder()
                {
                    string chosenPath = EditorUtility.OpenFolderPanel(
                        "Choose which Folder to Save the Assembly to", Application.dataPath, "");

                    if (chosenPath == "")
                        return;

                    // Prefabs can only save inside the project — an outside
                    // folder used to slip through and fail at save time
                    if (chosenPath == Application.dataPath ||
                        chosenPath.StartsWith(Application.dataPath + "/"))
                    {
                        savePath = "Assets" + chosenPath.Substring(Application.dataPath.Length);
                        MAST.EditorUI.DataManager.state.assemblySavePath = savePath;
                        MAST.EditorUI.DataManager.Save_Changes_To_Disk();
                        Sync();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("MAST Assembly Creator",
                            "The destination must be inside this project's Assets folder.", "OK");
                    }
                }

                // ------------------------------------------------------------------
                // Build the assembly prefab
                // ------------------------------------------------------------------
                private void CreateAssembly()
                {
                    // Create new empty Prefab parent
                    GameObject newPrefab = new GameObject();
                    newPrefab.name = string.IsNullOrEmpty(prefabName) ? "New Assembly" : prefabName;

                    // Guarantee cleanup — a failure mid-build used to strand
                    // the half-built assembly in the scene
                    try
                    {
                        // Anchor may have been deleted since it was chosen
                        bool anchorValid = anchorIndex >= 0 && anchorIndex < capturedSelection.Count &&
                            capturedSelection[anchorIndex] != null;
                        Vector3 anchorPosition = anchorValid
                            ? capturedSelection[anchorIndex].transform.position
                            : Vector3.zero;
                        Quaternion anchorRotation = (anchorValid && alignToAnchorRotation)
                            ? capturedSelection[anchorIndex].transform.rotation
                            : Quaternion.identity;
                        Quaternion inverseAnchorRotation = Quaternion.Inverse(anchorRotation);

                        // Remember where to put the replacement instance "the
                        // anchor's parent keeps it inside the same MAST Holder"
                        Transform replacementParent = anchorValid
                            ? capturedSelection[anchorIndex].transform.parent
                            : null;

                        foreach (GameObject original in capturedSelection)
                        {
                            // A selected GameObject can be deleted from the
                            // scene after "Add Selected Items" — skip it
                            if (original == null)
                                continue;

                            // Get the source Prefab for this selected GameObject
                            Object prefab = PrefabUtility.GetCorrespondingObjectFromSource(original);

                            // Prefab instances are re-instantiated from their source;
                            // plain GameObjects are copied "they used to NRE".
                            // The WORLD pose is copied from the original — the old
                            // property-modification replay used LOCAL values, so a
                            // piece under a rotated/scaled empty landed wrong
                            GameObject newChild;
                            if (prefab != null)
                            {
                                newChild = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                                // Carry the instance's overrides across "painted
                                // materials, per-piece tweaks" — re-instantiating
                                // from source used to silently discard them
                                PropertyModification[] instanceOverrides =
                                    PrefabUtility.GetPropertyModifications(original);
                                if (instanceOverrides != null)
                                    PrefabUtility.SetPropertyModifications(newChild, instanceOverrides);
                            }
                            else
                            {
                                newChild = Instantiate(original);
                            }

                            newChild.name = original.name;

                            // Pose relative to the Anchor: always offset by its
                            // position, and counter-rotated around it when
                            // "Align to Anchor rotation" is on
                            newChild.transform.SetPositionAndRotation(
                                inverseAnchorRotation * (original.transform.position - anchorPosition),
                                inverseAnchorRotation * original.transform.rotation);
                            newChild.transform.localScale = original.transform.lossyScale;

                            // Make this GameObject a child of the new Prefab GameObject
                            newChild.transform.parent = newPrefab.transform;
                        }

                        // Save the Prefab — never silently overwrite an existing one
                        string sanitizedName = newPrefab.name;
                        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                            sanitizedName = sanitizedName.Replace(invalid, '_');

                        string assemblyPath = AssetDatabase.GenerateUniqueAssetPath(
                            savePath + "/" + sanitizedName + ".prefab");
                        PrefabUtility.SaveAsPrefabAsset(newPrefab, assemblyPath);

                        // Show the user what was created "no popup, just a ping"
                        GameObject savedAssembly =
                            (GameObject)AssetDatabase.LoadMainAssetAtPath(assemblyPath);
                        EditorGUIUtility.PingObject(savedAssembly);

                        // Swap the originals for one instance of the new Assembly
                        // at the same spot — the scene looks identical but is now
                        // grouped and reusable.  One undo step restores everything
                        if (replaceOriginals && savedAssembly != null)
                        {
                            Undo.SetCurrentGroupName("Replace with Assembly");
                            int undoGroup = Undo.GetCurrentGroup();

                            var assemblyInstance =
                                (GameObject)PrefabUtility.InstantiatePrefab(savedAssembly);
                            assemblyInstance.transform.SetPositionAndRotation(
                                anchorPosition, anchorRotation);

                            // worldPositionStays keeps the world pose exact under
                            // a scaled/rotated holder
                            assemblyInstance.transform.SetParent(replacementParent, true);
                            Undo.RegisterCreatedObjectUndo(assemblyInstance, "Replace with Assembly");

                            foreach (GameObject original in capturedSelection)
                                if (original != null)
                                    Undo.DestroyObjectImmediate(original);

                            Undo.CollapseUndoOperations(undoGroup);

                            // The captured selection now points at destroyed
                            // objects — clear it so the list doesn't show ghosts
                            capturedSelection.Clear();
                            anchorIndex = 0;
                            anchorList.RefreshItems();
                            Sync();
                        }
                    }
                    finally
                    {
                        // Destroy the temporary copy of the Prefab from the scene
                        GameObject.DestroyImmediate(newPrefab);
                    }
                }
            }
        }
    }
}
