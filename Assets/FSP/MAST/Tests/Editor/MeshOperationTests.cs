using NUnit.Framework;
using UnityEngine;
using MAST.Tools;

namespace MAST
{
    namespace Tests
    {
        // Edit-mode tests for the pure mesh operations behind the Merge Meshes
        // window.  Run via Window > General > Test Runner (EditMode).
        public class MeshOperationTests
        {
            // ------------------------------------------------------------------
            // Test meshes
            // ------------------------------------------------------------------

            // A quad built as two triangles with fully duplicated vertices along
            // the shared diagonal "6 verts for 4 positions", matching normals/UVs
            private static Mesh QuadWithDuplicatedSeam()
            {
                var mesh = new Mesh();
                mesh.vertices = new[]
                {
                    new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                    new Vector3(0, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0)
                };
                mesh.normals = new[]
                {
                    Vector3.back, Vector3.back, Vector3.back,
                    Vector3.back, Vector3.back, Vector3.back
                };
                mesh.uv = new[]
                {
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0)
                };
                mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
                return mesh;
            }

            // Same layout, but the second triangle's seam vertices have a
            // different normal "a hard edge along the diagonal"
            private static Mesh QuadWithHardSeam()
            {
                Mesh mesh = QuadWithDuplicatedSeam();
                Vector3[] normals = mesh.normals;
                normals[3] = Vector3.up;
                normals[4] = Vector3.up;
                normals[5] = Vector3.up;
                mesh.normals = normals;
                return mesh;
            }

            // Same layout, but the second triangle's seam vertices sit on a
            // different UV island "a texture seam along the diagonal"
            private static Mesh QuadWithUVSeam()
            {
                Mesh mesh = QuadWithDuplicatedSeam();
                Vector2[] uv = mesh.uv;
                uv[3] = new Vector2(0.5f, 0.0f);
                uv[4] = new Vector2(1.0f, 0.5f);
                uv[5] = new Vector2(1.0f, 0.0f);
                mesh.uv = uv;
                return mesh;
            }

            // ------------------------------------------------------------------
            // WeldVertices
            // ------------------------------------------------------------------

            [Test]
            public void Weld_MergesIdenticalDuplicates()
            {
                Mesh mesh = MeshHelper.WeldVertices(QuadWithDuplicatedSeam(), 0.001f, compareNormals: true);

                Assert.AreEqual(4, mesh.vertexCount, "duplicated seam vertices should merge");
                Assert.AreEqual(6, mesh.triangles.Length, "triangle count must not change");
            }

            [Test]
            public void Weld_PreservesHardEdges_WhenComparingNormals()
            {
                Mesh mesh = MeshHelper.WeldVertices(QuadWithHardSeam(), 0.001f, compareNormals: true);

                Assert.AreEqual(6, mesh.vertexCount, "vertices with different normals must stay split");
            }

            [Test]
            public void Weld_MergesAcrossHardEdges_WhenIgnoringNormals()
            {
                Mesh mesh = MeshHelper.WeldVertices(QuadWithHardSeam(), 0.001f, compareNormals: false);

                Assert.AreEqual(4, mesh.vertexCount, "ignoring normals should merge across the hard edge");
            }

            [Test]
            public void Weld_NeverMergesAcrossUVSeams()
            {
                Mesh withNormals = MeshHelper.WeldVertices(QuadWithUVSeam(), 0.001f, compareNormals: true);
                Assert.AreEqual(6, withNormals.vertexCount, "UV seam vertices must stay split");

                Mesh ignoringNormals = MeshHelper.WeldVertices(QuadWithUVSeam(), 0.001f, compareNormals: false);
                Assert.AreEqual(6, ignoringNormals.vertexCount, "UV seams must survive even normal-ignoring welds");
            }

            [Test]
            public void Weld_PreservesSubmeshes()
            {
                Mesh mesh = QuadWithDuplicatedSeam();
                mesh.subMeshCount = 2;
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 3, 4, 5 }, 1);

                MeshHelper.WeldVertices(mesh, 0.001f, compareNormals: true);

                Assert.AreEqual(2, mesh.subMeshCount, "submesh count must survive welding");
                Assert.AreEqual(3, mesh.GetTriangles(0).Length);
                Assert.AreEqual(3, mesh.GetTriangles(1).Length);
            }

            // ------------------------------------------------------------------
            // HardenEdges / SoftenEdges
            // ------------------------------------------------------------------

            [Test]
            public void Harden_GivesEveryTriangleUniqueVertices()
            {
                Mesh mesh = MeshHelper.WeldVertices(QuadWithDuplicatedSeam(), 0.001f, compareNormals: true);
                Assert.AreEqual(4, mesh.vertexCount, "arrange: start from a welded quad");

                MeshHelper.HardenEdges(mesh);

                Assert.AreEqual(6, mesh.vertexCount, "each triangle should own its vertices");
                Assert.AreEqual(6, mesh.normals.Length, "normals should be recalculated per vertex");
            }

            [Test]
            public void Weld_AfterHarden_PreservesSplitNormals()
            {
                // Two triangles FOLDED along their shared edge "non-coplanar", so
                // hardening produces genuinely different face normals at the seam.
                // The merge pipeline's standalone weld "compareNormals: true" must
                // then leave the hardened seam split.
                var mesh = new Mesh();
                mesh.vertices = new[]
                {
                    new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                    new Vector3(0, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 1)
                };
                mesh.uv = new[]
                {
                    Vector2.zero, Vector2.up, Vector2.one,
                    Vector2.zero, Vector2.one, Vector2.right
                };
                mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };

                MeshHelper.HardenEdges(mesh);
                Assert.AreEqual(6, mesh.vertexCount, "arrange: hardened fold keeps 6 vertices");

                MeshHelper.WeldVertices(mesh, 0.001f, compareNormals: true);

                Assert.AreEqual(6, mesh.vertexCount,
                    "welding must not merge vertices whose normals differ");
            }

            [Test]
            public void Soften_MergesHardSeamAndAveragesNormals()
            {
                Mesh mesh = MeshHelper.SoftenEdges(QuadWithHardSeam(), 0.001f);

                Assert.AreEqual(4, mesh.vertexCount, "softening should weld across the hard seam");

                foreach (Vector3 normal in mesh.normals)
                    Assert.That(normal.magnitude, Is.EqualTo(1f).Within(0.001f), "normals must stay normalized");
            }
        }
    }
}
