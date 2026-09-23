using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace Tools
    {
        namespace Windows
        {
            // Four-step wizard that turns a folder of models into MAST-ready
            // prefabs: find models, organize/consolidate materials, link them
            // to saved assets, create the prefabs.  UI Toolkit on the shared
            // MAST chrome; all the heavy lifting lives in the headless
            // PrefabCreatorOperation operation class.
            //
            // Display strings are computed in each ListView's bindItem straight
            // from the data arrays — the IMGUI version kept parallel
            // "*SelGridData" display arrays that had to be rebuilt in sync
            public class PrefabCreator : EditorWindow
            {
                [SerializeField] private MAST.Tools.PrefabCreatorOperation PrepModelsClass;
                private MAST.Tools.PrefabCreatorOperation PrepModels
                {
                    get
                    {
                        if (PrepModelsClass == null)
                            PrepModelsClass = new MAST.Tools.PrefabCreatorOperation();
                        return PrepModelsClass;
                    }
                }

                // ---------------------------------------------------------------------------
                // Wizard state "serialized so a domain reload keeps the user's place"
                // ---------------------------------------------------------------------------
                [SerializeField] private int processStep = 0;
                [SerializeField] private int maxStepReached = 0;

                // Step 1: Find Models
                [SerializeField] private string modelPath = "";
                [SerializeField] private List<string> modelNames = new List<string>();

                // Step 2: Organize Materials
                [SerializeField] private List<Material> sourceMat;
                [SerializeField] private string[] sourceMatName;
                [SerializeField] private string[] sourceMatNewName;
                [SerializeField] private List<int> selectedMatPointers = new List<int>();
                [SerializeField] private string newMaterialName = "";

                // Step 3: Link Materials
                [SerializeField] private int[] conMatPointer;
                [SerializeField] private Material[] conMat;
                [SerializeField] private string[] conMatName;
                [SerializeField] private string[] conMatPath;
                [SerializeField] private string lastFolderSelected;

                // Step 4: Create Prefabs
                [SerializeField] private bool flagAddMeshCollider = true;
                [SerializeField] private bool flagAddEmptyParent = true;
                [SerializeField] private MAST.Tools.PrefabCreatorOperation.MergeMethod mergeMethod =
                    MAST.Tools.PrefabCreatorOperation.MergeMethod.MergeChildren;
                [SerializeField] private Vector3 meshPositionOffset = Vector3.zero;
                [SerializeField] private Vector3 meshRotationOffset = Vector3.zero;
                [SerializeField] private Vector3 meshScale = Vector3.one;
                [SerializeField] private bool flagWeldVertices = false;

                // ---------------------------------------------------------------------------
                // UI references
                // ---------------------------------------------------------------------------
                private readonly Button[] stepButtons = new Button[4];
                private readonly VisualElement[] pages = new VisualElement[4];

                private Label modelPathLabel;
                private ListView modelList;

                private ListView allMatsList;
                private ListView selectedMatsList;
                private TextField newNameField;

                private ListView linkList;
                private Button substituteButton;

                private void CreateGUI()
                {
                    var root = rootVisualElement;
                    EditorUI.WindowChrome.InitializeRoot(root);

                    // --------------------------------------------------------
                    // Step toolbar "jump to any page already reached"
                    // --------------------------------------------------------
                    var toolbar = EditorUI.WindowChrome.Row(root);
                    string[] stepTitles =
                    {
                        "Step 1:  Find Models", "Step 2:  Organize Materials",
                        "Step 3:  Link Materials", "Step 4:  Create Prefabs"
                    };
                    for (int i = 0; i < 4; i++)
                    {
                        int step = i;
                        stepButtons[i] = new Button(() => ShowStep(step)) { text = stepTitles[i] };
                        stepButtons[i].style.flexGrow = 1;
                        toolbar.Add(stepButtons[i]);
                    }

                    var content = new VisualElement();
                    content.style.flexGrow = 1;
                    root.Add(content);

                    pages[0] = BuildFindModelsPage();
                    pages[1] = BuildOrganizeMaterialsPage();
                    pages[2] = BuildLinkMaterialsPage();
                    pages[3] = BuildCreatePrefabsPage();
                    foreach (VisualElement page in pages)
                        content.Add(page);

                    ShowStep(processStep);
                }

                // ---------------------------------------------------------------------------
                // Navigation
                // ---------------------------------------------------------------------------
                private void ShowStep(int step)
                {
                    // A page is only valid once its data exists "reloads can
                    // restore a step number without the arrays behind it"
                    if (step >= 1 && (sourceMat == null || sourceMat.Count == 0))
                        step = 0;
                    if (step >= 2 && (conMatName == null || conMatName.Length == 0))
                        step = Math.Min(step, 1);

                    processStep = step;
                    maxStepReached = Math.Max(maxStepReached, step);

                    for (int i = 0; i < 4; i++)
                    {
                        pages[i].style.display = (i == step) ? DisplayStyle.Flex : DisplayStyle.None;
                        stepButtons[i].SetEnabled(i <= maxStepReached);
                        stepButtons[i].EnableInClassList("mast-tab--active", i == step);
                    }

                    switch (step)
                    {
                        case 0: RefreshModelPage(); break;
                        case 1: RefreshOrganizePage(); break;
                        case 2: RefreshLinkPage(); break;
                    }
                }

                // ---------------------------------------------------------------------------
                // Page 1: Find Models
                // ---------------------------------------------------------------------------
                private VisualElement BuildFindModelsPage()
                {
                    var page = new VisualElement();
                    page.style.flexGrow = 1;

                    var header = EditorUI.WindowChrome.Section(page);
                    header.Add(EditorUI.WindowChrome.BoldLabel("Step 1:  Find Models"));
                    header.Add(EditorUI.WindowChrome.WrapLabel("Choose folder containing models"));

                    var folderSection = EditorUI.WindowChrome.Section(page);
                    folderSection.Add(new Button(SelectModelFolder) { text = "Select Model Folder" });
                    modelPathLabel = EditorUI.WindowChrome.WrapLabel("");
                    folderSection.Add(modelPathLabel);

                    var listSection = EditorUI.WindowChrome.Section(page);
                    listSection.style.flexGrow = 1;
                    listSection.style.flexShrink = 1;
                    listSection.Add(EditorUI.WindowChrome.BoldLabel("Models Found"));
                    modelList = SimpleList(index => modelNames[index]);
                    modelList.itemsSource = modelNames;
                    listSection.Add(modelList);

                    var footer = EditorUI.WindowChrome.Section(page);
                    footer.Add(new Button(FinishFindModels) { text = "Done with Selecting Folder" });

                    return page;
                }

                private void RefreshModelPage()
                {
                    modelPathLabel.text = "Selected Path: [" + modelPath + "]";
                    modelList.RefreshItems();
                }

                private void SelectModelFolder()
                {
                    string chosenPath = EditorUtility.OpenFolderPanel(
                        "Choose the Folder that Contains your Models to Convert", Application.dataPath, "");

                    if (chosenPath == "")
                        return;

                    // Models can only load from inside the project
                    if (chosenPath != Application.dataPath &&
                        !chosenPath.StartsWith(Application.dataPath + "/"))
                    {
                        EditorUtility.DisplayDialog("MAST Prefab Creator",
                            "The model folder must be inside this project's Assets folder.", "OK");
                        return;
                    }

                    modelPath = "Assets" + chosenPath.Substring(Application.dataPath.Length);

                    modelNames.Clear();
                    foreach (string path in PrepModels.GetPathOfModelsInFolder(modelPath))
                        modelNames.Add(path.Replace(modelPath + "/", ""));

                    RefreshModelPage();
                }

                private void FinishFindModels()
                {
                    // Get materials from all models in the specified path
                    sourceMat = PrepModels.StripMaterials(modelPath);

                    if (sourceMat == null)
                    {
                        EditorUtility.DisplayDialog("No Materials found!",
                            "The Prefab Creator requires materials to be attached to the models.", "Understood");
                        return;
                    }

                    sourceMatName = new string[sourceMat.Count];
                    sourceMatNewName = new string[sourceMat.Count];
                    for (int i = 0; i < sourceMat.Count; i++)
                    {
                        sourceMatName[i] = sourceMat[i].name;
                        sourceMatNewName[i] = sourceMatName[i];
                    }

                    selectedMatPointers.Clear();
                    ShowStep(1);
                }

                // ---------------------------------------------------------------------------
                // Page 2: Organize Materials
                // ---------------------------------------------------------------------------
                private VisualElement BuildOrganizeMaterialsPage()
                {
                    var page = new VisualElement();
                    page.style.flexGrow = 1;

                    var header = EditorUI.WindowChrome.Section(page);
                    header.Add(EditorUI.WindowChrome.BoldLabel("Step 2:  Organize Materials"));
                    header.Add(EditorUI.WindowChrome.WrapLabel(
                        "Double-click to select materials to be renamed.  " +
                        "Multiple materials sharing the same new name will be consolidated."));

                    var columns = EditorUI.WindowChrome.Row(page);
                    columns.style.flexGrow = 1;
                    columns.style.alignItems = Align.Stretch;

                    // Left column: every material
                    var leftColumn = new VisualElement();
                    leftColumn.style.flexGrow = 1;
                    leftColumn.style.flexBasis = 0;
                    columns.Add(leftColumn);

                    var allSection = EditorUI.WindowChrome.Section(leftColumn);
                    allSection.style.flexGrow = 1;
                    allSection.Add(EditorUI.WindowChrome.WrapLabel("All Materials (Old > New) — double-click to Add/Remove"));
                    allMatsList = SimpleList(index => sourceMatName[index] + " > " + sourceMatNewName[index]);
                    allMatsList.itemsChosen += _ =>
                    {
                        if (allMatsList.selectedIndex < 0)
                            return;

                        ToggleSelectedPointer(allMatsList.selectedIndex);
                        allMatsList.ClearSelection();
                    };
                    allSection.Add(allMatsList);

                    var blenderSection = EditorUI.WindowChrome.Section(leftColumn);
                    var blenderButton = new Button(MergeBlenderDuplicates)
                    {
                        text = "Merge Blender Material Duplicates",
                        tooltip = "Merge all Blender material duplicates, removing the .001, .002, etc, leaving the base material name"
                    };
                    blenderSection.Add(blenderButton);

                    // Right column: the working selection
                    var rightColumn = new VisualElement();
                    rightColumn.style.flexGrow = 1;
                    rightColumn.style.flexBasis = 0;
                    columns.Add(rightColumn);

                    var selectedSection = EditorUI.WindowChrome.Section(rightColumn);
                    selectedSection.style.flexGrow = 1;
                    selectedSection.Add(EditorUI.WindowChrome.WrapLabel("Selected Materials (Old > New) — double-click to Remove"));
                    selectedMatsList = SimpleList(index =>
                    {
                        int pointer = selectedMatPointers[index];
                        return sourceMatName[pointer] + " > " + sourceMatNewName[pointer];
                    });
                    selectedMatsList.itemsSource = selectedMatPointers;
                    selectedMatsList.itemsChosen += _ =>
                    {
                        if (selectedMatsList.selectedIndex < 0)
                            return;

                        ToggleSelectedPointer(selectedMatPointers[selectedMatsList.selectedIndex]);
                        selectedMatsList.ClearSelection();
                    };
                    selectedSection.Add(selectedMatsList);

                    var renameSection = EditorUI.WindowChrome.Section(rightColumn);
                    newNameField = new TextField("New Name")
                    {
                        value = newMaterialName,
                        tooltip = "New name for material(s)"
                    };
                    newNameField.RegisterValueChangedCallback(evt => newMaterialName = evt.newValue);
                    renameSection.Add(newNameField);
                    renameSection.Add(new Button(RenameSelectedMaterials)
                    {
                        text = "Rename / Merge Selected Materials",
                        tooltip = "Rename all selected materials"
                    });

                    var footer = EditorUI.WindowChrome.Section(page);
                    footer.Add(new Button(FinishOrganizeMaterials)
                    {
                        text = "Done with Organizing  |  Extract Materials",
                        tooltip = "Extract materials then go to Link Materials section"
                    });

                    return page;
                }

                private void RefreshOrganizePage()
                {
                    allMatsList.itemsSource = sourceMatName;
                    allMatsList.RefreshItems();
                    selectedMatsList.RefreshItems();
                    newNameField.SetValueWithoutNotify(newMaterialName);
                }

                // Membership toggle on the working selection, kept sorted so
                // the right column follows the left column's order
                private void ToggleSelectedPointer(int pointer)
                {
                    if (!selectedMatPointers.Remove(pointer))
                    {
                        selectedMatPointers.Add(pointer);
                        selectedMatPointers.Sort();
                    }
                    selectedMatsList.RefreshItems();
                }

                private void MergeBlenderDuplicates()
                {
                    sourceMatNewName = MergeBlenderMaterialDuplicates(sourceMatNewName);
                    selectedMatPointers.Clear();
                    RefreshOrganizePage();
                }

                private void RenameSelectedMaterials()
                {
                    if (string.IsNullOrEmpty(newMaterialName))
                        return;

                    foreach (int pointer in selectedMatPointers)
                        sourceMatNewName[pointer] = newMaterialName;

                    selectedMatPointers.Clear();
                    newMaterialName = "";
                    RefreshOrganizePage();
                }

                private void FinishOrganizeMaterials()
                {
                    // If the "/Materials" subfolder doesn't exist
                    if (!AssetDatabase.IsValidFolder(modelPath + "/Materials"))
                    {
                        AssetDatabase.CreateFolder(modelPath, "Materials");
                    }
                    else
                    {
                        // Display a warning.  If the user clicks "Continue"
                        if (EditorUtility.DisplayDialog("Erase existing Material folder?",
                            "A material folder already exists here.  Continuing will erase all previous materials in the folder.",
                            "Continue", "Cancel"))
                        {
                            Directory.Delete(modelPath + "/Materials", true);
                            AssetDatabase.Refresh();
                            AssetDatabase.CreateFolder(modelPath, "Materials");
                            AssetDatabase.Refresh();
                        }
                        else return;
                    }

                    // Get a consolidated list of all new material names
                    // "removes duplicates created from consolidation"
                    conMatName = sourceMatNewName.Distinct().ToArray();

                    conMat = new Material[conMatName.Length];
                    conMatPath = new string[conMatName.Length];

                    for (int i = 0; i < conMatName.Length; i++)
                    {
                        // First occurrence of this name in the source list is
                        // the material the consolidated asset clones
                        int matIndexInSource = Array.IndexOf(sourceMatNewName, conMatName[i]);
                        conMat[i] = sourceMat[matIndexInSource];
                        conMatPath[i] = modelPath + "/Materials/" + conMatName[i] + ".mat";
                        AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(conMat[i]), conMatPath[i]);
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    // Swap the in-memory clones for the saved assets
                    for (int i = 0; i < conMatPath.Length; i++)
                        conMat[i] = (Material)AssetDatabase.LoadAssetAtPath(conMatPath[i], typeof(Material));

                    // Pointer from each original material to its consolidated slot
                    var conMatNameList = new List<string>(conMatName);
                    conMatPointer = new int[sourceMatName.Length];
                    for (int i = 0; i < conMatPointer.Length; i++)
                        conMatPointer[i] = conMatNameList.IndexOf(sourceMatNewName[i]);

                    lastFolderSelected = Application.dataPath;
                    ShowStep(2);
                }

                // ---------------------------------------------------------------------------
                // Page 3: Link Materials
                // ---------------------------------------------------------------------------
                private VisualElement BuildLinkMaterialsPage()
                {
                    var page = new VisualElement();
                    page.style.flexGrow = 1;

                    var header = EditorUI.WindowChrome.Section(page);
                    header.Add(EditorUI.WindowChrome.BoldLabel("Step 3:  Link Materials"));
                    header.Add(EditorUI.WindowChrome.WrapLabel("Link model materials to saved materials in project"));

                    var listSection = EditorUI.WindowChrome.Section(page);
                    listSection.style.flexGrow = 1;
                    listSection.style.flexShrink = 1;
                    listSection.Add(EditorUI.WindowChrome.BoldLabel("Consolidated Materials (Name > Path)"));
                    linkList = SimpleList(index => conMatName[index] + " > " + conMatPath[index]);
                    linkList.selectionChanged += _ =>
                        substituteButton.SetEnabled(linkList.selectedIndex >= 0);
                    listSection.Add(linkList);

                    var substituteSection = EditorUI.WindowChrome.Section(page);
                    substituteButton = new Button(SubstituteSelectedMaterial)
                    {
                        text = "Substitute Selected Material",
                        tooltip = "Choose a Material to substitute for the selected material"
                    };
                    substituteButton.SetEnabled(false);
                    substituteSection.Add(substituteButton);

                    var footer = EditorUI.WindowChrome.Section(page);
                    footer.Add(new Button(FinishLinkMaterials) { text = "Done with Substituting Materials" });

                    return page;
                }

                private void RefreshLinkPage()
                {
                    linkList.itemsSource = conMatName;
                    linkList.RefreshItems();
                    substituteButton.SetEnabled(linkList.selectedIndex >= 0);
                }

                private void SubstituteSelectedMaterial()
                {
                    int selected = linkList.selectedIndex;
                    if (selected < 0 || selected >= conMatName.Length)
                        return;

                    string matPath = EditorUtility.OpenFilePanelWithFilters(
                        "Choose substitute Material for " + conMatName[selected],
                        lastFolderSelected, new[] { "Material files", "mat" });

                    if (matPath == "")
                        return;

                    matPath = matPath.Replace(Application.dataPath, "Assets");
                    conMatPath[selected] = matPath;
                    conMat[selected] = (Material)AssetDatabase.LoadAssetAtPath(conMatPath[selected], typeof(Material));

                    // Remember this FOLDER for next time "storing the file path
                    // made the dialog silently reject it — the feature never worked"
                    lastFolderSelected = System.IO.Path.GetDirectoryName(
                        MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(matPath));

                    linkList.RefreshItems();
                }

                private void FinishLinkMaterials()
                {
                    // Reset "Create Prefab" section options
                    flagAddMeshCollider = true;
                    flagAddEmptyParent = true;
                    mergeMethod = MAST.Tools.PrefabCreatorOperation.MergeMethod.MergeChildren;
                    ShowStep(3);
                }

                // ---------------------------------------------------------------------------
                // Page 4: Create Prefabs
                // ---------------------------------------------------------------------------
                private VisualElement BuildCreatePrefabsPage()
                {
                    var page = new VisualElement();
                    page.style.flexGrow = 1;

                    var header = EditorUI.WindowChrome.Section(page);
                    header.Add(EditorUI.WindowChrome.BoldLabel("Step 4:  Create Prefabs"));
                    header.Add(EditorUI.WindowChrome.WrapLabel("Extract meshes, then generate and save prefabs."));

                    var optionsSection = EditorUI.WindowChrome.Section(page);

                    var colliderToggle = new Toggle("Add Mesh Collider") { value = flagAddMeshCollider };
                    colliderToggle.RegisterValueChangedCallback(evt => flagAddMeshCollider = evt.newValue);
                    optionsSection.Add(colliderToggle);

                    var parentToggle = new Toggle("Add Empty Parent") { value = flagAddEmptyParent };
                    parentToggle.RegisterValueChangedCallback(evt => flagAddEmptyParent = evt.newValue);
                    optionsSection.Add(parentToggle);

                    var mergeField = new EnumField("Merge Method", mergeMethod);
                    mergeField.RegisterValueChangedCallback(evt =>
                        mergeMethod = (MAST.Tools.PrefabCreatorOperation.MergeMethod)evt.newValue);
                    optionsSection.Add(mergeField);

                    // Mesh modifiers: baked into each prefab mesh directly, so the
                    // prefab roots stay at 0,0,0 with no rotation and scale 1
                    var modifierSection = EditorUI.WindowChrome.Section(page);
                    modifierSection.Add(EditorUI.WindowChrome.WrapLabel(
                        "Mesh Modifiers (baked into each prefab mesh — prefab roots stay at 0,0,0, no rotation, scale 1)"));

                    var positionField = new Vector3Field("Position Offset") { value = meshPositionOffset };
                    positionField.RegisterValueChangedCallback(evt => meshPositionOffset = evt.newValue);
                    modifierSection.Add(positionField);

                    var rotationField = new Vector3Field("Rotation") { value = meshRotationOffset };
                    rotationField.RegisterValueChangedCallback(evt => meshRotationOffset = evt.newValue);
                    modifierSection.Add(rotationField);

                    var scaleField = new Vector3Field("Scale") { value = meshScale };
                    scaleField.RegisterValueChangedCallback(evt => meshScale = evt.newValue);
                    modifierSection.Add(scaleField);

                    var weldToggle = new Toggle("Weld Vertices")
                    {
                        value = flagWeldVertices,
                        tooltip = "Weld duplicate vertices in each mesh (UV-seam and hard-edge safe)"
                    };
                    weldToggle.RegisterValueChangedCallback(evt => flagWeldVertices = evt.newValue);
                    modifierSection.Add(weldToggle);

                    var footer = EditorUI.WindowChrome.Section(page);
                    footer.Add(new Button(CreatePrefabs) { text = "Create Prefabs" });

                    return page;
                }

                private void CreatePrefabs()
                {
                    PrepModels.CreatePrefabs(modelPath, sourceMat, new List<string>(sourceMatName),
                        conMatPointer, conMatName, conMat,
                        flagAddMeshCollider, flagAddEmptyParent, mergeMethod,
                        meshPositionOffset, meshRotationOffset, meshScale,
                        flagWeldVertices);
                }

                // ---------------------------------------------------------------------------
                // Helpers
                // ---------------------------------------------------------------------------

                // Single-select list of plain text rows; the label for each row
                // is computed on bind so it always reflects the current data
                private static ListView SimpleList(Func<int, string> getText)
                {
                    var list = new ListView
                    {
                        fixedItemHeight = 18,
                        selectionType = SelectionType.Single,
                        makeItem = () => new Label(),
                        bindItem = null
                    };
                    list.bindItem = (element, index) => ((Label)element).text = getText(index);
                    list.style.flexGrow = 1;
                    return list;
                }

                // Strip Blender's ".001"-style suffixes so duplicates collapse
                // onto the base material name
                private string[] MergeBlenderMaterialDuplicates(string[] materialNames)
                {
                    for (int i = 0; i < materialNames.Length; i++)
                    {
                        if (materialNames[i].Length <= 4)
                            continue;
                        if (materialNames[i].Substring(materialNames[i].Length - 4, 1) != ".")
                            continue;

                        if (int.TryParse(materialNames[i].Substring(materialNames[i].Length - 3, 3), out int suffix)
                            && suffix > 0)
                            materialNames[i] = materialNames[i].Substring(0, materialNames[i].Length - 4);
                    }

                    return materialNames;
                }
            }
        }
    }
}
