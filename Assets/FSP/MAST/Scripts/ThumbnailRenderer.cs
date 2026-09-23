using UnityEngine;
using UnityEditor;


namespace MAST
{
    // Offscreen palette thumbnail renderer built on the editor's
    // PreviewRenderUtility.  Replaces the old scene-camera prefab rig
    // "Prefab_Preview_Camera + ThumbnailCamera component", which instantiated
    // subjects into the user's scene and depended on a prefab asset binding
    // its script: this renders in the editor's isolated preview scene with
    // its own lighting, touches nothing in the user's scene, and renders
    // through whichever render pipeline is active.
    //
    // The camera framing math and the Settings > GUI > Palette options
    // "pitch, yaw, thumbnail size" are carried over unchanged.
    public static class ThumbnailRenderer
    {
        public static Texture2D[] RenderPrefabThumbnails(GameObject[] prefabs)
        {
            var thumbnails = new Texture2D[prefabs.Length];

            PreviewRenderUtility preview = CreatePreview();
            try
            {
                for (int i = 0; i < prefabs.Length; i++)
                {
                    if (prefabs[i] == null)
                        continue;

                    GameObject subject = preview.InstantiatePrefabInScene(prefabs[i]);
                    thumbnails[i] = RenderSubject(preview, subject);
                    Object.DestroyImmediate(subject);
                }
            }
            finally
            {
                preview.Cleanup();
            }

            return thumbnails;
        }

        public static Texture2D[] RenderMaterialThumbnails(Material[] materials)
        {
            var thumbnails = new Texture2D[materials.Length];

            PreviewRenderUtility preview = CreatePreview();
            try
            {
                // One sphere, moved into the preview scene, re-skinned per material
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                preview.AddSingleGO(sphere);

                var sphereRenderer = sphere.GetComponent<MeshRenderer>();

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                        continue;

                    sphereRenderer.sharedMaterial = materials[i];
                    thumbnails[i] = RenderSubject(preview, sphere);
                }
            }
            finally
            {
                // Cleanup also destroys anything added to the preview scene
                preview.Cleanup();
            }

            return thumbnails;
        }

        // ------------------------------------------------------------------

        private static PreviewRenderUtility CreatePreview()
        {
            var preview = new PreviewRenderUtility();

            preview.camera.fieldOfView = 27f;
            preview.camera.nearClipPlane = 0.1f;
            preview.camera.farClipPlane = 10000f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0f, 0f, 0f, 0.25f);

            // Lighting is set per render in RenderSubject "key + fill aimed
            // relative to the camera, colors from Settings" — just make sure
            // the preview lights are on; scene lighting plays no part at all
            foreach (Light light in preview.lights)
            {
                if (light == null)
                    continue;
                light.enabled = true;
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
            }

            return preview;
        }

        private static Texture2D RenderSubject(PreviewRenderUtility preview, GameObject subject)
        {
            // ----------------------------------
            // Calculate subject bounds
            // ----------------------------------
            Bounds bounds;
            Renderer[] renderers = subject.GetComponentsInChildren<Renderer>();

            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
            }
            else
            {
                bounds = new Bounds(Vector3.zero, Vector3.zero);
            }

            // ----------------------------------
            // Orient and fit the camera "same math and settings as the old rig"
            // ----------------------------------
            float pitch = 0f;
            float yaw = 0f;
            float lightPitch = 30f;
            float lightYaw = 40f;
            Color lightColor = Color.white;
            var ambientColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            int thumbnailSize = 128;

            if (Settings.Data.gui != null)
            {
                pitch = Settings.Data.gui.palette.snapshotCameraPitch;
                yaw = Settings.Data.gui.palette.snapshotCameraYaw;
                lightPitch = Settings.Data.gui.palette.snapshotLightPitch;
                lightYaw = Settings.Data.gui.palette.snapshotLightYaw;
                lightColor = Settings.Data.gui.palette.snapshotLightColor;
                ambientColor = Settings.Data.gui.palette.snapshotAmbientColor;
                thumbnailSize = Mathf.Clamp(Settings.Data.gui.palette.thumbnailSize, 32, 512);
            }

            Quaternion rigRotation = Quaternion.Euler(yaw, pitch, 0f);

            // ----------------------------------
            // Aim the lights "relative to the camera view, through the subject
            // center".  The default 30/40 gives the classic slightly-from-the-
            // top-left key light; a soft opposite fill keeps the dark side
            // readable without depending on any scene lighting.  Colors come
            // from Settings — the ambient color's brightness IS its intensity
            // ----------------------------------
            Quaternion keyRotation = rigRotation * Quaternion.Euler(lightYaw, lightPitch, 0f);

            // Intensities are tuned like a typical scene's single directional
            // light "what the old rig borrowed": key at 1, a faint fill.  Any
            // hotter washes out in Gamma color space projects, where light
            // contributions add in sRGB.  Brightness beyond this comes from the
            // Light and Ambient color settings — their brightness IS intensity
            if (preview.lights.Length > 0 && preview.lights[0] != null)
            {
                preview.lights[0].transform.rotation = keyRotation;
                preview.lights[0].color = lightColor;
                preview.lights[0].intensity = 1f;
            }
            if (preview.lights.Length > 1 && preview.lights[1] != null)
            {
                preview.lights[1].transform.rotation =
                    rigRotation * Quaternion.Euler(lightYaw * 0.5f, lightPitch + 180f, 0f);
                preview.lights[1].color = lightColor;
                preview.lights[1].intensity = 0.3f;
            }

            preview.ambientColor = ambientColor;

            float objectSize = Mathf.Max(0.001f,
                Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            float cameraView = 3.5f * Mathf.Tan(0.5f * Mathf.Deg2Rad * preview.camera.fieldOfView);
            float distance = 2.5f * objectSize / cameraView + 0.5f * objectSize;

            preview.camera.transform.SetPositionAndRotation(
                bounds.center - distance * (rigRotation * Vector3.forward),
                rigRotation);

            // ----------------------------------
            // Render through the documented Begin/Render/End sandwich: Begin
            // activates the preview scene's lighting override "ambient and
            // lights" and End restores it.  Skipping the sandwich left the
            // override pointing at the destroyed preview scene after Cleanup,
            // which was a FATAL editor crash on the next AssetDatabase.Refresh.
            // EndStaticPreview would flatten alpha, so the pixels are read
            // straight from the preview's ARGB render target BEFORE EndPreview
            // — thumbnails keep their transparent background
            // ----------------------------------
            preview.BeginPreview(new Rect(0f, 0f, thumbnailSize, thumbnailSize), GUIStyle.none);
            preview.Render(true, false);

            var renderTarget = preview.camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTarget;

            var snapshot = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGBA32, false);
            snapshot.ReadPixels(new Rect(0f, 0f, renderTarget.width, renderTarget.height), 0, 0);

            // PNGs hold sRGB-encoded pixels.  In a Linear color space project
            // with a non-sRGB render target, the readback is raw linear values —
            // saved as-is they display washed out, so encode them first.  In
            // Gamma projects "or with an sRGB target" the bytes are already right
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && !renderTarget.sRGB)
            {
                Color[] pixels = snapshot.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = pixels[i].gamma;
                snapshot.SetPixels(pixels);
            }

            snapshot.Apply();

            RenderTexture.active = previousActive;
            preview.EndPreview();

            return snapshot;
        }
    }
}
