using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Tools
    {
        // Headless merge pipeline behind the Merge Meshes window: copy the source,
        // build one mesh per material, apply per-material edge handling, route each
        // material by its action (merge with all / merge separately / delete), save
        // prefabs with embedded meshes, place scene instances, disable the source.
        // No GUI here — the window is a thin front-end and this stays scriptable.
        public class MergeMeshesOperation
        {
            public class MaterialRule
            {
                public Material material;
                public MergeAction action = MergeAction.MergeWithAll;
                public EdgeMode edges = EdgeMode.Keep;
            }

            public GameObject source;
            public List<MaterialRule> rules = new List<MaterialRule>();
            public bool weldVertices = true;
            public float weldTolerance = 0.001f;
            public bool addMeshCollider = true;
            public MergePivot pivot = MergePivot.SourceRoot;

            // ------------------------------------------------------------------
            // Scanning (used by the window to build the material list)
            // ------------------------------------------------------------------

            public static List<Material> GetUniqueMaterials(GameObject source)
            {
                if (source == null)
                    return new List<Material>();

                return MeshHelper.GetUniqueMaterialListFromMeshRendererArray(
                    GetIncludedRenderers(source));
            }

            public static MeshRenderer[] GetIncludedRenderers(GameObject source)
            {
                // Include inactive children "a temporarily disabled child used
                // to silently vanish from the merged prefab", and honor the
                // exclude flag for whole SUBTREES so the scan matches the run
                var included = new List<MeshRenderer>();
                foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (IncludeInMerge(renderer.gameObject, source.transform))
                        included.Add(renderer);
                }
                return included.ToArray();
            }

            // A MAST prefab component can flag its GameObject "and everything
            // under it" to stay out of merges — the flag applies to the subtree
            private static bool IncludeInMerge(GameObject go, Transform root)
            {
                for (Transform current = go.transform; current != null; current = current.parent)
                {
                    var prefabComponent = current.GetComponent<MAST.Component.MASTPrefabSettings>();
                    if (prefabComponent != null && !prefabComponent.includeInMerge)
                        return false;

                    if (current == root)
                        break;
                }

                return true;
            }

            // ------------------------------------------------------------------
            // The merge
            // ------------------------------------------------------------------

            public bool Run(string mainPrefabPath, out string report)
            {
                report = "";

                if (source == null)
                {
                    report = "No source GameObject.";
                    return false;
                }

                // A prefab selected in the Project window is an ASSET — merging
                // it used to SetActive(false) the asset itself, so every
                // instance everywhere spawned disabled
                if (!source.scene.IsValid())
                {
                    report = "The source is a prefab asset.  Select the instance in the Hierarchy instead.";
                    return false;
                }

                // Capture where instances should be placed afterwards
                Vector3 sourcePosition = source.transform.position;
                Quaternion sourceRotation = source.transform.rotation;
                Vector3 sourceScale = source.transform.localScale;
                Transform sourceParent = source.transform.parent;

                // Work on a copy so the original is never modified
                GameObject workingCopy = Object.Instantiate(source);
                workingCopy.name = source.name;

                var temporaryObjects = new List<GameObject> { workingCopy };
                var savedPrefabs = new List<GameObject>();

                try
                {
                    // Move exclude-from-merge children to their own holder
                    GameObject excludedHolder = new GameObject("Not Merged");
                    temporaryObjects.Add(excludedHolder);

                    // Move only the TOP-MOST excluded transforms: moving every
                    // excluded descendant used to flatten the preserved
                    // hierarchy in the saved prefab
                    Transform[] copyTransforms = workingCopy.GetComponentsInChildren<Transform>(true);
                    for (int i = copyTransforms.Length - 1; i >= 0; i--)
                    {
                        if (copyTransforms[i] == null || copyTransforms[i] == workingCopy.transform)
                            continue;

                        if (!IncludeInMerge(copyTransforms[i].gameObject, workingCopy.transform) &&
                            IncludeInMerge(copyTransforms[i].parent.gameObject, workingCopy.transform))
                            copyTransforms[i].parent = excludedHolder.transform;
                    }

                    MeshRenderer[] renderers = workingCopy.GetComponentsInChildren<MeshRenderer>(true);

                    Transform pivotSpace = pivot == MergePivot.SourceRoot ? workingCopy.transform : null;

                    // --------------------------------------------------------------
                    // Build one processed mesh per non-deleted material
                    // --------------------------------------------------------------
                    var mergedMeshes = new List<Mesh>();
                    var mergedMaterials = new List<Material>();
                    var separateMeshes = new List<Mesh>();
                    var separateMaterials = new List<Material>();
                    int deletedCount = 0;

                    foreach (MaterialRule rule in rules)
                    {
                        if (rule.material == null)
                            continue;

                        if (rule.action == MergeAction.Delete)
                        {
                            deletedCount++;
                            continue;
                        }

                        GameObject partObject = MeshHelper.CombineAllMeshesInGameObject(
                            new List<Material> { rule.material }, renderers, pivotSpace);
                        Mesh partMesh = partObject.GetComponent<MeshFilter>().sharedMesh;
                        Object.DestroyImmediate(partObject);

                        if (partMesh == null || partMesh.vertexCount == 0)
                        {
                            Object.DestroyImmediate(partMesh);
                            continue;
                        }

                        // Per-material edge handling
                        switch (rule.edges)
                        {
                            case EdgeMode.Soften:
                                MeshHelper.SoftenEdges(partMesh, weldTolerance);
                                break;
                            case EdgeMode.Harden:
                                MeshHelper.HardenEdges(partMesh);
                                break;
                        }

                        // Optional weld "hardness-preserving, so Keep/Harden survive it;
                        // Soften already welded"
                        if (weldVertices && rule.edges != EdgeMode.Soften)
                            MeshHelper.WeldVertices(partMesh, weldTolerance, compareNormals: true);

                        if (rule.action == MergeAction.MergeWithAll)
                        {
                            mergedMeshes.Add(partMesh);
                            mergedMaterials.Add(rule.material);
                        }
                        else
                        {
                            separateMeshes.Add(partMesh);
                            separateMaterials.Add(rule.material);
                        }
                    }

                    bool hasExcludedChildren = excludedHolder.transform.childCount > 0;

                    if (mergedMeshes.Count == 0 && separateMeshes.Count == 0 && !hasExcludedChildren)
                    {
                        report = "Nothing to merge — every material was deleted or had no geometry.";
                        return false;
                    }

                    string directory = Path.GetDirectoryName(mainPrefabPath).Replace("\\", "/");
                    string baseName = Path.GetFileNameWithoutExtension(mainPrefabPath);

                    // --------------------------------------------------------------
                    // Main merged prefab "one submesh per merge-with-all material"
                    // --------------------------------------------------------------
                    if (mergedMeshes.Count > 0 || hasExcludedChildren)
                    {
                        GameObject mainObject = new GameObject(baseName);
                        temporaryObjects.Add(mainObject);

                        Mesh mainMesh = null;
                        if (mergedMeshes.Count > 0)
                        {
                            mainMesh = CombineAsSubmeshes(mergedMeshes);
                            mainMesh.name = baseName + "_mesh";

                            // The per-material intermediates are baked into mainMesh now
                            foreach (Mesh partMesh in mergedMeshes)
                                Object.DestroyImmediate(partMesh);

                            var meshFilter = mainObject.AddComponent<MeshFilter>();
                            meshFilter.sharedMesh = mainMesh;
                            var meshRenderer = mainObject.AddComponent<MeshRenderer>();
                            meshRenderer.sharedMaterials = mergedMaterials.ToArray();

                            if (addMeshCollider)
                                mainObject.AddComponent<MeshCollider>().sharedMesh = mainMesh;
                        }

                        // Keep excluded children with the main prefab, positioned
                        // relative to the chosen pivot
                        if (hasExcludedChildren)
                        {
                            if (pivot == MergePivot.SourceRoot)
                            {
                                // Children hold world transforms from the copy; parenting
                                // while the root matches the source, then resetting the
                                // root, leaves them local to the source pivot
                                mainObject.transform.SetPositionAndRotation(
                                    workingCopy.transform.position, workingCopy.transform.rotation);
                                mainObject.transform.localScale = workingCopy.transform.lossyScale;
                            }

                            for (int i = excludedHolder.transform.childCount - 1; i >= 0; i--)
                                excludedHolder.transform.GetChild(i).SetParent(mainObject.transform, true);

                            mainObject.transform.position = Vector3.zero;
                            mainObject.transform.rotation = Quaternion.identity;
                            mainObject.transform.localScale = Vector3.one;
                        }

                        SavePrefabWithMesh(mainObject, mainMesh, mainPrefabPath, savedPrefabs);
                    }

                    // --------------------------------------------------------------
                    // One separate prefab per merge-separately material
                    // --------------------------------------------------------------
                    var usedPartPaths = new HashSet<string> { mainPrefabPath };

                    for (int i = 0; i < separateMeshes.Count; i++)
                    {
                        string materialName = SanitizeFileName(separateMaterials[i].name);
                        string partPath = directory + "/" + baseName + "_" + materialName + ".prefab";

                        // Two DIFFERENT materials sharing a name "MagicaVoxel's
                        // all-Root case" used to silently overwrite each other
                        if (!usedPartPaths.Add(partPath))
                        {
                            partPath = AssetDatabase.GenerateUniqueAssetPath(partPath);
                            usedPartPaths.Add(partPath);
                        }

                        GameObject partObject = new GameObject(baseName + "_" + materialName);
                        temporaryObjects.Add(partObject);

                        separateMeshes[i].name = baseName + "_" + materialName + "_mesh";
                        var meshFilter = partObject.AddComponent<MeshFilter>();
                        meshFilter.sharedMesh = separateMeshes[i];
                        var meshRenderer = partObject.AddComponent<MeshRenderer>();
                        meshRenderer.sharedMaterial = separateMaterials[i];

                        if (addMeshCollider)
                            partObject.AddComponent<MeshCollider>().sharedMesh = separateMeshes[i];

                        SavePrefabWithMesh(partObject, separateMeshes[i], partPath, savedPrefabs);
                    }

                    AssetDatabase.SaveAssets();

                    // --------------------------------------------------------------
                    // Place instances of the new prefabs and disable the source
                    // --------------------------------------------------------------
                    foreach (GameObject prefab in savedPrefabs)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        instance.transform.SetParent(sourceParent, true);

                        if (pivot == MergePivot.SourceRoot)
                        {
                            instance.transform.SetPositionAndRotation(sourcePosition, sourceRotation);
                            instance.transform.localScale = sourceScale;
                        }

                        Undo.RegisterCreatedObjectUndo(instance, "Merge Meshes");
                    }

                    Undo.RecordObject(source, "Merge Meshes");
                    source.SetActive(false);

                    report = "Merged " + mergedMeshes.Count + " material(s) into " + baseName + ".prefab"
                        + (separateMeshes.Count > 0 ? ", plus " + separateMeshes.Count + " separate prefab(s)" : "")
                        + (deletedCount > 0 ? ", deleted " + deletedCount + " material(s)" : "")
                        + ".  Instances were placed in the scene and the source was disabled.";
                    return true;
                }
                finally
                {
                    // Remove every temporary scene object "instances and assets stay"
                    foreach (GameObject temporary in temporaryObjects)
                    {
                        if (temporary != null)
                            Object.DestroyImmediate(temporary);
                    }
                }
            }

            // ------------------------------------------------------------------
            // Helpers
            // ------------------------------------------------------------------

            // Combine single-submesh meshes into one mesh with one submesh each
            private static Mesh CombineAsSubmeshes(List<Mesh> meshes)
            {
                var instances = new CombineInstance[meshes.Count];
                for (int i = 0; i < meshes.Count; i++)
                {
                    instances[i].mesh = meshes[i];
                    instances[i].transform = Matrix4x4.identity;
                }

                Mesh combined = new Mesh();
                combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combined.CombineMeshes(instances, false, true);
                combined.RecalculateBounds();
                return combined;
            }

            // Save the prefab and embed its meshes "shared MAST implementation"
            private static void SavePrefabWithMesh(GameObject sceneObject, Mesh mesh, string path, List<GameObject> savedPrefabs)
            {
                savedPrefabs.Add(MeshHelper.SavePrefabWithEmbeddedMeshes(sceneObject, path));
            }

            private static string SanitizeFileName(string name)
            {
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    name = name.Replace(invalid, '_');
                return name;
            }
        }
    }
}
