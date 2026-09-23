using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Building
    {
        // Greeble tool: hit-face oriented placement.  Raycast the pointer onto
        // placed models (math only — no colliders, same philosophy as the
        // occupancy system), grow the hit triangle into a logical FACE by
        // flood-filling conjoined coplanar triangles, then snap the placement
        // to an N x N lattice of points spaced evenly between the face edges
        // "1 division = center, 2 = 33/66, 3 = 25/50/75".  The placed prefab's
        // outward axis (Settings > Placement > Greeble) aligns with the face
        // normal; the rotate hotkey spins it around that normal.
        //
        // Greeble placements ignore occupancy entirely — they attach to faces,
        // not cells (per Keith: "we couldn't do this with occupancy detection
        // active"), and each placed piece is marked allowOverlap so it neither
        // blocks nor catches face targeting afterward.
        public static class GreebleTool
        {
            // ---------------------------------------------------------------------------
            #region Target state (recomputed per pointer event)
            // ---------------------------------------------------------------------------

            private static bool hasTarget = false;

            // World-space placement frame of the targeted face
            private static Vector3 snapPosition;
            private static Vector3 faceNormal;
            private static Vector3 faceTangent;

            // Visual feedback: face boundary + the snap lattice
            private static readonly List<(Vector3 a, Vector3 b)> faceOutline =
                new List<(Vector3, Vector3)>();
            private static readonly List<Vector3> latticePoints = new List<Vector3>();

            // Spin around the face normal, driven by the rotate hotkey/button
            private static float spinDegrees = 0f;

            public static bool HasTarget => hasTarget;

            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Visualizer update + placement
            // ---------------------------------------------------------------------------

            // Aim the visualizer at the snapped point on the face under the
            // pointer.  Reuses Visualizer.visualizerOnGrid as the placement
            // gate so PlacePrefabInScene works unchanged
            public static void UpdateVisualizer()
            {
                Ray pointerRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                hasTarget = TryTargetFace(pointerRay);

                Visualizer.visualizerOnGrid = hasTarget;

                GameObject visualizer = Visualizer.GetGameObject();
                if (visualizer == null)
                    return;

                if (hasTarget)
                {
                    Quaternion rotation = ComputeRotation();
                    visualizer.transform.rotation = rotation;

                    // The prefab offset rides the greeble frame "its Y pushes
                    // off the surface no matter which way the face points"
                    visualizer.transform.position =
                        snapPosition + rotation * PrefabProfile.GetOffsetPosition();

                    DrawFaceHandles();
                }

                if (Visualizer.pointerInSceneview)
                    visualizer.SetActive(hasTarget);
            }

            // Spin the piece around the targeted face's normal "the rotate
            // hotkey and toolbar button route here while the tool is active"
            public static void Spin(float degrees)
            {
                spinDegrees = Mathf.Repeat(spinDegrees + degrees, 360f);
            }

            // Greebles are occupancy-exempt in BOTH directions: placement
            // ignored the map, and the placed piece must never block cells or
            // catch face targeting.  Marked with a TAG, not a component —
            // MASTPrefabSettings lives in MAST's editor-only assembly, and
            // Unity refuses AddComponent of editor-assembly scripts on scene
            // objects (returned null and threw every placement).  The tag
            // persists with the scene and touches neither prefab nor instance
            public static void MarkAsGreeble(GameObject placedInstance)
            {
                TagsAndLayers.AddTag(Const.Placement.greebleTag);
                placedInstance.tag = Const.Placement.greebleTag;
            }

            // Outward axis and spin composed into a world rotation: the
            // prefab's outward axis maps to the face normal, its reference
            // axis to the face tangent, then the user's spin turns it in place
            private static Quaternion ComputeRotation()
            {
                Vector3 outwardLocal = OutwardAxisVector(
                    Settings.Data.placement.greeble.outwardAxis);

                // Any axis orthogonal to outward works as the twist reference
                Vector3 referenceLocal = (outwardLocal.y != 0f) ? Vector3.forward : Vector3.up;

                Quaternion localFrame = Quaternion.LookRotation(referenceLocal, outwardLocal);
                Quaternion worldFrame = Quaternion.LookRotation(faceTangent, faceNormal);

                return Quaternion.AngleAxis(spinDegrees, faceNormal) *
                    worldFrame * Quaternion.Inverse(localFrame);
            }

            private static Vector3 OutwardAxisVector(GreebleAxis axis)
            {
                switch (axis)
                {
                    case GreebleAxis.YMinus: return Vector3.down;
                    case GreebleAxis.XPlus: return Vector3.right;
                    case GreebleAxis.XMinus: return Vector3.left;
                    case GreebleAxis.ZPlus: return Vector3.forward;
                    case GreebleAxis.ZMinus: return Vector3.back;
                    default: return Vector3.up;
                }
            }

            // Face boundary in cyan, snap lattice in green, chosen point in
            // yellow.  Handles.Draw* only render during Repaint, so calling
            // this on any event is safe
            private static void DrawFaceHandles()
            {
                Color previousColor = Handles.color;

                Handles.color = new Color(0f, 0.9f, 1f, 0.9f);
                foreach ((Vector3 a, Vector3 b) in faceOutline)
                    Handles.DrawLine(a, b);

                Handles.color = new Color(0.2f, 1f, 0.3f, 0.9f);
                foreach (Vector3 point in latticePoints)
                    Handles.DrawSolidDisc(point + faceNormal * 0.001f, faceNormal,
                        HandleUtility.GetHandleSize(point) * 0.03f);

                Handles.color = Color.yellow;
                Handles.DrawWireDisc(snapPosition + faceNormal * 0.001f, faceNormal,
                    HandleUtility.GetHandleSize(snapPosition) * 0.06f);

                Handles.color = previousColor;
            }

            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Face targeting (math raycast against MAST Holder meshes)
            // ---------------------------------------------------------------------------

            private static bool TryTargetFace(Ray worldRay)
            {
                faceOutline.Clear();
                latticePoints.Clear();

                // Find the holder without creating one "same rule as the
                // occupancy map — querying must never modify the scene"
                GameObject holder = Placement.targetParent;
                if (holder == null)
                {
                    try { holder = GameObject.FindGameObjectWithTag(Const.Placement.defaultTargetParentTag); }
                    catch { return false; }
                }
                if (holder == null)
                    return false;

                MeshFilter hitFilter = null;
                int hitTriangle = -1;
                Vector3 hitLocal = Vector3.zero;
                float closestDistance = float.MaxValue;

                foreach (MeshFilter filter in holder.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null)
                        continue;

                    // Cheap world-bounds reject before per-triangle testing
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || !renderer.bounds.IntersectRay(worldRay))
                        continue;

                    MeshData data = GetMeshData(mesh);
                    Transform meshTransform = filter.transform;
                    Matrix4x4 worldToLocal = meshTransform.worldToLocalMatrix;

                    // Ray normalizes its direction, so hit distances are in
                    // local units — candidates compare by WORLD distance so
                    // differently scaled pieces compete fairly
                    var localRay = new Ray(
                        worldToLocal.MultiplyPoint3x4(worldRay.origin),
                        worldToLocal.MultiplyVector(worldRay.direction));

                    int triangleCount = data.triangles.Length / 3;
                    for (int tri = 0; tri < triangleCount; tri++)
                    {
                        if (!RayIntersectsTriangle(localRay, data, tri, out float localDistance))
                            continue;

                        Vector3 local = localRay.GetPoint(localDistance);
                        float worldDistance = Vector3.Distance(
                            worldRay.origin, meshTransform.TransformPoint(local));

                        if (worldDistance < closestDistance)
                        {
                            closestDistance = worldDistance;
                            hitFilter = filter;
                            hitTriangle = tri;
                            hitLocal = local;
                        }
                    }
                }

                if (hitFilter == null)
                    return false;

                return BuildFaceFrame(hitFilter, hitTriangle, hitLocal);
            }

            // Grow the hit triangle into the logical face, derive its 2D
            // frame, build the snap lattice, and pick the snapped point
            private static bool BuildFaceFrame(MeshFilter filter, int seedTriangle, Vector3 localHit)
            {
                MeshData data = GetMeshData(filter.sharedMesh);

                Vector3 seedNormal = data.triNormals[seedTriangle];
                if (seedNormal == Vector3.zero)
                    return false;

                Vector3 planePoint = data.vertices[data.triangles[seedTriangle * 3]];
                float planeEpsilon = filter.sharedMesh.bounds.size.magnitude * 1e-4f + 1e-6f;

                // Flood-fill conjoined triangles that share the seed's plane.
                // The plane-distance check keeps gently curved surfaces from
                // chaining into one giant "face" via small per-step angles
                var faceTriangles = new HashSet<int> { seedTriangle };
                var pending = new Queue<int>();
                pending.Enqueue(seedTriangle);

                while (pending.Count > 0)
                {
                    int tri = pending.Dequeue();

                    for (int edge = 0; edge < 3; edge++)
                    {
                        (int, int) key = WeldedEdgeKey(data,
                            data.triangles[tri * 3 + edge],
                            data.triangles[tri * 3 + (edge + 1) % 3]);

                        foreach (int neighbor in data.edgeTriangles[key])
                        {
                            if (faceTriangles.Contains(neighbor))
                                continue;
                            if (Vector3.Dot(data.triNormals[neighbor], seedNormal) < 0.9998f)
                                continue;
                            if (!TriangleOnPlane(data, neighbor, seedNormal, planePoint, planeEpsilon))
                                continue;

                            faceTriangles.Add(neighbor);
                            pending.Enqueue(neighbor);
                        }
                    }
                }

                // Boundary edges "used by exactly one triangle of the face" —
                // they draw the outline, and the longest one aims the U axis
                // so the lattice lines up with rectangular kit faces
                var edgeUse = new Dictionary<(int, int), (int count, int v0, int v1)>();
                foreach (int tri in faceTriangles)
                {
                    for (int edge = 0; edge < 3; edge++)
                    {
                        int i0 = data.triangles[tri * 3 + edge];
                        int i1 = data.triangles[tri * 3 + (edge + 1) % 3];
                        (int, int) key = WeldedEdgeKey(data, i0, i1);

                        edgeUse[key] = edgeUse.TryGetValue(key, out (int count, int v0, int v1) known)
                            ? (known.count + 1, known.v0, known.v1)
                            : (1, i0, i1);
                    }
                }

                Transform meshTransform = filter.transform;
                Vector3 uLocal = Vector3.zero;
                float longestEdge = 0f;

                foreach ((int count, int v0, int v1) entry in edgeUse.Values)
                {
                    if (entry.count != 1)
                        continue;

                    Vector3 a = data.vertices[entry.v0];
                    Vector3 b = data.vertices[entry.v1];
                    faceOutline.Add((meshTransform.TransformPoint(a), meshTransform.TransformPoint(b)));

                    float length = (b - a).sqrMagnitude;
                    if (length > longestEdge)
                    {
                        longestEdge = length;
                        uLocal = b - a;
                    }
                }

                if (uLocal == Vector3.zero)
                    uLocal = Vector3.Cross(seedNormal,
                        Mathf.Abs(seedNormal.y) < 0.9f ? Vector3.up : Vector3.right);

                // Orthonormal face frame in mesh-local space
                Vector3 vLocal = Vector3.Cross(seedNormal, uLocal).normalized;
                uLocal = Vector3.Cross(vLocal, seedNormal).normalized;

                // 2D bounds of the face in (U,V) around the plane point
                float minU = float.MaxValue, maxU = float.MinValue;
                float minV = float.MaxValue, maxV = float.MinValue;
                foreach (int tri in faceTriangles)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        Vector3 vertex = data.vertices[data.triangles[tri * 3 + corner]];
                        float u = Vector3.Dot(vertex - planePoint, uLocal);
                        float v = Vector3.Dot(vertex - planePoint, vLocal);
                        if (u < minU) minU = u;
                        if (u > maxU) maxU = u;
                        if (v < minV) minV = v;
                        if (v > maxV) maxV = v;
                    }
                }

                float hitU = Vector3.Dot(localHit - planePoint, uLocal);
                float hitV = Vector3.Dot(localHit - planePoint, vLocal);
                Vector3 hitOnPlane = planePoint + uLocal * hitU + vLocal * hitV;
                Vector3 snapLocal = hitOnPlane;
                var validLocal = new List<Vector3>();

                // The toolbar's grid-snap toggle governs the greeble lattice
                // too: snap OFF is free placement anywhere on the face, still
                // oriented to its normal, with no lattice drawn
                if (Settings.Data.placement.snapToGrid)
                {
                    // Snap lattice: N divisions = N points per axis at even
                    // fractions between the edges "i / (N + 1)".  Points that
                    // fall off the actual surface (L-shaped faces) are discarded
                    int divisions = Mathf.Max(1, Settings.Data.placement.greeble.divisions);
                    float insideEpsilon = ((maxU - minU) + (maxV - minV)) * 1e-4f;

                    for (int i = 1; i <= divisions; i++)
                    {
                        float u = Mathf.Lerp(minU, maxU, i / (divisions + 1f));
                        for (int j = 1; j <= divisions; j++)
                        {
                            float v = Mathf.Lerp(minV, maxV, j / (divisions + 1f));
                            if (PointOverFace(data, faceTriangles, u, v, uLocal, vLocal,
                                    planePoint, insideEpsilon))
                                validLocal.Add(planePoint + uLocal * u + vLocal * v);
                        }
                    }

                    // Snap the hit to the nearest on-surface lattice point; a
                    // face with no valid points "tiny or oddly shaped" places
                    // unsnapped.  Distances measure against the FIXED hit point
                    float closest = float.MaxValue;
                    foreach (Vector3 candidate in validLocal)
                    {
                        float distance = (candidate - hitOnPlane).sqrMagnitude;
                        if (distance < closest)
                        {
                            closest = distance;
                            snapLocal = candidate;
                        }
                    }
                }

                // Back to world space.  Normals use the inverse-transpose so
                // non-uniform scale "holder multipliers" can't skew them
                snapPosition = meshTransform.TransformPoint(snapLocal);
                faceNormal = meshTransform.worldToLocalMatrix.transpose
                    .MultiplyVector(seedNormal).normalized;
                faceTangent = meshTransform.localToWorldMatrix.MultiplyVector(uLocal);
                faceTangent = (faceTangent - faceNormal * Vector3.Dot(faceTangent, faceNormal)).normalized;
                if (faceTangent == Vector3.zero)
                    return false;

                foreach (Vector3 candidate in validLocal)
                    latticePoints.Add(meshTransform.TransformPoint(candidate));

                return true;
            }

            private static bool TriangleOnPlane(MeshData data, int tri,
                Vector3 planeNormal, Vector3 planePoint, float epsilon)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 vertex = data.vertices[data.triangles[tri * 3 + corner]];
                    if (Mathf.Abs(Vector3.Dot(vertex - planePoint, planeNormal)) > epsilon)
                        return false;
                }
                return true;
            }

            private static bool PointOverFace(MeshData data, HashSet<int> faceTriangles,
                float u, float v, Vector3 uLocal, Vector3 vLocal, Vector3 planePoint,
                float epsilon)
            {
                foreach (int tri in faceTriangles)
                {
                    Vector3 a = data.vertices[data.triangles[tri * 3]];
                    Vector3 b = data.vertices[data.triangles[tri * 3 + 1]];
                    Vector3 c = data.vertices[data.triangles[tri * 3 + 2]];

                    if (PointInTriangle2D(u, v,
                            Vector3.Dot(a - planePoint, uLocal), Vector3.Dot(a - planePoint, vLocal),
                            Vector3.Dot(b - planePoint, uLocal), Vector3.Dot(b - planePoint, vLocal),
                            Vector3.Dot(c - planePoint, uLocal), Vector3.Dot(c - planePoint, vLocal),
                            epsilon))
                        return true;
                }
                return false;
            }

            // Same-side sign test with a tolerance so lattice points exactly
            // on a shared triangle edge still count as inside
            private static bool PointInTriangle2D(float px, float py,
                float ax, float ay, float bx, float by, float cx, float cy, float epsilon)
            {
                float d1 = (px - ax) * (by - ay) - (py - ay) * (bx - ax);
                float d2 = (px - bx) * (cy - by) - (py - by) * (cx - bx);
                float d3 = (px - cx) * (ay - cy) - (py - cy) * (ax - cx);

                bool hasNegative = d1 < -epsilon || d2 < -epsilon || d3 < -epsilon;
                bool hasPositive = d1 > epsilon || d2 > epsilon || d3 > epsilon;

                return !(hasNegative && hasPositive);
            }

            // Möller–Trumbore with backface culling "greebles only target
            // faces looking at the camera".  det > 0 is front-facing for
            // Unity's clockwise winding
            private static bool RayIntersectsTriangle(Ray ray, MeshData data, int tri,
                out float distance)
            {
                distance = 0f;

                Vector3 v0 = data.vertices[data.triangles[tri * 3]];
                Vector3 v1 = data.vertices[data.triangles[tri * 3 + 1]];
                Vector3 v2 = data.vertices[data.triangles[tri * 3 + 2]];

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;

                Vector3 p = Vector3.Cross(ray.direction, edge2);
                float det = Vector3.Dot(edge1, p);
                if (det < 1e-8f)
                    return false;

                float invDet = 1f / det;
                Vector3 s = ray.origin - v0;
                float u = Vector3.Dot(s, p) * invDet;
                if (u < 0f || u > 1f)
                    return false;

                Vector3 q = Vector3.Cross(s, edge1);
                float v = Vector3.Dot(ray.direction, q) * invDet;
                if (v < 0f || u + v > 1f)
                    return false;

                float t = Vector3.Dot(edge2, q) * invDet;
                if (t <= 1e-6f)
                    return false;

                distance = t;
                return true;
            }

            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Per-mesh topology cache
            // ---------------------------------------------------------------------------

            private class MeshData
            {
                public Vector3[] vertices;
                public int[] triangles;
                public Vector3[] triNormals;
                public int[] weld;
                public Dictionary<(int, int), List<int>> edgeTriangles;
            }

            // Meshes are immutable assets during a session; the cache clears
            // itself on domain reload like the footprint cache
            private static readonly Dictionary<Mesh, MeshData> meshCache =
                new Dictionary<Mesh, MeshData>();

            private static MeshData GetMeshData(Mesh mesh)
            {
                if (meshCache.TryGetValue(mesh, out MeshData cached))
                    return cached;

                var data = new MeshData
                {
                    vertices = mesh.vertices,
                    triangles = mesh.triangles
                };

                // Position-weld so adjacency crosses the vertex splits hard
                // edges and UV seams create — without it every hard-edged
                // quad would be its own island
                data.weld = new int[data.vertices.Length];
                var canonical = new Dictionary<Vector3, int>();
                for (int i = 0; i < data.vertices.Length; i++)
                {
                    if (canonical.TryGetValue(data.vertices[i], out int index))
                        data.weld[i] = index;
                    else
                    {
                        canonical[data.vertices[i]] = i;
                        data.weld[i] = i;
                    }
                }

                int triangleCount = data.triangles.Length / 3;
                data.triNormals = new Vector3[triangleCount];
                data.edgeTriangles = new Dictionary<(int, int), List<int>>();

                for (int tri = 0; tri < triangleCount; tri++)
                {
                    Vector3 a = data.vertices[data.triangles[tri * 3]];
                    Vector3 b = data.vertices[data.triangles[tri * 3 + 1]];
                    Vector3 c = data.vertices[data.triangles[tri * 3 + 2]];
                    data.triNormals[tri] = Vector3.Cross(b - a, c - a).normalized;

                    for (int edge = 0; edge < 3; edge++)
                    {
                        (int, int) key = WeldedEdgeKey(data,
                            data.triangles[tri * 3 + edge],
                            data.triangles[tri * 3 + (edge + 1) % 3]);

                        if (!data.edgeTriangles.TryGetValue(key, out List<int> list))
                            data.edgeTriangles[key] = list = new List<int>();
                        list.Add(tri);
                    }
                }

                meshCache[mesh] = data;
                return data;
            }

            private static (int, int) WeldedEdgeKey(MeshData data, int i0, int i1)
            {
                int w0 = data.weld[i0];
                int w1 = data.weld[i1];
                return w0 < w1 ? (w0, w1) : (w1, w0);
            }

            #endregion
            // ---------------------------------------------------------------------------
        }
    }
}
