using UnityEditor;
using UnityEngine;
using BroomstickBash;
/// <summary>Test-time fairness sweep for the werewolf patterns (also run automatically by BuildWeb.Build).
/// Headless: Unity -batchmode -nographics -projectPath . -executeMethod BossFairnessTest.RunBatch -logFile f.log</summary>
public static class BossFairnessTest {
  [MenuItem("Broomstick Bash/Check Boss Fairness")]
  public static bool Run(){
    var (total, bad) = FairnessValidator.Sweep(Werewolf.DefaultPhases(), FlapModel.Default);
    Debug.Log($"[Fairness] {total - bad}/{total} werewolf patterns fair (margin {FairnessValidator.Margin} m, flap every >= {FairnessValidator.DecisionStep}s)");
    return bad == 0;
  }
  public static void RunBatch(){ bool ok = Run(); if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1); }
}
