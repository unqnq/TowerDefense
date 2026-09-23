using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MAST
{
    namespace Tools
    {
        // Headless pipeline behind the Replace Materials window: scan a prefab
        // folder for its unique materials, then swap chosen replacements into
        // every prefab in that folder.  Prefab assets are edited in place.
        public class ReplaceMaterialsOperation
        {
            public class MaterialRule
            {
                public Material original;
                public Material replacement;   // null = "Unchanged"
            }

            public string prefabFolder = "";
            public readonly List<MaterialRule> rules = new List<MaterialRule>();

            public int PrefabCount { get; private set; }

            // ------------------------------------------------------------------
            // Scan the folder: every unique material used by every prefab
            // ------------------------------------------------------------------
            public void Scan()
            {
                rules.Clear();
                PrefabCount = 0;

                if (string.IsNullOrEmpty(prefabFolder) || !AssetDatabase.IsValidFolder(prefabFolder))
                    return;

                var uniqueMaterials = new List<Material>();

                foreach (string path in GetPrefabPaths())
                {
                    PrefabCount++;

                    // Main asset = the prefab ROOT; loading by GameObject type
                    // can return a child object, whose subtree would miss the
                    // rest of the prefab's renderers
                    var prefab = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                    if (prefab == null)
                        continue;

                    // Shared MAST scanner "identity-keyed, null-safe"
                    foreach (Material material in MeshHelper.GetUniqueMaterials(
                        prefab.GetComponentsInChildren<Renderer>(true)))
                    {
                        if (!uniqueMaterials.Contains(material))
                            uniqueMaterials.Add(material);
                    }
                }

                foreach (Material material in uniqueMaterials.OrderBy(material => material.name))
                    rules.Add(new MaterialRule { original = material });
            }

            public int ReplacementCount()
            {
                int count = 0;
                foreach (MaterialRule rule in rules)
                {
                    if (rule.replacement != null && rule.replacement != rule.original)
                        count++;
                }
                return count;
            }

            // ------------------------------------------------------------------
            // Apply every chosen replacement to every prefab in the folder
            // ------------------------------------------------------------------
            public bool Run(out string report)
            {
                var replacements = new Dictionary<Material, Material>();
                foreach (MaterialRule rule in rules)
                {
                    if (rule.original != null && rule.replacement != null && rule.replacement != rule.original)
                        replacements[rule.original] = rule.replacement;
                }

                if (replacements.Count == 0)
                {
                    report = "No replacement materials were chosen — nothing to do.";
                    return false;
                }

                int changedPrefabs = 0;
                int changedSlots = 0;
                int failedPrefabs = 0;
                bool canceled = false;

                var paths = new List<string>(GetPrefabPaths());

                try
                {
                    // Batch the asset writes "each SaveAsPrefabAsset used to
                    // trigger its own reimport, multiplying the wall time"
                    AssetDatabase.StartAssetEditing();

                    for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                    {
                        string path = paths[pathIndex];

                        if (EditorUtility.DisplayCancelableProgressBar(
                            "Replace Materials", path, (float)pathIndex / paths.Count))
                        {
                            canceled = true;
                            break;
                        }

                        GameObject root = PrefabUtility.LoadPrefabContents(path);

                        // One bad prefab "read-only file, VCS lock" must neither
                        // leak the loaded contents nor abort the rest
                        try
                        {
                            bool changed = false;

                            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                            {
                                Material[] sharedMaterials = renderer.sharedMaterials;
                                bool rendererChanged = false;

                                for (int i = 0; i < sharedMaterials.Length; i++)
                                {
                                    if (sharedMaterials[i] != null &&
                                        replacements.TryGetValue(sharedMaterials[i], out Material replacement))
                                    {
                                        sharedMaterials[i] = replacement;
                                        rendererChanged = true;
                                        changedSlots++;
                                    }
                                }

                                if (rendererChanged)
                                {
                                    renderer.sharedMaterials = sharedMaterials;
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                PrefabUtility.SaveAsPrefabAsset(root, path);
                                changedPrefabs++;
                            }
                        }
                        catch (System.Exception exception)
                        {
                            failedPrefabs++;
                            Debug.LogError("MAST: Replace Materials failed on \"" + path + "\": " + exception.Message);
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();
                }

                AssetDatabase.SaveAssets();

                report = "Replaced " + replacements.Count + " material(s) across "
                    + changedSlots + " material slot(s) in " + changedPrefabs + " prefab(s)."
                    + (failedPrefabs > 0 ? "  " + failedPrefabs + " prefab(s) FAILED — see the Console." : "")
                    + (canceled ? "  Canceled before finishing." : "");
                return true;
            }

            private IEnumerable<string> GetPrefabPaths()
            {
                foreach (string guid in AssetDatabase.FindAssets("t:prefab", new[] { prefabFolder }))
                    yield return AssetDatabase.GUIDToAssetPath(guid);
            }
        }
    }
}
