using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Painting
    {
        public static class Painter
        {
            // Does a paint preview exist?
            private static bool previewExists = false;
            
            // Gameobject, meshrenderer, and sharematerial index of current paint preview
            private static GameObject previewGameObject;
            private static MeshRenderer previewMeshRenderer;
            private static int previewSharedMaterialIndex;
            
            // Backup of the hovered slot's material, and whether the renderer
            // already had a materials override on its prefab instance — a
            // hover preview must not leave a permanent override behind
            private static Material originalMaterial;
            private static bool previewHadMaterialsOverride;

            // A domain reload wipes this class's statics while a previewed
            // material is still really assigned — restore it first
            [InitializeOnLoadMethod]
            private static void RegisterReloadCleanup()
            {
                AssemblyReloadEvents.beforeAssemblyReload += ClearCurrentMaterialPaintPreview;
            }
            
            // Is a painting session active?
            private static bool painting = false;
            
            private static bool paintingFirstMaterial = false;
            
            // Index of the under group for each painting session
            private static int undoGroupIndex;
            
            // ---------------------------------------------------------------------------
            // Show a preview of the material on the face under the mouse pointer
            // ---------------------------------------------------------------------------
            public static void PreviewPaint(Material material)
            {
                // Process the mouse pointer raycast
                ProcessMousePointerRaycast();
                
                // If no material was provided, this is RESTORE mode: the journal
                // says what MAST painted here and what to put back.  Slots MAST
                // never painted "including the user's own intentional overrides"
                // are simply not restorable — that's the point
                bool restoring = material == null;
                if (restoring)
                {
                    // If a gameobject was hit
                    if (previewExists)
                    {
                        material = Journal.GetOriginal(previewMeshRenderer, previewSharedMaterialIndex);

                        // Nothing journaled for this slot — exit here
                        if (material == null)
                            return;
                    }
                }
                
                // If a paint preview exists
                if (previewExists)
                {
                    // If this material is not already applied to the hovered material
                    if (previewMeshRenderer.sharedMaterials[previewSharedMaterialIndex] != material)
                    {
                        // If painting
                        if (painting)
                        {
                            // Add this gameobject's meshrenderer to the undo group
                            Undo.RegisterCompleteObjectUndo(previewMeshRenderer, "");
                        }

                        // Get all materials on the gameobject, change the hovered material, then apply them back to the gameobject
                        ApplyMaterial(material);

                        // If painting, remove the paintpreviewexists flag, making the change permanent
                        if (painting)
                        {
                            // Keep the journal in step with the commit: a paint
                            // records the slot's pre-MAST original; a restore
                            // closes the entry out
                            if (restoring)
                                Journal.RemoveEntry(previewMeshRenderer, previewSharedMaterialIndex);
                            else
                                Journal.RecordPaint(previewMeshRenderer, previewSharedMaterialIndex,
                                    originalMaterial, material);

                            previewExists = false;
                        }
                    }
                    
                    // If this material is already applied to the hovered material
                    else
                    {
                        // If this is the first material being painted
                        if (paintingFirstMaterial)
                        {
                            // Restore the original material
                            ApplyMaterial(originalMaterial);
                            
                            // Add this gameobject's meshrenderer to the undo group
                            Undo.RegisterCompleteObjectUndo(previewMeshRenderer, "");
                            
                            // Add the painted material preview back
                            ApplyMaterial(material);
                            
                            // Mark as done painting the first material
                            paintingFirstMaterial = false;
                        }
                    }
                }
            }
            
            // ---------------------------------------------------------------------------
            // Start a new painting session
            // ---------------------------------------------------------------------------
            public static void StartPainting()
            {
                // Start painting
                painting = true;
                paintingFirstMaterial = true;
                
                // Start undo group that will contain all material changes
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Painted Material(s)");
                undoGroupIndex = Undo.GetCurrentGroup();
            }
            
            // ---------------------------------------------------------------------------
            // Stop the current painting session
            // ---------------------------------------------------------------------------
            public static void StopPainting()
            {
                // Never collapse when no session started: a stray call used to
                // run CollapseUndoOperations(0) and flatten the user's whole
                // undo history into one step
                if (!painting)
                    return;

                Undo.CollapseUndoOperations(undoGroupIndex);

                painting = false;

                // Make sure the last preview is permanent
                previewExists = false;
            }

            private static void ApplyMaterial(Material material)
            {
                // The hovered object can be destroyed under us "undo, delete" —
                // a dead renderer used to throw here on every event until reload
                if (previewMeshRenderer == null ||
                    previewSharedMaterialIndex >= previewMeshRenderer.sharedMaterials.Length)
                {
                    previewExists = false;
                    return;
                }

                Material[] sharedMaterials = previewMeshRenderer.sharedMaterials;
                sharedMaterials[previewSharedMaterialIndex] = material;
                previewMeshRenderer.sharedMaterials = sharedMaterials;
            }

            // ---------------------------------------------------------------------------
            // Remove material paint preview from whatever gameobject it's applied to
            // ---------------------------------------------------------------------------
            public static void ClearCurrentMaterialPaintPreview()
            {
                // If not painting and a preview exists, remove the preview by restoring previous material
                if (!painting)
                    if (previewExists)
                    {
                        ApplyMaterial(originalMaterial);

                        // Restoring the material still leaves an m_Materials
                        // override recorded on a prefab instance; revert it when
                        // the slot had none before the hover, so sweeping the
                        // pointer across placed prefabs doesn't detach them all
                        // from their source materials
                        if (!previewHadMaterialsOverride && previewMeshRenderer != null &&
                            PrefabUtility.IsPartOfPrefabInstance(previewMeshRenderer))
                        {
                            var serializedRenderer = new SerializedObject(previewMeshRenderer);
                            SerializedProperty materialsProperty = serializedRenderer.FindProperty("m_Materials");
                            if (materialsProperty != null)
                                PrefabUtility.RevertPropertyOverride(materialsProperty, InteractionMode.AutomatedAction);
                        }

                        previewExists = false;
                    }
            }
            
            // --------------------------------------------------------------------------------
            // Used by the (Replace All Instances) paint tool: swap every
            // occurrence of the HOVERED slot's material with the selected one,
            // across the GameObjects in the same MAST Holder
            // --------------------------------------------------------------------------------
            public static void ReplaceAllHoveredInHolder(Material replacement)
            {
                if (!previewExists || replacement == null)
                    return;

                // The hovered slot currently shows the PREVIEW — its real
                // material is the backup.  Take the preview off first so the
                // sweep records the true original in the journal
                Renderer clickedRenderer = previewMeshRenderer;
                Material target = originalMaterial;
                ClearCurrentMaterialPaintPreview();

                int changedSlots = SceneMaterialReplacement.ReplaceAllUnderHolder(
                    clickedRenderer, target, replacement);

                // Only surface the FAILURE case — a successful replacement is
                // its own feedback
                if (changedSlots < 0 && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.ShowNotification(
                        new GUIContent("Not under a MAST Holder — nothing replaced"), 2.5);
            }

            // ---------------------------------------------------------------------------
            // Raycast to the mouse pointer to find and process the material hit
            // ---------------------------------------------------------------------------
            private static void ProcessMousePointerRaycast()
            {
                // Create a ray starting from the current point the mouse is.
                // The grid and visualizer are excluded by LAYER "the old name
                // compare made the grid block painting anything beneath it"
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                int paintableLayers = ~(1 << MAST.Const.Grid.gridLayer | 1 << MAST.Const.Placement.visualizerLayer);

                if (!Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, Mathf.Infinity, paintableLayers))
                {
                    // Nothing hit — remove the last material paint preview
                    ClearCurrentMaterialPaintPreview();
                    return;
                }

                // Face painting needs a triangle hit, which only a non-convex
                // MeshCollider provides "triangleIndex is -1 for box, sphere,
                // capsule, and convex colliders — indexing with it used to throw"
                var meshCollider = hit.collider as MeshCollider;
                GameObject hitGameObject = hit.collider.gameObject;
                Mesh renderMesh = GetMesh(hitGameObject);

                if (renderMesh == null)
                {
                    ClearCurrentMaterialPaintPreview();
                    return;
                }

                int subMeshIndex;

                if (renderMesh.subMeshCount == 1)
                {
                    // Single-submesh meshes don't need triangle mapping at all —
                    // this also makes box/sphere-collider kit pieces paintable
                    subMeshIndex = 0;
                }
                else
                {
                    // Triangle indices belong to the COLLIDER's mesh; mapping
                    // them onto a different render mesh would pick wrong faces
                    if (meshCollider == null || hit.triangleIndex < 0 || meshCollider.sharedMesh != renderMesh)
                    {
                        ClearCurrentMaterialPaintPreview();
                        return;
                    }

                    subMeshIndex = FindSubMeshIndex(renderMesh, hit.triangleIndex);
                    if (subMeshIndex < 0)
                    {
                        ClearCurrentMaterialPaintPreview();
                        return;
                    }
                }

                // A mesh can have more submeshes than the renderer has slots
                MeshRenderer hitRenderer = hitGameObject.GetComponent<MeshRenderer>();
                if (hitRenderer == null || subMeshIndex >= hitRenderer.sharedMaterials.Length)
                {
                    ClearCurrentMaterialPaintPreview();
                    return;
                }

                // If a new slot or gameobject is hovered, or no preview exists
                if (previewSharedMaterialIndex != subMeshIndex ||
                    previewGameObject != hitGameObject ||
                    !previewExists)
                {
                    // Clear the current material paint preview, so it's not permanently applied
                    ClearCurrentMaterialPaintPreview();

                    // Prep the new material paint preview
                    PrepNextPaintPreview(hitGameObject, subMeshIndex);
                }
            }

            // Which submesh owns this triangle?  Descriptor ranges make this
            // O(subMeshCount) with zero allocation "the old version copied the
            // whole index buffer several times per SceneView event"
            private static int FindSubMeshIndex(Mesh mesh, int triangleIndex)
            {
                int firstIndex = triangleIndex * 3;

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    UnityEngine.Rendering.SubMeshDescriptor subMesh = mesh.GetSubMesh(i);
                    if (firstIndex >= subMesh.indexStart && firstIndex < subMesh.indexStart + subMesh.indexCount)
                        return i;
                }

                return -1;
            }

            // Set the next gameobject and shared material index to apply the current material to
            private static void PrepNextPaintPreview(GameObject gameObject, int sharedMaterialIndex)
            {
                // Save this gameobject, meshrenderer, and sharedmaterial index.
                // No same-slot early-out here: after painting, re-preparing the
                // same slot is exactly how it becomes paintable again "the old
                // guard made a freshly painted face dead until the pointer left
                // it", and the painted material correctly becomes the new
                // original to restore to.
                previewGameObject = gameObject;
                previewMeshRenderer = previewGameObject.GetComponent<MeshRenderer>();
                previewSharedMaterialIndex = sharedMaterialIndex;

                // Backup current material, and whether this renderer already
                // carried a materials override on its prefab instance
                originalMaterial = previewMeshRenderer.sharedMaterials[previewSharedMaterialIndex];
                previewHadMaterialsOverride = HasMaterialsOverride(previewMeshRenderer);

                previewExists = true;
            }

            // Does this prefab-instance renderer already have any m_Materials
            // override of its own?  Non-instances report true "nothing to revert"
            private static bool HasMaterialsOverride(MeshRenderer renderer)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(renderer))
                    return true;

                Object sourceRenderer = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                PropertyModification[] modifications =
                    PrefabUtility.GetPropertyModifications(renderer);

                if (modifications == null)
                    return false;

                foreach (PropertyModification modification in modifications)
                {
                    if (modification.target == sourceRenderer &&
                        modification.propertyPath.StartsWith("m_Materials"))
                        return true;
                }

                return false;
            }
            
            // Safely get mesh from the selected GameObject
            private static Mesh GetMesh(GameObject testGameObject)
            {
                // If gameobject exists
                if (testGameObject != null)
                {
                    // Get meshfilter for gameobject
                    MeshFilter meshFilter = testGameObject.GetComponent<MeshFilter>();
                    
                    // If meshfilter exists
                    if (meshFilter != null)
                    {
                        // Get sharedmesh "never the .mesh getter: in edit mode it
                        // instantiates a leaked copy of the shared asset"
                        Mesh mesh = meshFilter.sharedMesh;

                        // If mesh exists
                        if (mesh != null)
                        {
                            // Return with mesh
                            return mesh;
                        }
                    }
                }
                
                // Return null
                return (Mesh)null;
            }
        }
    }
}
