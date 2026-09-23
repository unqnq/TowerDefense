using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MAST.Building;

namespace MAST
{
    namespace Tests
    {
        // Edit-mode tests for the occupancy footprint math behind overlap
        // blocking and face-adjacent placement.  All tests use the pure
        // CalculateCells overload with explicit cell sizes and margin, so they
        // run without MAST settings loaded.
        public class OccupancyTests
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

            // A model root with one cube mesh child.  MAST convention: the pivot
            // sits at the XZ center and Y bottom of its cell, so a kit piece's
            // cube is lifted half its height above the root
            private GameObject NewModel(Vector3 cubeCenter, Vector3 cubeSize)
            {
                var root = new GameObject("root");
                createdObjects.Add(root);

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                createdObjects.Add(cube);
                cube.transform.parent = root.transform;
                cube.transform.localPosition = cubeCenter;
                cube.transform.localScale = cubeSize;

                return root;
            }

            [Test]
            public void UnitCube_OccupiesExactlyItsOwnCell()
            {
                GameObject model = NewModel(new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.9f, 0.9f));

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);

                CollectionAssert.AreEquivalent(new[] { Vector3Int.zero }, cells);
            }

            [Test]
            public void TwoUnitTallModel_OccupiesTwoStackedCells()
            {
                GameObject model = NewModel(new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1.9f, 0.9f));

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);

                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0) }, cells);
            }

            [Test]
            public void Margin_DecidesWhetherShallowPenetrationClaimsNeighborCells()
            {
                // Cube spanning x from -0.6 to +0.6 pokes 0.1 "10% of a cell"
                // into each X neighbor
                GameObject model = NewModel(new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.9f));

                List<Vector3Int> strict = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);
                CollectionAssert.AreEquivalent(new[] { Vector3Int.zero }, strict,
                    "a 10% poke must not claim neighbors at a 15% margin");

                List<Vector3Int> loose = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.05f);
                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(-1, 0, 0), Vector3Int.zero, new Vector3Int(1, 0, 0) }, loose,
                    "a 10% poke must claim neighbors at a 5% margin");
            }

            [Test]
            public void Rotation_IsBakedIntoTheFootprint()
            {
                // Two-cell-long piece along Z...
                GameObject model = NewModel(new Vector3(0f, 0.45f, 0.5f), new Vector3(0.9f, 0.9f, 1.9f));

                List<Vector3Int> unrotated = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);
                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1) }, unrotated);

                // ...turned 90° about Y lies along X instead
                model.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                List<Vector3Int> rotated = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);
                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) }, rotated);
            }

            [Test]
            public void NonCubicCells_UseTheYUnitSizeForVerticalCells()
            {
                // One unit tall, but with half-unit Y cells it spans two of them
                GameObject model = NewModel(new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.9f, 0.9f));

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 0.5f, 0.15f);

                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0) }, cells);
            }

            [Test]
            public void RotatedGrid_MeasuresFootprintsAlongGridAxes()
            {
                // A two-cell-long piece rotated together WITH the grid has an
                // identity pose relative to the grid — its footprint must be the
                // same as the unrotated piece on an unrotated grid
                GameObject model = NewModel(new Vector3(0f, 0.45f, 0.5f), new Vector3(0.9f, 0.9f, 1.9f));
                var gridRotation = Quaternion.Euler(0f, 90f, 0f);
                model.transform.rotation = gridRotation;

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f, gridRotation);

                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1) }, cells);
            }

            [Test]
            public void SolidBox_ClaimsCellsItFullyContains()
            {
                // A wide solid box whose faces all lie exactly on cell
                // boundaries: the claim margin shaves every face out of the
                // center cell's shrunk box, so only the interior volume test
                // can claim it — this was placing models fully inside big
                // solid pieces
                GameObject model = NewModel(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 1f));

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);

                CollectionAssert.Contains(cells, Vector3Int.zero,
                    "the fully-buried center cell must be claimed by the interior test");
                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(-1, 0, 0), Vector3Int.zero, new Vector3Int(1, 0, 0) }, cells,
                    "side walls bisect the neighbor columns; boundary faces claim nothing else");
            }

            // An OPEN box "hidden bottom face removed, the standard kit
            // optimization" spanning the given cell range, faces exactly on
            // cell boundaries — the worst case for the surface test
            private GameObject NewOpenBoxModel(float minZ, float maxZ)
            {
                var root = new GameObject("root");
                createdObjects.Add(root);

                var child = new GameObject("openBox");
                createdObjects.Add(child);
                child.transform.parent = root.transform;

                var mesh = new Mesh();
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, minZ), new Vector3(0.5f, 0f, minZ),
                    new Vector3(-0.5f, 1f, minZ), new Vector3(0.5f, 1f, minZ),
                    new Vector3(-0.5f, 0f, maxZ), new Vector3(0.5f, 0f, maxZ),
                    new Vector3(-0.5f, 1f, maxZ), new Vector3(0.5f, 1f, maxZ)
                };
                mesh.triangles = new[]
                {
                    2, 3, 7,  2, 7, 6,   // top
                    0, 1, 3,  0, 3, 2,   // -z end
                    4, 6, 7,  4, 7, 5,   // +z end
                    0, 2, 6,  0, 6, 4,   // -x side
                    1, 5, 7,  1, 7, 3    // +x side  (bottom is MISSING)
                };
                mesh.RecalculateNormals();
                createdObjects.Add(mesh);

                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                child.AddComponent<MeshRenderer>();
                return root;
            }

            [Test]
            public void OpenBox_BoundaryAligned_ClaimsItsCell()
            {
                // All faces sit ON cell boundaries "margin shaves the surface
                // test to nothing" and the missing bottom makes the mesh open —
                // only the multi-ray vote can claim the cell
                GameObject model = NewOpenBoxModel(-0.5f, 0.5f);

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);

                CollectionAssert.AreEquivalent(new[] { Vector3Int.zero }, cells);
            }

            [Test]
            public void OpenBox_TwoCells_ClaimsBothCells_AndFollowsRotation()
            {
                // The reported case: a two-cell piece with the pivot in one
                // cube, hidden faces removed.  Its footprint used to collapse
                // to the single fallback cell no matter the prefab size, so a
                // rotated second half slid inside other placed prefabs
                GameObject model = NewOpenBoxModel(-0.5f, 1.5f);

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);
                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 1) }, cells);

                // Rotated 180° the second half must relocate with it
                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                List<Vector3Int> rotated = OccupancyFootprint.CalculateCells(model, 1f, 1f, 0.15f);
                CollectionAssert.AreEquivalent(
                    new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 0, -1) }, rotated);
            }

            [Test]
            public void OpenMesh_NeverClaimsInteriorCells()
            {
                // A flat open quad has no inside — the interior test must not
                // fill phantom cells behind the plane
                var root = new GameObject("root");
                createdObjects.Add(root);

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                createdObjects.Add(quad);
                quad.transform.parent = root.transform;
                quad.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                quad.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(root, 1f, 1f, 0.15f);

                CollectionAssert.AreEquivalent(new[] { Vector3Int.zero }, cells);
            }

            [Test]
            public void MeshlessModel_FallsBackToItsPivotCell()
            {
                var empty = new GameObject("empty");
                createdObjects.Add(empty);

                List<Vector3Int> cells = OccupancyFootprint.CalculateCells(empty, 1f, 1f, 0.15f);

                CollectionAssert.AreEquivalent(new[] { Vector3Int.zero }, cells);
            }
        }
    }
}
