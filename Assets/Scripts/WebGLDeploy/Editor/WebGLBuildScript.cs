using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WebGLDeploy.EditorTools
{
    // CI/local entry point: `Unity -batchmode -quit -buildTarget WebGL
    // -executeMethod WebGLDeploy.EditorTools.WebGLBuildScript.Build [-buildOutput <path>]`
    public static class WebGLBuildScript
    {
        private const string DefaultOutputDir = "Builds/WebGL";

        [MenuItem("Tools/WebGL Deploy/Build WebGL Player")]
        public static void Build()
        {
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                }

                WebGLPlayerSettingsSetup.Apply();
                BuildAddressablesContent();
                BuildPlayer();
            }
            catch (Exception e)
            {
                Debug.LogError($"WebGL build failed: {e}");
                EditorApplication.Exit(1);
            }
        }

        private static void BuildAddressablesContent()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("No AddressableAssetSettings found; cannot build Addressables content for WebGL.");
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"Addressables content build failed: {result.Error}");
            }
        }

        private static void BuildPlayer()
        {
            string outputPath = GetCommandLineArg("-buildOutput") ?? DefaultOutputDir;
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"WebGL player build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"WebGL build succeeded: {outputPath} ({report.summary.totalSize} bytes)");
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
