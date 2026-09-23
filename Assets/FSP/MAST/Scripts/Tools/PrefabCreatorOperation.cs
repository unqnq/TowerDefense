using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Tools
    {
        public class PrefabCreatorOperation
        {
            public enum MergeMethod { DoNotMerge, MergeChildren, MergeAll }
            
            // Selected folder
            private string pathSelected;
            private string pathMeshes;
            private string pathMaterials;
            private string pathPrefabs;
            
            // Original model files
            private GameObject[] sourceModels;
            
        #region StripMaterials
            
            // ---------------------------------------------------------------------------
            // Desc:    Strip Materials from all models in the specified folder
            //
            // In:      String path for models
            //
            // Out:     List containing all unique materials
            // ---------------------------------------------------------------------------
            public List<Material> StripMaterials(string targetPath)
            {
                // Return empty if no path was selected
                if (targetPath == "")
                    return null;
                
                // Get Models as a GameObject array
                GameObject[] models = GetModelsInFolder(targetPath);
                
                // If Models were found
                if (models != null)
                {
                    List<Material> mats = new List<Material>();
                    
                    // Loop through each model
                    for (int i = 0; i < models.Length; i++)
                    {
                        // Begin a recursive algorithm that iterates through all children on down
                        // Returns a Material List containing only unique Materials
                        mats = AppendUniqueMaterials(models[i].transform, mats);
                    }
                    
                    // Sort material list by material names
                    mats = mats.OrderBy(Material=>Material.name).ToList();
                    
                    return mats;
                }
                
                // NO Models were found
                return null;
            }
            
            // ---------------------------------------------------------------------------
            // Desc:    Get a GameObject array from the specific folder
            //
            // Used by StripMaterials and CreatePrefabs.
            // ---------------------------------------------------------------------------
            // In:      String folder path for models
            //
            // Out:     GameObject array containing all "model" GameObjects in the folder
            // ---------------------------------------------------------------------------
            private GameObject[] GetModelsInFolder(string targetPath)
            {
                // Get all GameObject GUID's in the specified path and any subfolders of it
                string[] modelPath = GetPathOfModelsInFolder(targetPath);
                
                // If models were found
                if (modelPath != null)
                {
                    // Create array to store the gameObjects
                    GameObject[] modelGameObject = new GameObject[modelPath.Length];
                    
                    // Loop through each GameObject in the folder
                    for (int i = 0; i < modelPath.Length; i++)
                    {
                        // Get gameObject at path
                        modelGameObject[i] = (GameObject)AssetDatabase.LoadMainAssetAtPath(modelPath[i]);
                    }
                    
                    return modelGameObject;
                }
                
                return null;
            }
            
            public string[] GetPathOfModelsInFolder(string targetPath)
            {
                // Get all GameObject GUID's in the specified path and any subfolders of it
                string[] GUIDOfAllGameObjectsInFolder = AssetDatabase.FindAssets("t:gameobject", new[] { targetPath });

                // Collect the model paths, EXCLUDING this tool's own output
                // folders — re-running the wizard on the same folder used to
                // pick up its previously generated prefabs as "models" and
                // re-process them over themselves
                var modelPaths = new List<string>();
                string generatedPrefabs = targetPath + "/Prefabs/";
                string generatedMaterials = targetPath + "/Materials/";
                string generatedMeshes = targetPath + "/Meshes/";

                foreach (string guid in GUIDOfAllGameObjectsInFolder)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    if (path.StartsWith(generatedPrefabs) ||
                        path.StartsWith(generatedMaterials) ||
                        path.StartsWith(generatedMeshes))
                        continue;

                    modelPaths.Add(path);
                }

                // Empty array, never null — callers index .Length directly
                return modelPaths.ToArray();
            }
            
            // ---------------------------------------------------------------------------
            // Desc:    Recursive search that returns a list of unique materials and
            //          appends any new materials to the supplied list and returns it.
            //
            // Used by StripMaterials.
            //
            // DELIBERATELY name-keyed, unlike MeshHelper.GetUniqueMaterials:
            // the extract step consolidates same-named materials across models
            // on purpose — that is this wizard's feature, not the MagicaVoxel
            // bug the identity-keyed shared scanner exists to avoid.
            // ---------------------------------------------------------------------------
            // In:      Transform
            //
            // Out:     Material List containing unique materials
            // ---------------------------------------------------------------------------
            private List<Material> AppendUniqueMaterials(Transform transform, List<Material> mats)
            {
                // Get this GameObject's MeshRenderer
                MeshRenderer meshRenderer = transform.gameObject.GetComponent<MeshRenderer>();
                
                // If MeshRenderer is found
                if (meshRenderer)
                {
                    // Get Materials (array) in this MeshRenderer
                    Material[] tempMats = meshRenderer.sharedMaterials;
                    
                    // Flag to show if material was found
                    bool foundMat;
                    
                    // Loop through each Material
                    for (int t = 0; t < tempMats.Length; t++)
                    {
                        // Empty material slots are common on imported models —
                        // null.name used to crash the whole extract step
                        if (tempMats[t] == null)
                            continue;

                        // Set found material flag back to false
                        foundMat = false;
                        
                        // Loop through each material in the Material list
                        foreach (Material material in mats)
                        {
                            // If material names match set found material flag to true
                            if (tempMats[t].name == material.name)
                                foundMat = true;
                        }
                        
                        // If the Material doesn't already exist in the unique list, add it
                        if (!foundMat)
                        {
                            mats.Add(tempMats[t]);
                        }
                    }
                }
                
                // Run this method for all child transforms
                foreach (Transform childTransform in transform)
                {
                    mats = AppendUniqueMaterials(childTransform, mats);
                }
                
                // Return with the current Material List
                return mats;
            }
            
        #endregion
            
            // ------------------------------------------------------------------------------------------------
            // Desc:    Look for a Material in a Material List
            // ------------------------------------------------------------------------------------------------
            // In:      string          targetPath      Path containing models
            //          List<Material>  sourceMat       Original Materials stripped from the models.
            //          string[]        sourceMatName   Original Material names.  Used to find the object's
            //                                            material in the primary material list.
            //          int[]           conMatPointer   Pointers linking the source material list to new materials
            //          string[]        conMatName      New Material names "shorter array with combined materials"
            //                                            used to rename the material.
            //          Material[]      conMat          Consolidated material array.  Already contains subsituted
            //                                            materials.
            //          bool            flagAddMeshCollider     Should a MeshCollider be added for each mesh
            //          bool            flagAddEmptyParent      Should an empty parent GameObject be created
            //          bool            flagPreserveModelHierarchy      Should prefab use same parent/child hierarchy as model
            //
            // Out:     Bool whether process was successful
            // ------------------------------------------------------------------------------------------------
            public bool CreatePrefabs(string targetPath, List<Material> sourceMat, List<string> sourceMatName,
                                    int[] conMatPointer, string[] conMatName, Material[] conMat,
                                    bool flagAddMeshCollider, bool flagAddEmptyParent, MergeMethod mergeMethod,
                                    Vector3 meshPositionOffset, Vector3 meshRotationOffset, Vector3 meshScale,
                                    bool weldVertices)
            {
                // Create a new "/Prefabs" folder, if one doesn't exist
                if (!AssetDatabase.IsValidFolder(targetPath + "/Prefabs"))
                    AssetDatabase.CreateFolder(targetPath, "Prefabs");
                
                AssetDatabase.SaveAssets();
                
                // Convert source Material name array to List for easy searching
                List<string> searchMatName = new List<string>(sourceMatName);
                
                // Loop through each source Material and give each a reference to the extracted or substituted Material
                for (int i = 0; i < sourceMat.Count; i++)
                {
                    sourceMat[i] = conMat[conMatPointer[i]];
                }
                
                // Get all models in the target folder
                GameObject[] model = GetModelsInFolder(targetPath);
                
                // Create new Prefab and Prefab child GameObjects
                GameObject finalGameObject = null;

                // Same-named models in different subfolders used to silently
                // overwrite each other's prefab
                var usedPrefabPaths = new HashSet<string>();
                int failedModels = 0;
                bool canceled = false;

                try
                {

                // Loop through each model
                for (int i = 0; i < model.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "MAST Prefab Creator", model[i] != null ? model[i].name : "…", (float)i / model.Length))
                    {
                        canceled = true;
                        break;
                    }

                    if (model[i] == null)
                        continue;

                    GameObject prefabGameObject = null;
                    finalGameObject = null;

                    // One bad model "null material, odd hierarchy" must not
                    // abort the batch or strand temp objects in the scene
                    try
                    {

                    // ------------------------------------------------------------
                    // Create new GameObject based on merge method chosen
                    // ------------------------------------------------------------

                    switch (mergeMethod)
                    {
                        // If merging children only, create a GameObject with a semi-preserved hierarchy, only merging meshes
                        //     in GameObjects that are at the end of the hierarchy and shared by the same parent GameObject
                        case MergeMethod.MergeChildren:
                            finalGameObject = CreateGameObjectWithMergedChildren(model[i].transform, searchMatName, sourceMat, model[i].name);
                            break;
                        
                        // If not performing a merge, create a GameObject with a fully preserved hierarchy
                        case MergeMethod.DoNotMerge:
                            finalGameObject = CreateGameObjectWithPreservedHierarchy(model[i].transform, searchMatName, sourceMat, model[i].name);
                            break;
                        
                        // If merging everything, create a GameObject with a single MeshFilter containing all Meshes combined
                        case MergeMethod.MergeAll:
                            finalGameObject = CreateGameObjectWithSingleMesh(model[i].transform, searchMatName, sourceMat);
                            break;
                    }
                    
                    // ------------------------------------------------------------
                    // Add parent GameObject if requested and needed
                    // ------------------------------------------------------------

                    // If adding an empty parent GameObject and the GameObject doesn't already have an empty parent GameObject
                    if (flagAddEmptyParent && finalGameObject.GetComponent<MeshRenderer>())
                    {
                        // Create an empty parent with model's name and attach the gameobject as a child
                        prefabGameObject = new GameObject(model[i].name);
                        finalGameObject.transform.parent = prefabGameObject.transform;
                    }
                    
                    // If not adding an empty parent GameObject or an empty parent GameObject already exists
                    else
                    {
                        // Create an empty parent with model's name
                        prefabGameObject = new GameObject(model[i].name);
                        
                        // If the final GameObject has a MeshRenderer at its top level
                        if (finalGameObject.GetComponent<MeshRenderer>())
                        {
                            // Copy the Mesh and Materials to the Prefab.  "The old code
                            // assigned the GetComponent results to local variables —
                            // copying nothing — so merge-all without an empty parent
                            // produced an empty prefab and failed on save"
                            MeshFilter meshFilter = prefabGameObject.AddComponent<MeshFilter>();
                            meshFilter.sharedMesh = finalGameObject.GetComponent<MeshFilter>().sharedMesh;

                            MeshRenderer meshRenderer = prefabGameObject.AddComponent<MeshRenderer>();
                            meshRenderer.sharedMaterials = finalGameObject.GetComponent<MeshRenderer>().sharedMaterials;
                        }
                        
                        // Move each child of the final GameObject to the Prefab manually, iterate through children in reverse since they will disappear
                        for (int j = finalGameObject.transform.childCount - 1; j > -1 ; j--)
                        {
                            finalGameObject.transform.GetChild(j).parent = prefabGameObject.transform;
                        }
                        
                        // Delete the the empty final GameObject
                        GameObject.DestroyImmediate(finalGameObject);
                    }
                    
                    // If user wants to add a MeshCollider to the Prefab
                    if (flagAddMeshCollider)
                    {
                        // Get all MeshRenderers in the Prefab
                        MeshRenderer[] meshRenderers = prefabGameObject.GetComponentsInChildren<MeshRenderer>();
                        
                        // Add a MeshCollider to each GameObject with a MeshRenderer
                        foreach (MeshRenderer childMeshRenderer in meshRenderers)
                            childMeshRenderer.gameObject.AddComponent<MeshCollider>();
                    }
                    
                    // ------------------------------------------------------------
                    // Bake mesh modifiers and optional weld into every mesh
                    // "meshes are unique instances at this point; the prefab roots
                    // stay at position 0,0,0 with no rotation and scale 1"
                    // ------------------------------------------------------------
                    bool hasModifiers = meshPositionOffset != Vector3.zero
                        || meshRotationOffset != Vector3.zero
                        || meshScale != Vector3.one;

                    if (hasModifiers || weldVertices)
                    {
                        Matrix4x4 modifier = Matrix4x4.TRS(
                            meshPositionOffset, Quaternion.Euler(meshRotationOffset), meshScale);

                        foreach (MeshFilter prefabMeshFilter in prefabGameObject.GetComponentsInChildren<MeshFilter>())
                        {
                            Mesh mesh = prefabMeshFilter.sharedMesh;
                            if (mesh == null)
                                continue;

                            if (hasModifiers)
                            {
                                Vector3[] vertices = mesh.vertices;
                                for (int v = 0; v < vertices.Length; v++)
                                    vertices[v] = modifier.MultiplyPoint3x4(vertices[v]);
                                mesh.vertices = vertices;

                                // Normals need the inverse-transpose — plain
                                // MultiplyVector skews them under non-uniform scale
                                Matrix4x4 normalModifier = modifier.inverse.transpose;
                                Vector3[] normals = mesh.normals;
                                for (int n = 0; n < normals.Length; n++)
                                    normals[n] = normalModifier.MultiplyVector(normals[n]).normalized;
                                mesh.normals = normals;

                                // Rotated/scaled normals invalidate the tangents
                                if (mesh.uv.Length == mesh.vertexCount)
                                    mesh.RecalculateTangents();

                                mesh.RecalculateBounds();
                            }

                            if (weldVertices)
                                MeshHelper.WeldVertices(mesh);
                        }
                    }

                    // ------------------------------------------------------------
                    // Save Prefab along with any Meshes it uses
                    // ------------------------------------------------------------

                    // Create a Prefab from the new GameObject, saving it as the
                    // model's name.  Two models sharing a name in different
                    // subfolders get a unique suffix instead of overwriting
                    string prefabPath = targetPath + "/Prefabs/" + model[i].name + ".prefab";
                    if (!usedPrefabPaths.Add(prefabPath))
                    {
                        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                        usedPrefabPaths.Add(prefabPath);
                    }

                    // Save the prefab with its meshes embedded "shared MAST
                    // implementation — replaces the index-paired recursion"
                    MeshHelper.SavePrefabWithEmbeddedMeshes(prefabGameObject, prefabPath);

                    }
                    catch (System.Exception exception)
                    {
                        failedModels++;
                        Debug.LogError("MAST Prefab Creator: failed on \"" + model[i].name + "\": " + exception.Message);
                    }
                    finally
                    {
                        // Delete the temp objects from the scene/Hierarchy —
                        // including on failure, which used to strand them
                        if (prefabGameObject != null)
                            GameObject.DestroyImmediate(prefabGameObject);
                        if (finalGameObject != null)
                            GameObject.DestroyImmediate(finalGameObject);
                        finalGameObject = null;
                    }
                }

                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                // Save all changes
                AssetDatabase.SaveAssets();

                // Display a completion summary
                EditorUtility.DisplayDialog("Prefab Creation Complete",
                    "Created " + (model.Length - failedModels) + " prefab(s) in (" + targetPath + "/Prefabs"
                    + ").  The extracted Materials are located in (" + targetPath + "/Materials" + ") and the Prefab Meshes are located in ("
                    + targetPath + "/Meshes).  The original models are no longer required for the prefabs."
                    + (failedModels > 0 ? "\n\n" + failedModels + " model(s) FAILED — see the Console." : "")
                    + (canceled ? "\n\nCanceled before finishing." : ""),
                    "Got It!");

                return true;
            }
            
            // ------------------------------------------------------------------------------------------------
            // Desc:    Map a model's material to its extracted/substituted version.
            //          Guarded: an empty material slot or a model added to the
            //          folder after the extract step keeps its original material
            //          instead of crashing on a missing name
            // ------------------------------------------------------------------------------------------------
            private static Material ResolveSavedMaterial(Material material, List<string> searchMatName, List<Material> savedMat)
            {
                if (material == null)
                    return null;

                int savedIndex = searchMatName.IndexOf(material.name);
                if (savedIndex < 0)
                {
                    Debug.LogWarning("MAST Prefab Creator: material \"" + material.name +
                        "\" was not captured in the extract step — keeping the original.");
                    return material;
                }

                return savedMat[savedIndex];
            }

            // ------------------------------------------------------------------------------------------------
            // Desc:    Create a GameObject from a model, merging everything into a single mesh
            // ------------------------------------------------------------------------------------------------
            // In:      Transform       modelTransform          Transform of the model being searched through
            //          List<string>    searchMatName           Original Material names.  Used to find the object's
            //          Material[]      savedMat                Direct reference to the saved mat files
            //          string          targetPath              Location of the models
            //
            // Out:     GameObject      newGameObject   GameObject being created
            // ------------------------------------------------------------------------------------------------
            
            private GameObject CreateGameObjectWithSingleMesh(Transform sourceTransform, List<string> searchMatName, List<Material> savedMat)
            {
                // Create a new GameObject that will become the Prefab of this Model, and name it after the Model
                GameObject newGameObject = new GameObject(sourceTransform.gameObject.name);
                
                // Get all MeshRenderers from the source GameObject, including all
                // children "the combiner pairs each renderer with its own filter"
                MeshRenderer[] modelMeshRenderers = sourceTransform.gameObject.GetComponentsInChildren<MeshRenderer>();

                // Create a List containing all unique Materials found in the Model
                List<Material> uniqueMats = MAST.Tools.MeshHelper.GetUniqueMaterialListFromMeshRendererArray(modelMeshRenderers);

                // Create a GameObject with all SubMeshes combined into a single Mesh
                GameObject combineMeshGameObject = MAST.Tools.MeshHelper.CombineAllMeshesInGameObject(uniqueMats, modelMeshRenderers);

                // Use the combined mesh directly — instantiating a copy leaked
                // the original "meshes are never garbage collected"
                MeshFilter gameObjectMeshFilter = newGameObject.AddComponent<MeshFilter>();
                gameObjectMeshFilter.sharedMesh = combineMeshGameObject.GetComponent<MeshFilter>().sharedMesh;
                gameObjectMeshFilter.sharedMesh.name = sourceTransform.gameObject.name + "_mesh";
                
                // Create MeshRenderer component in the final GameObject and copy all Materials from the combined GameObject
                MeshRenderer finalMeshRenderer = newGameObject.AddComponent<MeshRenderer>();
                
                // Prepare Material List for the final GameObject "may have subsituted materials"
                List<Material> finalMats = new List<Material>();
                
                // Get the MeshRenderer from the combined Mesh GameObject
                MeshRenderer combineMeshRenderer = combineMeshGameObject.transform.GetComponent<MeshRenderer>();
                
                // ------------------------------------------------------------
                // Replace the Model's Materials with any chosen substitutions
                // ------------------------------------------------------------
                // Loop through each material in this MeshRenderer's sharedMaterials array
                foreach (Material material in combineMeshRenderer.sharedMaterials)
                {
                    finalMats.Add(ResolveSavedMaterial(material, searchMatName, savedMat));
                }
                
                // Destroy temporary Combine GameObject
                GameObject.DestroyImmediate(combineMeshGameObject);
                
                // Save the final Material array "including substitutions"
                finalMeshRenderer.sharedMaterials = finalMats.ToArray();
                // ------------------------------------------------------------
                
                // Return with the new GameObject
                return newGameObject;
            }
            
            
            
            // ------------------------------------------------------------------------------------------------
            // Desc:    Recursive search to create a GameObject from the components of a model
            // ------------------------------------------------------------------------------------------------
            // In:      Transform       modelTransform  Transform of the model being searched through
            //          List<string>    searchMatName   Original Material names.  Used to find the object's
            //          Material[]      savedMat        Direct reference to the saved mat files
            //          string          targetPath      Location of the models
            //          string          saveName        Name of the model
            //          int             saveIndex       0 for first child in array, 1 for 2nd child in array
            //
            // Out:     GameObject      newGameObject   GameObject being created
            // ------------------------------------------------------------------------------------------------
            
            private GameObject CreateGameObjectWithPreservedHierarchy(Transform sourceTransform, List<string> searchMatName,
                                                        List<Material> savedMat, string saveName, int saveIndex = 0)
            {
                // Add to the current saved name with this child's index
                saveName = saveName + "_" + saveIndex;
                
                // Create a new GameObject to hold this model's data from this child level
                GameObject newGameObject = new GameObject();
                
                // Rename the GameObject to the model's name at this child level
                newGameObject.name = sourceTransform.gameObject.name;
                
                // --------------------------
                // Mesh
                // --------------------------
                
                // Get this model's MeshRenderer
                MeshRenderer modelMeshRenderer = sourceTransform.gameObject.GetComponent<MeshRenderer>();
                
                // If MeshRenderer is found
                if (modelMeshRenderer)
                {
                    // Create MeshFilter on new GameObject and copy the Mesh from the source
                    MeshFilter gameObjectMeshFilter = newGameObject.AddComponent<MeshFilter>();
                    gameObjectMeshFilter.sharedMesh = (Mesh)GameObject.Instantiate(sourceTransform.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    gameObjectMeshFilter.sharedMesh.name = saveName + "_mesh";
                    
                    // --------------------------
                    // Materials
                    // --------------------------
                    
                    // Add MeshRenderer component to the GameObject
                    MeshRenderer gameObjectMeshRenderer = newGameObject.AddComponent<MeshRenderer>();
                    
                    // Create Material List for the GameObject
                    List<Material> gameObjectMats = new List<Material>();
                    
                    // Loop through each material in the Material list
                    foreach (Material material in modelMeshRenderer.sharedMaterials)
                    {
                        gameObjectMats.Add(ResolveSavedMaterial(material, searchMatName, savedMat));
                    }
                    
                    // Add material array to the GameObject's MeshRenderer
                    gameObjectMeshRenderer.sharedMaterials = gameObjectMats.ToArray();
                }
                
                // --------------------------
                // Transforms
                // --------------------------
                
                // Apply transforms to the GameObject
                newGameObject.transform.position = sourceTransform.position;
                newGameObject.transform.rotation = sourceTransform.rotation;
                newGameObject.transform.localScale = sourceTransform.lossyScale;
                
                // --------------------------
                // Children
                // --------------------------
                
                // Run this method for all child transforms
                for (int i = 0; i < sourceTransform.childCount; i++)
                {
                    // Create a new GameObject for this child GameObject in the model
                    GameObject newChildGameObject = CreateGameObjectWithPreservedHierarchy(sourceTransform.GetChild(i),
                        searchMatName, savedMat, saveName, i);
                    
                    // Attach it as a child of this new GameObject
                    newChildGameObject.transform.parent = newGameObject.transform;
                }
                
                // Return this new GameObject
                return newGameObject;
            }
            
            
            
            // ------------------------------------------------------------------------------------------------
            // Desc:    Recursive search to create a GameObject from the components of a model
            //          with all children merged.
            // ------------------------------------------------------------------------------------------------
            // In:      Transform       modelTransform  Transform of the model being searched through
            //          List<string>    searchMatName   Original Material names.  Used to find the object's
            //          Material[]      savedMat        Direct reference to the saved mat files
            //          string          saveName        Name of the model
            //          int             saveIndex       0 for first child in array, 1 for 2nd child in array
            //
            // Out:     GameObject      newGameObject   GameObject being created
            // ------------------------------------------------------------------------------------------------
            
            private GameObject CreateGameObjectWithMergedChildren(Transform sourceTransform, List<string> searchMatName,
                                                        List<Material> savedMat, string saveName, int saveIndex = 0)
            {
                // Add to the current saved name with this child's index
                saveName = saveName + "_" + saveIndex;
                
                // Create a new GameObject to hold this model's data from this child level
                GameObject newGameObject = new GameObject();
                
                // Rename the GameObject to the model's name at this child level
                newGameObject.name = sourceTransform.gameObject.name;
                
                // Get this model's MeshRenderer
                MeshRenderer modelMeshRenderer = sourceTransform.gameObject.GetComponent<MeshRenderer>();
                
                // If MeshRenderer is found
                if (modelMeshRenderer)
                {
                    // ------------------------------------------
                    // Copy mesh from source to new GameObject
                    // ------------------------------------------
                    
                    // Create MeshFilter on new GameObject and copy the Mesh from the source
                    MeshFilter gameObjectMeshFilter = newGameObject.AddComponent<MeshFilter>();
                    gameObjectMeshFilter.sharedMesh = (Mesh)GameObject.Instantiate(sourceTransform.gameObject.GetComponent<MeshFilter>().sharedMesh);
                    
                    // ------------------------------------------
                    // Materials
                    // ------------------------------------------
                    
                    // Add MeshRenderer component to the GameObject
                    MeshRenderer gameObjectMeshRenderer = newGameObject.AddComponent<MeshRenderer>();
                    
                    // Create Material List for the GameObject
                    List<Material> gameObjectMats = new List<Material>();
                    
                    // Loop through each material in the Material list
                    foreach (Material material in modelMeshRenderer.sharedMaterials)
                    {
                        gameObjectMats.Add(ResolveSavedMaterial(material, searchMatName, savedMat));
                    }
                    
                    // Add material array to the GameObject's MeshRenderer
                    gameObjectMeshRenderer.sharedMaterials = gameObjectMats.ToArray();
                }
                
                // --------------------------
                // Transforms
                // --------------------------
                
                // Apply transforms to the GameObject
                newGameObject.transform.position = sourceTransform.position;
                newGameObject.transform.rotation = sourceTransform.rotation;
                newGameObject.transform.localScale = sourceTransform.lossyScale;
                
                // --------------------------
                // Children
                // --------------------------
                
                List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

                int childIndex = 0;
                
                // Run this method for all child transforms
                for (int i = 0; i < sourceTransform.childCount; i++)
                {
                    // If this child GameObject has its own children
                    if (sourceTransform.GetChild(i).childCount > 0)
                    {
                        // Increase child index
                        childIndex += 1;
                        
                        // Create a new GameObject for this child GameObject in the model
                        GameObject newChildGameObject = CreateGameObjectWithMergedChildren(sourceTransform.GetChild(i),
                            searchMatName, savedMat, saveName, childIndex);
                        
                        // Attach it as a child of this new GameObject
                        newChildGameObject.transform.parent = newGameObject.transform;
                    }
                    
                    // If this child GameObject has no children
                    else
                    {
                        // Add the child's MeshRenderer to the combine list —
                        // empty leaf nodes "attach points, sockets" are common in
                        // FBX exports and used to crash the combine with a null
                        MeshRenderer childRenderer = sourceTransform.GetChild(i).GetComponent<MeshRenderer>();
                        if (childRenderer != null)
                            meshRenderers.Add(childRenderer);
                    }
                }
                
                // If any meshes were found to combine
                if (meshRenderers.Count > 0)
                {
                    
                    // Get list of mats in this GameObject
                    List<Material> matList = MAST.Tools.MeshHelper.GetUniqueMaterialListFromMeshRendererArray(meshRenderers.ToArray());
                    
                    // Create a GameObject with all SubMeshes combined into a single Mesh
                    GameObject combineMeshGameObject = MAST.Tools.MeshHelper.CombineAllMeshesInGameObject(matList, meshRenderers.ToArray());

                    matList.Clear();

                    // Loop through each material in this MeshRenderer's sharedMaterials array
                    foreach (Material material in combineMeshGameObject.GetComponent<MeshRenderer>().sharedMaterials)
                    {
                        matList.Add(ResolveSavedMaterial(material, searchMatName, savedMat));
                    }
                    
                    // Save the final Material array "including substitutions"
                    combineMeshGameObject.GetComponent<MeshRenderer>().sharedMaterials = matList.ToArray();
                    
                    // Re-pivot the combined mesh: the combiner bakes vertices in
                    // model-root space with the merged object at the origin, which
                    // gave every merged group the top parent's pivot.  Transform the
                    // vertices into THIS GameObject's local space so the group keeps
                    // its own parent's pivot.
                    Mesh combinedMesh = combineMeshGameObject.GetComponent<MeshFilter>().sharedMesh;
                    Matrix4x4 intoLocal = sourceTransform.worldToLocalMatrix;

                    Vector3[] vertices = combinedMesh.vertices;
                    for (int v = 0; v < vertices.Length; v++)
                        vertices[v] = intoLocal.MultiplyPoint3x4(vertices[v]);
                    combinedMesh.vertices = vertices;

                    // Normals need the inverse-transpose "MultiplyVector alone
                    // skews them under non-uniform scale"
                    Matrix4x4 intoLocalNormals = intoLocal.inverse.transpose;
                    Vector3[] normals = combinedMesh.normals;
                    for (int n = 0; n < normals.Length; n++)
                        normals[n] = intoLocalNormals.MultiplyVector(normals[n]).normalized;
                    combinedMesh.normals = normals;

                    combinedMesh.RecalculateBounds();

                    // Name GameObject and Mesh and make it a child of the current
                    // GameObject at local identity "the vertices are local now"
                    combineMeshGameObject.name = saveName + "_0";
                    combineMeshGameObject.GetComponent<MeshFilter>().sharedMesh.name = saveName + "_0_mesh";
                    combineMeshGameObject.transform.SetParent(newGameObject.transform, false);
                    combineMeshGameObject.transform.localPosition = Vector3.zero;
                    combineMeshGameObject.transform.localRotation = Quaternion.identity;
                    combineMeshGameObject.transform.localScale = Vector3.one;
                }
                
                // Return this new GameObject
                return newGameObject;
            }
            
            
        }
    }
}
