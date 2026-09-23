using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;


namespace MAST
{
    // Detects the project's active render pipeline and points MAST's editor
    // materials (grid, paint area, eraser) at the matching shader variant.
    // URP is the default; the Built-in render pipeline remains supported and
    // is adapted to automatically — no manual shader swapping.
    public static class RenderPipelineUtil
    {
        public enum Pipeline { BuiltIn, Universal }

        public static Pipeline ActivePipeline
        {
            get
            {
                // null = Built-in render pipeline.  Any scriptable pipeline gets
                // the URP shader set: MAST's overlays are plain unlit passes that
                // URP renders through SRPDefaultUnlit.  (HDRP is not supported.)
                return GraphicsSettings.currentRenderPipeline == null
                    ? Pipeline.BuiltIn
                    : Pipeline.Universal;
            }
        }

        // Ensure every MAST editor material uses the shader for the active
        // pipeline.  Runs when the MAST window opens; cheap when nothing changed.
        public static void ApplyPipelineShaders()
        {
            bool universal = ActivePipeline == Pipeline.Universal;

            SwapShader(LoadingHelper.GetGridMaterial(),
                universal ? "MAST/URP/Shader_Grid_URP" : "MAST/SRP/Shader_Grid_SRP");

            SwapShader(LoadingHelper.GetPaintAreaMaterial(),
                universal ? "MAST/URP/Shader_PaintArea_URP" : "MAST/SRP/Shader_PaintArea_SRP");
        }

        private static void SwapShader(Material material, string shaderName)
        {
            if (material == null)
                return;

            if (material.shader != null && material.shader.name == shaderName)
                return;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("MAST: could not find shader '" + shaderName + "' to match the active render pipeline.");
                return;
            }

            material.shader = shader;
            EditorUtility.SetDirty(material);
        }
    }
}
