using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FunFlow.Editor
{
    public static class ProjectBuilder
    {
        [MenuItem("FunFlow/Create game scene")]
        public static void Prepare()
        {
            PlayerSettings.companyName = "FunFlow TPU Team";
            PlayerSettings.productName = "Ricochet - FunFlow Tanks";
            PlayerSettings.bundleVersion = "3.1.0";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            SerializedObject settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty input = settings.FindProperty("activeInputHandler");
            if (input != null) input.intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Ricochet Game", typeof(RicochetGame), typeof(GameQA));
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Ricochet.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Ricochet.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("FUNFLOW_SCENE_READY");
        }

        [MenuItem("FunFlow/Build Windows")]
        public static void Build()
        {
            Prepare();
            string output = ReadArgument("-buildOutput") ?? Path.GetFullPath("../Windows/Ricochet.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Ricochet.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Build failed: " + report.summary.result + "; errors: " + report.summary.totalErrors);
            Debug.Log("FUNFLOW_BUILD_SUCCESS " + output);
        }

        private static string ReadArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
