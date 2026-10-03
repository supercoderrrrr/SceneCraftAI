using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SceneCraftAI.Editor
{
    public static class PortfolioBuild
    {
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sceneBuild");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Provide -sceneBuild with an output executable path");
            string output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log("SCENE_BUILD: " + report.summary.result + ", errors " + report.summary.totalErrors);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
