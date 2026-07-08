using UnityEditor;
using UnityEditor.Build;

namespace WebGLDeploy.EditorTools
{
    // Applies the WebGL Player Settings decided in openspec/changes/add-webgl-deployment/design.md (D1-D3).
    public static class WebGLPlayerSettingsSetup
    {
        public const int InitialMemorySizeMb = 256;
        public const int MaximumMemorySizeMb = 1024;
        public const float GeometricMemoryGrowthStep = 0.2f;

        [MenuItem("Tools/WebGL Deploy/Apply Player Settings")]
        public static void Apply()
        {
            var target = NamedBuildTarget.WebGL;

            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSpeed);

            // D1: client-side decompression so the build works on hosts that don't set
            // Content-Encoding correctly (itch.io, GitHub Pages, etc.) without server config.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;

            // D2: no WebGL Threading Support - it requires COOP/COEP response headers that
            // free static hosts (itch.io) cannot set. Streaming Jobs must run single-threaded.
            PlayerSettings.WebGL.threadsSupport = false;

            // D3: explicit heap ceiling + geometric growth so an OOM fails loudly instead of
            // the browser tab silently crashing. Values are a starting point pending the
            // real-device measurement tracked as an Open Question in design.md.
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.initialMemorySize = InitialMemorySizeMb;
            PlayerSettings.WebGL.maximumMemorySize = MaximumMemorySizeMb;
            PlayerSettings.WebGL.geometricMemoryGrowthStep = GeometricMemoryGrowthStep;

            AssetDatabase.SaveAssets();
        }
    }
}
