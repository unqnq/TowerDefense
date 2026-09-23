using UnityEngine;


namespace MAST
{
    namespace Building
    {
        // Grid space IS the MAST Holder's local frame: the grid is a plane of
        // the holder's local space, so moving, rotating, or scaling the holder
        // carries the grid — and every placement calculation — along with it.
        // All snapping, occupancy, and area math happens in grid coordinates
        // "plain grid units" and converts back to world at the edges.
        //
        // The grid unit sizes from Settings stay authoritative: the holder's
        // scale acts as a multiplier on top of them, so a holder scaled 2x
        // gives 2x-sized cells in the world while every grid-space number is
        // unchanged.  With no holder, grid space is world space — the old
        // behavior.
        public static class GridSpace
        {
            private static Transform Holder =>
                Placement.targetParent != null ? Placement.targetParent.transform : null;

            public static Vector3 Origin => Holder != null ? Holder.position : Vector3.zero;

            public static Quaternion Rotation => Holder != null ? Holder.rotation : Quaternion.identity;

            // Rotation frame for PIECE orientation.  Positions always follow
            // the holder, but pieces only rotate with it when the setting asks
            // — otherwise they keep world rotation "props stay upright on a
            // tilted holder" while still snapping to the rotated grid's cells
            public static Quaternion PlacementRotation
            {
                get
                {
                    if (Settings.Data.placement != null && Settings.Data.placement.rotation.rotateWithHolder)
                        return Rotation;

                    return Quaternion.identity;
                }
            }

            // Holder scale, per axis in the holder's own frame.  A zero
            // component would make grid space unrecoverable "division by
            // zero everywhere", so it degrades to 1
            public static Vector3 Scale
            {
                get
                {
                    if (Holder == null)
                        return Vector3.one;

                    Vector3 scale = Holder.lossyScale;
                    return new Vector3(SafeComponent(scale.x), SafeComponent(scale.y), SafeComponent(scale.z));
                }
            }

            private static float SafeComponent(float component)
            {
                return Mathf.Abs(component) < 1e-6f ? 1f : component;
            }

            public static Vector3 WorldToGrid(Vector3 worldPoint)
            {
                return InverseScaled(Quaternion.Inverse(Rotation) * (worldPoint - Origin));
            }

            public static Vector3 GridToWorld(Vector3 gridPoint)
            {
                return Origin + Rotation * Vector3.Scale(gridPoint, Scale);
            }

            public static Vector3 WorldToGridDirection(Vector3 worldDirection)
            {
                return InverseScaled(Quaternion.Inverse(Rotation) * worldDirection);
            }

            public static Vector3 GridToWorldDirection(Vector3 gridDirection)
            {
                return Rotation * Vector3.Scale(gridDirection, Scale);
            }

            // NOTE: Ray's constructor normalizes the direction, so distances
            // along a grid ray are in grid metric — convert hit POINTS back to
            // world before comparing with world distances
            public static Ray WorldToGridRay(Ray worldRay)
            {
                return new Ray(WorldToGrid(worldRay.origin), WorldToGridDirection(worldRay.direction));
            }

            private static Vector3 InverseScaled(Vector3 vector)
            {
                Vector3 scale = Scale;
                return new Vector3(vector.x / scale.x, vector.y / scale.y, vector.z / scale.z);
            }
        }
    }
}
