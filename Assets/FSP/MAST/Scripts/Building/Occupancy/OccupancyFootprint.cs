using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Building
    {
        // Computes which grid cells a model's geometry occupies "its footprint",
        // borrowed from the DoaB scene builder's ModelOccupancyCalculator.
        //
        // Every triangle of the model is tested against each candidate cell's box
        // with a Separating Axis Theorem test.  The cell box is shrunk by the
        // occupancy margin on every side, so geometry must reach that fraction
        // into a cell before the cell counts as occupied — a wall that merely
        // touches a neighboring cell's boundary doesn't claim it.
        //
        // Cell conventions match MAST placement: pivots sit at the XZ center of
        // their cell (offset 0 spans ±half a cell around the pivot) and at the
        // BOTTOM of their Y cell (offset 0 spans pivot to pivot + one Y unit).
        //
        // Results are cached per (prefab, rotation, scale), so a kit piece placed
        // a hundred times at four rotations costs four calculations.  Rotation is
        // baked into the calculation itself rather than transformed with integer
        // cell math, so odd angles are just as accurate as 90° steps.
        public static class OccupancyFootprint
        {
            // Keyed by source asset, then by quantized rotation + scale "Object
            // keys compare by reference, so no deprecated instance-ID calls"
            private static readonly Dictionary<Object, Dictionary<string, List<Vector3Int>>> cache =
                new Dictionary<Object, Dictionary<string, List<Vector3Int>>>();

            // The settings the cached footprints were computed with — any change
            // to these makes every cached footprint wrong
            private static float cachedMargin = -1f;
            private static float cachedXZUnitSize = -1f;
            private static float cachedYUnitSize = -1f;

            // ---------------------------------------------------------------------------
            // Cached footprint lookup
            // ---------------------------------------------------------------------------

            // Get the footprint of a scene instance at its current rotation and
            // scale.  cacheIdentity names the asset the instance came from; when
            // null, the instance's own prefab source is used, and instances with
            // no prefab source are computed uncached
            public static List<Vector3Int> GetCells(GameObject instance, Object cacheIdentity = null)
            {
                InvalidateCacheIfSettingsChanged();

                float xzUnitSize = Settings.Data.gui.grid.xzUnitSize;
                float yUnitSize = Settings.Data.gui.grid.yUnitSize;
                float margin = Settings.Data.placement.occupancyMargin;

                Object identity = cacheIdentity;
                if (identity == null)
                    identity = PrefabUtility.GetCorrespondingObjectFromSource(instance);

                // No stable identity to cache under — compute directly
                if (identity == null)
                    return CalculateCells(instance, xzUnitSize, yUnitSize, margin,
                        GridSpace.Rotation, GridSpace.Scale);

                if (!cache.TryGetValue(identity, out Dictionary<string, List<Vector3Int>> poses))
                {
                    poses = new Dictionary<string, List<Vector3Int>>();
                    cache[identity] = poses;
                }

                // A footprint depends only on the pose RELATIVE to the grid, so
                // keying on the relative rotation keeps the cache valid however
                // the holder itself is rotated
                Quaternion gridRelativeRotation =
                    Quaternion.Inverse(GridSpace.Rotation) * instance.transform.rotation;
                string pose = BuildPoseKey(gridRelativeRotation, instance.transform.lossyScale);

                if (poses.TryGetValue(pose, out List<Vector3Int> cells))
                    return cells;

                cells = CalculateCells(instance, xzUnitSize, yUnitSize, margin,
                    GridSpace.Rotation, GridSpace.Scale);
                poses[pose] = cells;
                return cells;
            }

            // Pose key from quantized rotation and scale "quantizing keeps float
            // noise from creating near-duplicate entries"
            private static string BuildPoseKey(Quaternion rotation, Vector3 scale)
            {
                Vector3 euler = rotation.eulerAngles;
                return Mathf.RoundToInt(euler.x * 10f) + "," +
                    Mathf.RoundToInt(euler.y * 10f) + "," +
                    Mathf.RoundToInt(euler.z * 10f) + "|" +
                    Mathf.RoundToInt(scale.x * 1000f) + "," +
                    Mathf.RoundToInt(scale.y * 1000f) + "," +
                    Mathf.RoundToInt(scale.z * 1000f);
            }

            // Drop all cached footprints when the grid unit sizes or the
            // occupancy margin change
            private static void InvalidateCacheIfSettingsChanged()
            {
                float margin = Settings.Data.placement.occupancyMargin;
                float xzUnitSize = Settings.Data.gui.grid.xzUnitSize;
                float yUnitSize = Settings.Data.gui.grid.yUnitSize;

                if (margin == cachedMargin && xzUnitSize == cachedXZUnitSize && yUnitSize == cachedYUnitSize)
                    return;

                cache.Clear();
                cachedMargin = margin;
                cachedXZUnitSize = xzUnitSize;
                cachedYUnitSize = yUnitSize;
                OccupancyMap.MarkDirty();
            }

            // ---------------------------------------------------------------------------
            // Footprint calculation
            // ---------------------------------------------------------------------------

            // Calculate the cells occupied by all meshes under the instance, as
            // offsets from the instance's pivot cell.  Pure math on explicit
            // parameters so tests can run it without MAST settings loaded
            public static List<Vector3Int> CalculateCells(
                GameObject instance, float xzUnitSize, float yUnitSize, float margin)
            {
                return CalculateCells(instance, xzUnitSize, yUnitSize, margin, Quaternion.identity, Vector3.one);
            }

            public static List<Vector3Int> CalculateCells(
                GameObject instance, float xzUnitSize, float yUnitSize, float margin, Quaternion gridRotation)
            {
                return CalculateCells(instance, xzUnitSize, yUnitSize, margin, gridRotation, Vector3.one);
            }

            // Grid-frame-aware overload: cells are measured along the GRID'S
            // axes "the holder's local frame", with the holder's scale acting
            // as a multiplier on the cell unit sizes
            public static List<Vector3Int> CalculateCells(
                GameObject instance, float xzUnitSize, float yUnitSize, float margin,
                Quaternion gridRotation, Vector3 gridScale)
            {
                var occupiedCells = new HashSet<Vector3Int>();
                Vector3 pivot = instance.transform.position;
                Quaternion inverseGridRotation = Quaternion.Inverse(gridRotation);

                // World size of one cell per axis "unit size times holder scale"
                var cellWorldSize = new Vector3(
                    xzUnitSize * gridScale.x,
                    yUnitSize * gridScale.y,
                    xzUnitSize * gridScale.z);

                foreach (MeshFilter meshFilter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    if (meshFilter.sharedMesh == null)
                        continue;
                    ProcessMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix,
                        pivot, inverseGridRotation, cellWorldSize, margin, occupiedCells);
                }

                foreach (SkinnedMeshRenderer skinnedMesh in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (skinnedMesh.sharedMesh == null)
                        continue;
                    ProcessMesh(skinnedMesh.sharedMesh, skinnedMesh.transform.localToWorldMatrix,
                        pivot, inverseGridRotation, cellWorldSize, margin, occupiedCells);
                }

                // A model with no mesh "or one too thin to claim any cell" still
                // occupies the cell it stands in
                if (occupiedCells.Count == 0)
                    occupiedCells.Add(Vector3Int.zero);

                return new List<Vector3Int>(occupiedCells);
            }

            // Test one mesh's triangles against candidate cells.  Vertices are
            // converted to "cell space" — pivot-relative, rotated into the grid's
            // axes, and divided by the WORLD cell size per axis — so every cell
            // is a unit cube and the SAT math stays simple even when the XZ and
            // Y unit sizes differ or the grid is rotated or scaled
            private static void ProcessMesh(Mesh mesh, Matrix4x4 localToWorld, Vector3 pivot,
                Quaternion inverseGridRotation,
                Vector3 cellWorldSize, float margin, HashSet<Vector3Int> occupiedCells)
            {
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;

                if (vertices.Length == 0)
                    return;

                var cellSpaceVertices = new Vector3[vertices.Length];
                Vector3 min = Vector3.positiveInfinity;
                Vector3 max = Vector3.negativeInfinity;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertices[i]);
                    Vector3 gridVertex = inverseGridRotation * (worldVertex - pivot);
                    cellSpaceVertices[i] = new Vector3(
                        gridVertex.x / cellWorldSize.x,
                        gridVertex.y / cellWorldSize.y,
                        gridVertex.z / cellWorldSize.z);
                    min = Vector3.Min(min, cellSpaceVertices[i]);
                    max = Vector3.Max(max, cellSpaceVertices[i]);
                }

                // Candidate cell range: X and Z are pivot-centered "cell 0 spans
                // -0.5 to +0.5", Y is floor-based "cell 0 spans 0 to 1"
                int minX = Mathf.FloorToInt(min.x + 0.5f);
                int maxX = Mathf.CeilToInt(max.x - 0.5f);
                int minY = Mathf.FloorToInt(min.y);
                int maxY = Mathf.CeilToInt(max.y) - 1;
                int minZ = Mathf.FloorToInt(min.z + 0.5f);
                int maxZ = Mathf.CeilToInt(max.z - 0.5f);

                // The surface test alone misses cells buried inside a solid piece
                // — a large box's core, or a layer whose faces all lie exactly on
                // cell boundaries where the claim margin shaves them off.  Cells
                // whose center is INSIDE the volume are therefore also claimed.
                // Closed meshes use an exact single-ray parity test; open meshes
                // "kit pieces with hidden faces removed" use a multi-ray majority
                // vote, which sees through the missing faces without phantom-
                // filling real voids like archways
                bool meshIsClosed = IsMeshClosed(cellSpaceVertices, triangles);

                for (int cellX = minX; cellX <= maxX; cellX++)
                    for (int cellY = minY; cellY <= maxY; cellY++)
                        for (int cellZ = minZ; cellZ <= maxZ; cellZ++)
                        {
                            // Cell centers sit at integers in X/Z and integer + 0.5 in Y
                            var cellCenter = new Vector3(cellX, cellY + 0.5f, cellZ);
                            if (MeshIntersectsCell(cellSpaceVertices, triangles, cellCenter, margin) ||
                                CellCenterInsideVolume(cellSpaceVertices, triangles, cellCenter, meshIsClosed))
                                occupiedCells.Add(new Vector3Int(cellX, cellY, cellZ));
                        }
            }

            // ---------------------------------------------------------------------------
            // Interior test "cells buried inside a closed volume"
            // ---------------------------------------------------------------------------

            // Fixed off-axis ray directions for the parity tests.  Grid-kit
            // meshes are axis-aligned, so an axis-aligned ray would constantly
            // graze faces and edges; unrelated components make that vanishingly
            // rare.  Several directions "and their opposites" for the vote
            private static readonly Vector3[] parityRayDirections =
            {
                new Vector3( 0.5416f,  0.4652f,  0.6997f),
                new Vector3(-0.5416f, -0.4652f, -0.6997f),
                new Vector3( 0.6997f,  0.5416f,  0.4652f),
                new Vector3(-0.6997f, -0.5416f, -0.4652f),
                new Vector3( 0.4652f,  0.6997f,  0.5416f),
                new Vector3(-0.4652f, -0.6997f, -0.5416f)
            };

            // Is the point inside the mesh volume?
            //
            // Closed meshes: one parity ray is exact "odd crossings = inside".
            //
            // Open meshes "hidden faces removed — the standard kit optimization":
            // one ray can be fooled by a hole, so all six rays vote and a strict
            // majority of odd counts wins.  From inside such a piece most rays
            // still cross exactly one wall; from a real void "under an archway"
            // rays cross zero or two faces, so voids stay unclaimed
            private static bool CellCenterInsideVolume(Vector3[] vertices, int[] triangles,
                Vector3 point, bool meshIsClosed)
            {
                if (meshIsClosed)
                    return (CountRayCrossings(vertices, triangles, point, parityRayDirections[0]) & 1) == 1;

                int oddVotes = 0;

                for (int d = 0; d < parityRayDirections.Length; d++)
                {
                    if ((CountRayCrossings(vertices, triangles, point, parityRayDirections[d]) & 1) == 1)
                        oddVotes++;
                }

                return oddVotes >= 4;
            }

            private static int CountRayCrossings(Vector3[] vertices, int[] triangles, Vector3 point, Vector3 direction)
            {
                int crossings = 0;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (RayCrossesTriangle(point, direction,
                        vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]))
                        crossings++;
                }

                return crossings;
            }

            // Möller–Trumbore ray/triangle intersection, counting only crossings
            // strictly in front of the origin "a point lying on a face doesn't
            // cross its own triangle"
            private static bool RayCrossesTriangle(Vector3 origin, Vector3 direction,
                Vector3 v0, Vector3 v1, Vector3 v2)
            {
                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;

                Vector3 crossDirectionEdge2 = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, crossDirectionEdge2);

                // Ray parallel to the triangle plane
                if (Mathf.Abs(determinant) < 1e-9f)
                    return false;

                float inverseDeterminant = 1f / determinant;
                Vector3 originToV0 = origin - v0;

                float u = Vector3.Dot(originToV0, crossDirectionEdge2) * inverseDeterminant;
                if (u < 0f || u > 1f)
                    return false;

                Vector3 crossOriginEdge1 = Vector3.Cross(originToV0, edge1);
                float v = Vector3.Dot(direction, crossOriginEdge1) * inverseDeterminant;
                if (v < 0f || u + v > 1f)
                    return false;

                float distance = Vector3.Dot(edge2, crossOriginEdge1) * inverseDeterminant;
                return distance > 1e-6f;
            }

            // Is every edge shared by exactly two triangles?  Vertices are
            // welded by position first — hard edges and UV seams duplicate
            // vertices, which would otherwise make every kit piece look open
            private static bool IsMeshClosed(Vector3[] vertices, int[] triangles)
            {
                // Canonical vertex ids by quantized position
                var canonicalByPosition = new Dictionary<Vector3Int, int>();
                var canonical = new int[vertices.Length];

                for (int i = 0; i < vertices.Length; i++)
                {
                    var quantized = new Vector3Int(
                        Mathf.RoundToInt(vertices[i].x * 10000f),
                        Mathf.RoundToInt(vertices[i].y * 10000f),
                        Mathf.RoundToInt(vertices[i].z * 10000f));

                    if (!canonicalByPosition.TryGetValue(quantized, out int id))
                    {
                        id = canonicalByPosition.Count;
                        canonicalByPosition[quantized] = id;
                    }
                    canonical[i] = id;
                }

                // Count triangle uses of every undirected edge
                var edgeUseCounts = new Dictionary<long, int>();

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = canonical[triangles[i]];
                    int b = canonical[triangles[i + 1]];
                    int c = canonical[triangles[i + 2]];

                    // Degenerate triangles "zero-length edges" break manifold
                    // accounting — treat the mesh as open
                    if (a == b || b == c || c == a)
                        return false;

                    CountEdge(edgeUseCounts, a, b);
                    CountEdge(edgeUseCounts, b, c);
                    CountEdge(edgeUseCounts, c, a);
                }

                foreach (int useCount in edgeUseCounts.Values)
                    if (useCount != 2)
                        return false;

                return edgeUseCounts.Count > 0;
            }

            private static void CountEdge(Dictionary<long, int> edgeUseCounts, int a, int b)
            {
                long key = a < b
                    ? ((long)a << 32) | (uint)b
                    : ((long)b << 32) | (uint)a;

                edgeUseCounts.TryGetValue(key, out int useCount);
                edgeUseCounts[key] = useCount + 1;
            }

            // Does any triangle reach into the cell's margin-shrunk box?
            private static bool MeshIntersectsCell(Vector3[] vertices, int[] triangles,
                Vector3 cellCenter, float margin)
            {
                float halfSize = 0.5f - margin;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (TriangleIntersectsBox(
                        vertices[triangles[i]] - cellCenter,
                        vertices[triangles[i + 1]] - cellCenter,
                        vertices[triangles[i + 2]] - cellCenter,
                        halfSize))
                        return true;
                }

                return false;
            }

            // Separating Axis Theorem test between a triangle "already translated
            // to the box center" and an origin-centered cube
            private static bool TriangleIntersectsBox(Vector3 v0, Vector3 v1, Vector3 v2, float boxHalfSize)
            {
                Vector3 edge0 = v1 - v0;
                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v0 - v2;

                // 9 axes from cross products of triangle edges with the box axes
                if (EdgeAxesSeparate(Vector3.right, edge0, edge1, edge2, v0, v1, v2, boxHalfSize)) return false;
                if (EdgeAxesSeparate(Vector3.up, edge0, edge1, edge2, v0, v1, v2, boxHalfSize)) return false;
                if (EdgeAxesSeparate(Vector3.forward, edge0, edge1, edge2, v0, v1, v2, boxHalfSize)) return false;

                // 3 box face normals
                if (IsSeparatingAxis(Vector3.right, v0, v1, v2, boxHalfSize)) return false;
                if (IsSeparatingAxis(Vector3.up, v0, v1, v2, boxHalfSize)) return false;
                if (IsSeparatingAxis(Vector3.forward, v0, v1, v2, boxHalfSize)) return false;

                // Triangle face normal
                Vector3 triangleNormal = Vector3.Cross(edge0, edge1);
                if (triangleNormal.sqrMagnitude > 0.0001f)
                    if (IsSeparatingAxis(triangleNormal, v0, v1, v2, boxHalfSize))
                        return false;

                return true;
            }

            // Test the three cross-product axes one box axis contributes
            private static bool EdgeAxesSeparate(Vector3 boxAxis,
                Vector3 edge0, Vector3 edge1, Vector3 edge2,
                Vector3 v0, Vector3 v1, Vector3 v2, float boxHalfSize)
            {
                Vector3 axis = Vector3.Cross(boxAxis, edge0);
                if (axis.sqrMagnitude >= 0.0001f && IsSeparatingAxis(axis, v0, v1, v2, boxHalfSize))
                    return true;

                axis = Vector3.Cross(boxAxis, edge1);
                if (axis.sqrMagnitude >= 0.0001f && IsSeparatingAxis(axis, v0, v1, v2, boxHalfSize))
                    return true;

                axis = Vector3.Cross(boxAxis, edge2);
                if (axis.sqrMagnitude >= 0.0001f && IsSeparatingAxis(axis, v0, v1, v2, boxHalfSize))
                    return true;

                return false;
            }

            // Does this axis separate the triangle from the box?
            private static bool IsSeparatingAxis(Vector3 axis, Vector3 v0, Vector3 v1, Vector3 v2, float boxHalfSize)
            {
                float projection0 = Vector3.Dot(axis, v0);
                float projection1 = Vector3.Dot(axis, v1);
                float projection2 = Vector3.Dot(axis, v2);

                float triangleMin = Mathf.Min(projection0, Mathf.Min(projection1, projection2));
                float triangleMax = Mathf.Max(projection0, Mathf.Max(projection1, projection2));

                // Project the origin-centered cube onto the axis
                float boxRadius = boxHalfSize *
                    (Mathf.Abs(axis.x) + Mathf.Abs(axis.y) + Mathf.Abs(axis.z));

                return triangleMax < -boxRadius || triangleMin > boxRadius;
            }
        }
    }
}
