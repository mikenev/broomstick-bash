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
    // negative control: a near-solid wall and a too-fast tight wall must be rejected, or the validator is broken
    var tight = new PhaseTuning{ fireballSpeed = 4f, telegraph = 1f, wallGap = 0.6f };
    var shots = FireballPatterns.Build(AttackKind.Wall, tight, new Vector2(10.9f, 0), 5f, 0, 10, new System.Random(1));
    bool detects = !FairnessValidator.IsFair(FairnessValidator.ToAbsolute(shots, 5f), FlapModel.Default, 5f, 0, 1f);
    Debug.Log($"[Fairness] negative control (0.6 m gap wall) rejected: {detects}");
    return bad == 0 && detects;
  }
  public static void RunBatch(){ bool ok = Run(); if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1); }
}
