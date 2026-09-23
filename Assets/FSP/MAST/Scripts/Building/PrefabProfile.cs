using UnityEngine;


namespace MAST
{
    namespace Building
    {
        // Effective placement values for the SELECTED palette prefab: each
        // accessor resolves per-prefab MASTPrefabSettings with the global
        // Placement settings as override/fallback.
        //
        // The component is looked up from the palette selection ON DEMAND (cached
        // per prefab).  The old design was a hidden `Helper.mastScript` global
        // that Interface assigned when the selection changed — read it at the
        // wrong time and every accessor silently described the WRONG prefab
        public static class PrefabProfile
        {
            private static GameObject cachedPrefab;
            private static Component.MASTPrefabSettings cachedSettings;

            private static Component.MASTPrefabSettings SelectedSettings
            {
                get
                {
                    GameObject selected = Palette.PrefabPalette.GetSelectedPrefab();
                    if (!ReferenceEquals(selected, cachedPrefab))
                    {
                        cachedPrefab = selected;
                        cachedSettings = selected != null
                            ? selected.GetComponent<Component.MASTPrefabSettings>()
                            : null;
                    }
                    return cachedSettings;
                }
            }

            private static MAST.DataClass.PlacementRaycast SelectedRaycast =>
                SelectedSettings != null ? SelectedSettings.placementRaycast : null;

            private static MAST.DataClass.Randomizer SelectedRandomizer =>
                SelectedSettings != null ? SelectedSettings.randomizer : null;

            // ---------------------------------------------------------------------------
            #region Basic placement values
            // ---------------------------------------------------------------------------

            public static Vector3 GetOffsetPosition()
            {
                if (Settings.Data.placement.overridePrefabOffset)
                    return Settings.Data.placement.offset.pos;

                Component.MASTPrefabSettings settings = SelectedSettings;
                return settings != null ? settings.offsetPosition : Settings.Data.placement.offset.pos;
            }

            public static Vector3 GetRotationStep()
            {
                if (Settings.Data.placement.overridePrefabRotation)
                    return Settings.Data.placement.rotation.step;

                Component.MASTPrefabSettings settings = SelectedSettings;
                return settings != null ? settings.rotationStep : Settings.Data.placement.rotation.step;
            }

            // Can prefab be placed inside others?  No MASTPrefabSettings means
            // solid "overlap is an opt-in; SHIFT still bypasses occupancy for
            // one-off overlaps"
            public static bool GetAllowOverlap()
            {
                Component.MASTPrefabSettings settings = SelectedSettings;
                return settings != null && settings.allowOverlap;
            }

            // Stretch the prefab when painting an area?
            public static bool GetPaintAreaStretch()
            {
                Component.MASTPrefabSettings settings = SelectedSettings;
                return settings == null || settings.paintAreaStretch;
            }

            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Placement Raycast
            // ---------------------------------------------------------------------------
            public static class PlacementRaycast
            {
                public static bool GetUseRaycast()
                {
                    if (Settings.Data.placement.overridePrefabRaycast)
                        return Settings.Data.placement.placementRaycast.useRaycast;

                    MAST.DataClass.PlacementRaycast raycast = SelectedRaycast;
                    return raycast != null ? raycast.useRaycast : Settings.Data.placement.placementRaycast.useRaycast;
                }

                public static DirectionVector GetDirection()
                {
                    MAST.DataClass.PlacementRaycast raycast = SelectedRaycast;
                    return raycast != null ? raycast.direction : Settings.Data.placement.placementRaycast.direction;
                }

                public static Vector3 GetStartOffset()
                {
                    MAST.DataClass.PlacementRaycast raycast = SelectedRaycast;
                    return raycast != null ? raycast.startOffset : Settings.Data.placement.placementRaycast.startOffset;
                }
            }
            #endregion
            // ---------------------------------------------------------------------------

            // ---------------------------------------------------------------------------
            #region Randomizer
            // ---------------------------------------------------------------------------
            public static class Randomizer
            {
                public static bool GetUseRandomizer()
                {
                    if (Settings.Data.placement.overridePrefabRandomizer)
                        return Settings.Data.placement.randomizer.useRandomizer;

                    MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                    return randomizer != null ? randomizer.useRandomizer : Settings.Data.placement.randomizer.useRandomizer;
                }

                public static class Replace
                {
                    public static bool GetReplaceable()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null && randomizer.allowReplacement;
                    }

                    public static int GetReplaceID()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.replacementID : 0;
                    }
                }

                public static class Rotation
                {
                    public static Vector3 GetStep()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.rotateStep : Settings.Data.placement.randomizer.rotateStep;
                    }
                    public static Vector3 GetMin()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.rotateMin : Settings.Data.placement.randomizer.rotateMin;
                    }
                    public static Vector3 GetMax()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.rotateMax : Settings.Data.placement.randomizer.rotateMax;
                    }
                }

                public static class Scale
                {
                    public static Vector3 GetMin()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.scaleMin : Settings.Data.placement.randomizer.scaleMin;
                    }
                    public static Vector3 GetMax()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.scaleMax : Settings.Data.placement.randomizer.scaleMax;
                    }
                    public static ScaleAxisLock GetLock()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? (ScaleAxisLock)(int)randomizer.scaleLock : Settings.Data.placement.randomizer.scaleLock;
                    }
                }

                public static class Position
                {
                    public static Vector3 GetMin()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.posMin : Settings.Data.placement.randomizer.posMin;
                    }
                    public static Vector3 GetMax()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.posMax : Settings.Data.placement.randomizer.posMax;
                    }
                }

                public static class Flip
                {
                    public static bool GetX()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.flipX : Settings.Data.placement.randomizer.flipX;
                    }
                    public static bool GetY()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.flipY : Settings.Data.placement.randomizer.flipY;
                    }
                    public static bool GetZ()
                    {
                        MAST.DataClass.Randomizer randomizer = SelectedRandomizer;
                        return randomizer != null ? randomizer.flipZ : Settings.Data.placement.randomizer.flipZ;
                    }
                }
            }
            #endregion
            // ---------------------------------------------------------------------------
        }
    }
}
