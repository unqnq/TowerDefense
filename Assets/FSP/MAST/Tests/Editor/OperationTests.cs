using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MAST.Tools;

namespace MAST
{
    namespace Tests
    {
        // Edit-mode tests for the scanning/counting logic behind the Merge Meshes
        // and Replace Materials operations.
        public class OperationTests
        {
            private readonly List<Object> createdObjects = new List<Object>();

            [TearDown]
            public void TearDown()
            {
                foreach (Object created in createdObjects)
                {
                    if (created != null)
                        Object.DestroyImmediate(created);
                }
                createdObjects.Clear();
            }

            private Material NewMaterial(string name)
            {
                var material = new Material(Shader.Find("Standard")) { name = name };
                createdObjects.Add(material);
                return material;
            }

            private GameObject NewRenderedChild(GameObject parent, params Material[] materials)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                createdObjects.Add(child);
                child.transform.parent = parent.transform;
                child.GetComponent<MeshRenderer>().sharedMaterials = materials;
                return child;
            }

            // ------------------------------------------------------------------
            // MergeMeshesOperation scanning
            // ------------------------------------------------------------------

            [Test]
            public void GetUniqueMaterials_ComparesIdentity_NotNames()
            {
                var root = new GameObject("root");
                createdObjects.Add(root);

                // Two DIFFERENT materials sharing one name "the MagicaVoxel case"
                Material first = NewMaterial("Root");
                Material second = NewMaterial("Root");
                NewRenderedChild(root, first);
                NewRenderedChild(root, second);

                List<Material> unique = MergeMeshesOperation.GetUniqueMaterials(root);

                Assert.AreEqual(2, unique.Count, "same-named but distinct materials must both be found");
            }

            [Test]
            public void GetUniqueMaterials_HonorsExcludeFromMerge()
            {
                var root = new GameObject("root");
                createdObjects.Add(root);

                Material included = NewMaterial("Included");
                Material excluded = NewMaterial("Excluded");

                NewRenderedChild(root, included);
                GameObject excludedChild = NewRenderedChild(root, excluded);
                excludedChild.AddComponent<MAST.Component.MASTPrefabSettings>().includeInMerge = false;

                List<Material> unique = MergeMeshesOperation.GetUniqueMaterials(root);

                Assert.AreEqual(1, unique.Count, "exclude-from-merge renderers must be skipped");
                Assert.AreSame(included, unique[0]);
            }

            // ------------------------------------------------------------------
            // ReplaceMaterialsOperation counting
            // ------------------------------------------------------------------

            [Test]
            public void ReplacementCount_IgnoresEmptyAndSelfReplacements()
            {
                var operation = new ReplaceMaterialsOperation();
                Material a = NewMaterial("A");
                Material b = NewMaterial("B");
                Material c = NewMaterial("C");

                operation.rules.Add(new ReplaceMaterialsOperation.MaterialRule { original = a, replacement = b });
                operation.rules.Add(new ReplaceMaterialsOperation.MaterialRule { original = b, replacement = null });
                operation.rules.Add(new ReplaceMaterialsOperation.MaterialRule { original = c, replacement = c });

                Assert.AreEqual(1, operation.ReplacementCount(),
                    "only a real, non-self replacement counts");
            }
        }
    }
}
