using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor shortcuts for the run data written by <see cref="RunProfiler"/>.</summary>
public static class RunDataMenu
{
    [MenuItem("Tools/Brick Breaker/Open Run Data Folder")]
    private static void OpenFolder()
    {
        Directory.CreateDirectory(RunProfiler.Folder);
        EditorUtility.RevealInFinder(RunProfiler.Folder);
    }

    [MenuItem("Tools/Brick Breaker/Clear Run Data")]
    private static void Clear()
    {
        if (!Directory.Exists(RunProfiler.Folder)) return;
        string[] files = Directory.GetFiles(RunProfiler.Folder, "run_*.json");
        if (!EditorUtility.DisplayDialog("Clear run data", $"Delete {files.Length} recorded run(s) from the editor?", "Delete", "Cancel")) return;
        foreach (string file in files) File.Delete(file);
    }
}
