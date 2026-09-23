using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Tools
    {
        public static class MeshHelper
        {
            // ------------------------------------------------------------------------------------------------
            // The ONE unique-material scanner for every MAST tool: identity-keyed
            // "imported assets can share a name — MagicaVoxel imports are all
            // 'Root' — while being different materials" and null-safe "empty
            // material slots are common on imported models".
            // ------------------------------------------------------------------------------------------------
            public static List<Material> GetUniqueMaterials(IEnumerable<Renderer> renderers)
            {
                var uniqueMats = new List<Material>();

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && !uniqueMats.Contains(material))
                            uniqueMats.Add(material);
                    }
                }

                return uniqueMats;
            }

            // Kept as the established call surface for the merge pipeline
            public static List<Material> GetUniqueMaterialListFromMeshRendererArray(MeshRenderer[] sourceMeshRenderers)
            {
                return GetUniqueMaterials(sourceMeshRenderers);
            }
            
            // ----------------------------------------------------------------------------------------------------
            // Combine Meshes that include multiple Materials
            // ----------------------------------------------------------------------------------------------------
            // In:      List<Material>      matsInCombine           Materials to include in the Combine operation.
            //                                                      Any Submesh using a Material not in this List
            //                                                      will be ignored.
            //
            //          MeshFilter[]        sourceMeshFilters       MeshFilters containing all Meshes to combine
            //
            //          MeshRenderer[]      sourceMeshRenderers     MeshRenderers containing all Mesh Materials.
            //                                                      This will line up with [sourceMeshFilters]. 
            // ----------------------------------------------------------------------------------------------------
            // Out:     GameObject containing all Meshes combined
            // ----------------------------------------------------------------------------------------------------
            private class MeshCombine
            {
                public List<CombineInstance> combineList;
            }
            
            // pivotSpace: when supplied, vertices are baked relative to that transform
            // instead of world space, so the combined mesh pivots there.
            //
            // The MeshFilter for each renderer is looked up FROM the renderer:
            // parallel filter/renderer arrays from separate GetComponentsInChildren
            // calls misalign whenever a child has one component but not the other
            // "collision-only meshes", which used to merge the wrong geometry.
            public static GameObject CombineAllMeshesInGameObject(List<Material> matsInCombine, MeshRenderer[] sourceMeshRenderers, Transform pivotSpace = null)
            {
                // ---------------------------------------------------------------------------
                // Extract Meshes into Separate CombineInstances based on Material
                // ---------------------------------------------------------------------------

                // Create a MeshCombine Class Array the size of the uniqueMats List and initialize
                MeshCombine[] uniqueMatMeshCombine = new MeshCombine[matsInCombine.Count];
                for (int i = 0; i < matsInCombine.Count; i++)
                {
                    uniqueMatMeshCombine[i] = new MeshCombine();
                    uniqueMatMeshCombine[i].combineList = new List<CombineInstance>();
                }

                // Prepare variables
                CombineInstance combineInstance;

                // Loop through each MeshRenderer in sourceMeshRenderers
                for (int i = 0; i < sourceMeshRenderers.Length; i++)
                {
                    if (sourceMeshRenderers[i] == null)
                        continue;

                    // The renderer's OWN MeshFilter — a renderer without a mesh
                    // contributes nothing instead of throwing
                    MeshFilter sourceFilter = sourceMeshRenderers[i].GetComponent<MeshFilter>();
                    if (sourceFilter == null || sourceFilter.sharedMesh == null)
                        continue;

                    Mesh sourceMesh = sourceFilter.sharedMesh;

                    // Loop through each Material in each MeshRenderer
                    for (int j = 0; j < sourceMeshRenderers[i].sharedMaterials.Length; j++)
                    {
                        // Loop through each Material in the uniqueMats List
                        for (int k = 0; k < matsInCombine.Count; k++)
                        {
                            // If this Material matches the Material in the uniqueMats List
                            if (sourceMeshRenderers[i].sharedMaterials[j] == matsInCombine[k])
                            {
                                // Initialize a Combine Instance
                                combineInstance = new CombineInstance();

                                // Copy this mesh to the Combine Instance
                                combineInstance.mesh = sourceMesh;

                                // Set it to only include the Mesh with the specified
                                // material.  A renderer can carry more materials
                                // than the mesh has submeshes — Unity renders the
                                // last submesh again for the extras, so clamp
                                combineInstance.subMeshIndex = Mathf.Min(j, sourceMesh.subMeshCount - 1);

                                // Transform to world "or pivot" space
                                combineInstance.transform = pivotSpace != null
                                    ? pivotSpace.worldToLocalMatrix * sourceFilter.transform.localToWorldMatrix
                                    : sourceFilter.transform.localToWorldMatrix;

                                // Add this CombineInstance to the appropriate CombineInstance List (by Material)
                                uniqueMatMeshCombine[k].combineList.Add(combineInstance);
                            }
                        }
                    }
                }

                // ---------------------------------------------------------------------------
                // Combine all Mesh Instances into a single GameObject
                // ---------------------------------------------------------------------------

                // Create the final GameObject that will hold all the other GameObjects
                GameObject finalGameObject = new GameObject("Merged Meshes");

                // Combine meshes for each material into one intermediate mesh
                CombineInstance[] finalCombineInstance = new CombineInstance[matsInCombine.Count];

                // Loop through each Material in the uniqueMats List
                for (int i = 0; i < matsInCombine.Count; i++)
                {
                    var perMaterialMesh = new Mesh();
                    perMaterialMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    perMaterialMesh.CombineMeshes(uniqueMatMeshCombine[i].combineList.ToArray());

                    // Add this Mesh to the final Mesh Combine
                    finalCombineInstance[i].mesh = perMaterialMesh;
                    finalCombineInstance[i].transform = Matrix4x4.identity;
                }

                // Combine the Mesh, optimize it, and recalculate the bounds
                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(finalCombineInstance, false, false);
                MeshUtility.Optimize(mesh);
                mesh.RecalculateBounds();

                // The per-material intermediates were copied into the final mesh —
                // destroy them "meshes aren't garbage collected; a big batch used
                // to leak hundreds of them until the next domain reload"
                for (int i = 0; i < finalCombineInstance.Length; i++)
                    Object.DestroyImmediate(finalCombineInstance[i].mesh);

                // Add MeshFilter to final GameObject and Combine all Meshes
                MeshFilter finalMeshFilter = finalGameObject.AddComponent<MeshFilter>();
                finalMeshFilter.sharedMesh = mesh;

                // Add MeshRenderer to final GameObject Attach Materials
                MeshRenderer finalMeshRenderer = finalGameObject.AddComponent<MeshRenderer>();
                finalMeshRenderer.sharedMaterials = matsInCombine.ToArray();

                return finalGameObject;
            }
            
            // ------------------------------------------------------------------------------------------------
            // Save a scene object as a prefab and embed every non-asset mesh it
            // uses inside the prefab asset.  ONE implementation for every MAST
            // tool "Merge Meshes and the Prefab Creator each had their own,
            // one of them a fragile index-paired recursion".  Meshes that are
            // already assets "model files" are left referenced, not embedded.
            // ------------------------------------------------------------------------------------------------
            public static GameObject SavePrefabWithEmbeddedMeshes(GameObject sceneObject, string path)
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sceneObject, path);

                foreach (MeshFilter prefabFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = prefabFilter.sharedMesh;

                    if (mesh == null || EditorUtility.IsPersistent(mesh))
                        continue;

                    AssetDatabase.AddObjectToAsset(mesh, prefab);
                    prefabFilter.sharedMesh = mesh;

                    // A collider sharing the render mesh follows it into the asset
                    MeshCollider meshCollider = prefabFilter.GetComponent<MeshCollider>();
                    if (meshCollider != null &&
                        (meshCollider.sharedMesh == null || meshCollider.sharedMesh == mesh))
                        meshCollider.sharedMesh = mesh;
                }

                return prefab;
            }

            // ------------------------------------------------------------------------------------------------
            // Weld duplicate vertices in place.  UVs are always compared so texture
            // seams are never corrupted.  compareNormals = true preserves hard edges
            // "split vertices with different normals stay split"; false merges across
            // them, which is how SoftenEdges prepares a mesh before recalculating
            // averaged normals.  Submeshes are preserved.
            // ------------------------------------------------------------------------------------------------
            public static Mesh WeldVertices(Mesh aMesh, float aMaxDelta = 0.001f, bool compareNormals = true)
            {
                Vector3[] verts = aMesh.vertices;
                if (verts.Length == 0)
                    return aMesh;

                Vector3[] normals = aMesh.normals;
                Vector4[] tangents = aMesh.tangents;
                Vector2[] uvs = aMesh.uv;
                Vector2[] uvs2 = aMesh.uv2;
                Color[] colors = aMesh.colors;

                bool hasNormals = normals.Length == verts.Length;
                bool hasTangents = tangents.Length == verts.Length;
                bool hasUVs = uvs.Length == verts.Length;
                bool hasUVs2 = uvs2.Length == verts.Length;
                bool hasColors = colors.Length == verts.Length;

                // Bucket vertices by quantized position/uv "and normal when hardness
                // must be preserved" — O(n) instead of the old O(n^2) scan
                float quantize = 1f / Mathf.Max(aMaxDelta, 1e-6f);
                var lookup = new Dictionary<(int, int, int, int, int, int, int, int), int>(verts.Length);
                int[] map = new int[verts.Length];
                List<int> keep = new List<int>(verts.Length);

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = verts[i] * quantize;
                    Vector2 uv = hasUVs ? uvs[i] * quantize : Vector2.zero;
                    Vector3 n = (compareNormals && hasNormals) ? normals[i] * 100f : Vector3.zero;

                    var key = (Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z),
                               Mathf.RoundToInt(uv.x), Mathf.RoundToInt(uv.y),
                               Mathf.RoundToInt(n.x), Mathf.RoundToInt(n.y), Mathf.RoundToInt(n.z));

                    if (lookup.TryGetValue(key, out int existing))
                    {
                        map[i] = existing;
                    }
                    else
                    {
                        map[i] = keep.Count;
                        lookup[key] = keep.Count;
                        keep.Add(i);
                    }
                }

                // Nothing to weld
                if (keep.Count == verts.Length)
                    return aMesh;

                // Rebuild attribute arrays from the kept vertices
                var newVerts = new Vector3[keep.Count];
                var newNormals = hasNormals ? new Vector3[keep.Count] : null;
                var newTangents = hasTangents ? new Vector4[keep.Count] : null;
                var newUVs = hasUVs ? new Vector2[keep.Count] : null;
                var newUVs2 = hasUVs2 ? new Vector2[keep.Count] : null;
                var newColors = hasColors ? new Color[keep.Count] : null;

                for (int i = 0; i < keep.Count; i++)
                {
                    int a = keep[i];
                    newVerts[i] = verts[a];
                    if (hasNormals) newNormals[i] = normals[a];
                    if (hasTangents) newTangents[i] = tangents[a];
                    if (hasUVs) newUVs[i] = uvs[a];
                    if (hasUVs2) newUVs2[i] = uvs2[a];
                    if (hasColors) newColors[i] = colors[a];
                }

                // Remap triangles per submesh, then rebuild the mesh
                int subMeshCount = aMesh.subMeshCount;
                var subMeshTris = new int[subMeshCount][];
                for (int s = 0; s < subMeshCount; s++)
                {
                    subMeshTris[s] = aMesh.GetTriangles(s);
                    for (int t = 0; t < subMeshTris[s].Length; t++)
                        subMeshTris[s][t] = map[subMeshTris[s][t]];
                }

                aMesh.Clear();
                aMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                aMesh.vertices = newVerts;
                if (hasNormals) aMesh.normals = newNormals;
                if (hasTangents) aMesh.tangents = newTangents;
                if (hasUVs) aMesh.uv = newUVs;
                if (hasUVs2) aMesh.uv2 = newUVs2;
                if (hasColors) aMesh.colors = newColors;
                aMesh.subMeshCount = subMeshCount;
                for (int s = 0; s < subMeshCount; s++)
                    aMesh.SetTriangles(subMeshTris[s], s);

                aMesh.RecalculateBounds();
                return aMesh;
            }

            // ------------------------------------------------------------------------------------------------
            // Make all edges soft: weld across normal splits, then average the normals
            // ------------------------------------------------------------------------------------------------
            public static Mesh SoftenEdges(Mesh aMesh, float weldTolerance = 0.001f)
            {
                WeldVertices(aMesh, weldTolerance, compareNormals: false);
                aMesh.RecalculateNormals();

                // New normals invalidate the welded tangents — stale tangents
                // break normal mapping on lit shaders
                if (aMesh.uv.Length == aMesh.vertexCount)
                    aMesh.RecalculateTangents();

                return aMesh;
            }

            // ------------------------------------------------------------------------------------------------
            // Make all edges hard: give every triangle its own vertices, then
            // recalculate normals "unshared vertices produce flat facets"
            // ------------------------------------------------------------------------------------------------
            public static Mesh HardenEdges(Mesh aMesh)
            {
                Vector3[] verts = aMesh.vertices;
                if (verts.Length == 0)
                    return aMesh;

                Vector2[] uvs = aMesh.uv;
                Vector2[] uvs2 = aMesh.uv2;
                Color[] colors = aMesh.colors;

                bool hasUVs = uvs.Length == verts.Length;
                bool hasUVs2 = uvs2.Length == verts.Length;
                bool hasColors = colors.Length == verts.Length;

                int subMeshCount = aMesh.subMeshCount;
                var subMeshTris = new int[subMeshCount][];
                int totalIndices = 0;
                for (int s = 0; s < subMeshCount; s++)
                {
                    subMeshTris[s] = aMesh.GetTriangles(s);
                    totalIndices += subMeshTris[s].Length;
                }

                var newVerts = new Vector3[totalIndices];
                var newUVs = hasUVs ? new Vector2[totalIndices] : null;
                var newUVs2 = hasUVs2 ? new Vector2[totalIndices] : null;
                var newColors = hasColors ? new Color[totalIndices] : null;
                var newSubMeshTris = new int[subMeshCount][];

                int write = 0;
                for (int s = 0; s < subMeshCount; s++)
                {
                    newSubMeshTris[s] = new int[subMeshTris[s].Length];
                    for (int t = 0; t < subMeshTris[s].Length; t++)
                    {
                        int a = subMeshTris[s][t];
                        newVerts[write] = verts[a];
                        if (hasUVs) newUVs[write] = uvs[a];
                        if (hasUVs2) newUVs2[write] = uvs2[a];
                        if (hasColors) newColors[write] = colors[a];
                        newSubMeshTris[s][t] = write;
                        write++;
                    }
                }

                aMesh.Clear();
                aMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                aMesh.vertices = newVerts;
                if (hasUVs) aMesh.uv = newUVs;
                if (hasUVs2) aMesh.uv2 = newUVs2;
                if (hasColors) aMesh.colors = newColors;
                aMesh.subMeshCount = subMeshCount;
                for (int s = 0; s < subMeshCount; s++)
                    aMesh.SetTriangles(newSubMeshTris[s], s);

                aMesh.RecalculateNormals();

                // The rebuild dropped the tangent array entirely — normal-mapped
                // materials need it regenerated
                if (hasUVs)
                    aMesh.RecalculateTangents();

                aMesh.RecalculateBounds();
                return aMesh;
            }
        }
    }
}

