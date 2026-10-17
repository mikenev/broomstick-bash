using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using BroomstickBash.Art;

/// <summary>Broomstick Bash art import. Configures the procedural FBX models in Assets/Art/Models (generic rigs, clip names and
/// loops, flat URP palette materials incl. emissive *Glow and translucent Ghost*), builds Animator controllers and writes
/// prefabs to Assets/Art/Resources/Art (loaded at runtime with BroomArt.Load). Menu: Broomstick Bash/Art/Rebuild Art Prefabs.
/// Batch: -executeMethod BroomArtSetup.BatchRebuild. Source: ArtSource/build_assets.py (Blender 4.2, headless).</summary>
public static class BroomArtSetup {
  const string Root = "Assets/Art", Models = Root + "/Models", Mats = Root + "/Materials", Out = Root + "/Resources/Art", Ctl = Root + "/Animators";
  /// <summary>Yaw applied to every model under its prefab root so that characters face +X (flight direction) and their
  /// authored front faces the side camera at -Z. Determined from the import (see Validate: Witch bristles must be at -X).</summary>
  public const float ModelYaw = 180f;
  static readonly Dictionary<string, Color> Palette = new() {
      {"Skin", new Color(0.55f,0.78f,0.45f)},
      {"SkinWarm", new Color(0.85f,0.62f,0.45f)},
      {"Robe", new Color(0.30f,0.12f,0.45f)},
      {"RobeDark", new Color(0.16f,0.06f,0.26f)},
      {"HatBand", new Color(0.95f,0.50f,0.08f)},
      {"Hair", new Color(0.85f,0.30f,0.10f)},
      {"Broom", new Color(0.45f,0.28f,0.12f)},
      {"Straw", new Color(0.90f,0.72f,0.30f)},
      {"Boot", new Color(0.10f,0.08f,0.10f)},
      {"Stripe", new Color(0.95f,0.55f,0.10f)},
      {"Pumpkin", new Color(0.95f,0.45f,0.05f)},
      {"PumpkinDark", new Color(0.70f,0.28f,0.03f)},
      {"Stem", new Color(0.25f,0.45f,0.12f)},
      {"Sheet", new Color(0.93f,0.92f,0.88f)},
      {"Cardboard", new Color(0.72f,0.55f,0.32f)},
      {"Metal", new Color(0.62f,0.66f,0.72f)},
      {"Shoe", new Color(0.85f,0.15f,0.15f)},
      {"ShoeBlue", new Color(0.15f,0.35f,0.85f)},
      {"Candy", new Color(0.95f,0.75f,0.10f)},
      {"Jeans", new Color(0.20f,0.30f,0.55f)},
      {"Black", new Color(0.05f,0.05f,0.07f)},
      {"BatWing", new Color(0.22f,0.12f,0.30f)},
      {"GhostBody", new Color(0.58f,1.0f,0.93f)},
      {"EyeGlow", new Color(0.70f,1.0f,0.30f)},
      {"Fur", new Color(0.35f,0.30f,0.32f)},
      {"FurDark", new Color(0.20f,0.17f,0.20f)},
      {"Claw", new Color(0.92f,0.90f,0.80f)},
      {"Shorts", new Color(0.35f,0.18f,0.45f)},
      {"Tooth", new Color(0.95f,0.95f,0.90f)},
      {"Tongue", new Color(0.80f,0.25f,0.35f)},
      {"CarRed", new Color(0.70f,0.12f,0.15f)},
      {"CarTeal", new Color(0.10f,0.45f,0.50f)},
      {"Glass", new Color(0.25f,0.35f,0.55f)},
      {"Tire", new Color(0.08f,0.08f,0.09f)},
      {"LightGlow", new Color(1.0f,0.85f,0.45f)},
      {"Bark", new Color(0.22f,0.15f,0.12f)},
      {"BarkDark", new Color(0.14f,0.10f,0.09f)},
      {"LeafOrange", new Color(0.85f,0.40f,0.08f)},
      {"Lamp", new Color(0.18f,0.18f,0.22f)},
      {"HouseA", new Color(0.17f,0.17f,0.27f)},
      {"HouseB", new Color(0.14f,0.16f,0.27f)},
      {"HouseC", new Color(0.19f,0.16f,0.26f)},
      {"Roof", new Color(0.08f,0.08f,0.14f)},
      {"WindowGlow", new Color(0.55f,0.46f,0.34f)},
      {"FarHouse", new Color(0.09f,0.09f,0.18f)},
      {"Moon", new Color(0.95f,0.93f,0.75f)},
      {"MoonCrater", new Color(0.80f,0.78f,0.62f)},
      {"Cloud", new Color(0.20f,0.22f,0.36f)},
      {"Fence", new Color(0.10f,0.10f,0.16f)},
      {"PumpkinGlow", new Color(0.62f,0.42f,0.20f)},
      {"PumpkinBG", new Color(0.42f,0.24f,0.12f)},
      {"StemBG", new Color(0.14f,0.20f,0.12f)},
      {"SpellGlow", new Color(0.75f,0.40f,1.0f)},
      {"SparkleGlow", new Color(1.0f,0.95f,0.55f)},
      {"FireGlow", new Color(1.0f,0.45f,0.08f)},
      {"FireCore", new Color(1.0f,0.85f,0.30f)},
      {"Grass", new Color(0.08f,0.11f,0.14f)},
      {"OutlineWarm", new Color(1.0f,0.82f,0.50f)},
      {"OutlineCool", new Color(0.70f,0.95f,1.0f)},
      {"Gold", new Color(1.0f,0.76f,0.15f)},
  };
  static readonly HashSet<string> Looping = new() { "Fly", "Walk", "Flap@Bat", "Float", "Idle" };
  static readonly string[] Static = { "Car_Red", "Car_Teal", "Branch", "LampPost", "BG_Moon", "BG_HousesFar", "BG_HousesNear", "BG_Pumpkins", "FX_Spell", "FX_Sparkle", "FX_Fireball" };
  /// <summary>Background-only glows kept dim so windows / jack-o'-lanterns don't compete with gameplay objects.</summary>
  static readonly HashSet<string> BackgroundGlow = new() { "WindowGlow", "PumpkinGlow" };
  /// <summary>Art-only upscale for the kid costumes (applied to the prefab's Model child, so BroomArtSkin's fit-to-gray-box
  /// scale on the instance root and the gray-box collider are unchanged; feet stay on the ground because the origin is at the feet).</summary>
  public const float KidArtScale = 1.45f;
  static readonly string[] Kids = { "Kid_Pumpkin", "Kid_SheetGhost", "Kid_Robot" };
  static readonly string[] Animated = { "Witch", "Werewolf", "Bat", "Ghost", "Kid_Pumpkin", "Kid_SheetGhost", "Kid_Robot" };

  [MenuItem("Broomstick Bash/Art/Rebuild Art Prefabs")]
  public static void Rebuild(){
    Directory.CreateDirectory(Mats); Directory.CreateDirectory(Out); Directory.CreateDirectory(Ctl);
    var mats = Palette.ToDictionary(kv => kv.Key, kv => Mat(kv.Key, kv.Value));
    foreach (var path in Directory.GetFiles(Models, "*.fbx")) Configure(path.Replace('\\', '/'), mats);
    AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    foreach (var n in Static) Prefab(n, null);
    Prefab("Witch", Controller("Witch", "Fly", new[]{ "Flap", "Cast", "Hurt" }));
    Prefab("Werewolf", Controller("Werewolf", "Idle", new[]{ "Throw", "Hurt" }, "Defeat"));
    Prefab("Bat", Controller("Bat", "Flap", new string[0]));
    Prefab("Ghost", Controller("Ghost", "Float", new string[0]));
    foreach (var k in Kids) Prefab(k, Controller(k, "Walk", new[]{ "Cheer" }));
    AssetDatabase.SaveAssets();
    Debug.Log("BroomArt: rebuilt " + (Static.Length + Animated.Length) + " prefabs");
  }
  public static void BatchRebuild(){
    int code = 0;
    try { Rebuild(); Validate(); } catch (System.Exception e){ Debug.LogException(e); code = 1; }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }

  static Material Mat(string name, Color c){
    var p = $"{Mats}/BA_{name}.mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
    var shader = Shader.Find(name.StartsWith("Outline") ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
    if (!m){ m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
    if (m.shader != shader) m.shader = shader;
    if (name.StartsWith("Outline")){ // inverted-hull outline shell (flipped faces in the mesh, Cull Back): flat unlit rim colour
      m.SetColor("_BaseColor", c); m.SetFloat("_Cull", 2); EditorUtility.SetDirty(m); return m; }
    bool ghost = name.StartsWith("Ghost");
    m.SetColor("_BaseColor", ghost ? new Color(c.r, c.g, c.b, 0.8f) : c); // Gamma colour space: palette used as-is
    m.SetFloat("_Metallic", name == "Metal" || name == "Gold" ? 0.5f : 0f); m.SetFloat("_Smoothness", name == "Glass" ? 0.6f : 0.12f);
    if (name.EndsWith("Glow")){ m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * (BackgroundGlow.Contains(name) ? 0.35f : 0.9f)); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
    else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
    if (ghost){ // translucent enemy ghosts
      m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
      m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
      m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; m.SetOverrideTag("RenderType", "Transparent");
      m.SetColor("_EmissionColor", c * 0.4f); m.EnableKeyword("_EMISSION");
    }
    EditorUtility.SetDirty(m); return m;
  }

  static void Configure(string path, Dictionary<string, Material> mats){
    var imp = (ModelImporter)AssetImporter.GetAtPath(path);
    imp.globalScale = 1; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false;
    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; imp.isReadable = false;
    imp.meshCompression = ModelImporterMeshCompression.Medium; imp.importNormals = ModelImporterNormals.Import; imp.importBlendShapes = false;
    string n = Path.GetFileNameWithoutExtension(path);
    bool animated = Animated.Contains(n);
    imp.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None; imp.importAnimation = animated;
    foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
    imp.SaveAndReimport();
    if (animated){
      imp.clipAnimations = imp.defaultClipAnimations.Select(c => {
        var s = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
        c.name = s; c.loopTime = Looping.Contains(s) || Looping.Contains(s + "@" + n); return c; }).ToArray();
      imp.SaveAndReimport();
    }
  }
  static AnimationClip Clip(string model, string name) =>
    AssetDatabase.LoadAllAssetsAtPath($"{Models}/{model}.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == name)
    ?? throw new System.Exception($"BroomArt: clip {model}/{name} not found");

  /// <summary>Default looping state + one trigger per one-shot state (trigger name == state name). One-shots return to the
  /// default state; the optional terminal state (e.g. Defeat) does not.</summary>
  static AnimatorController Controller(string model, string def, string[] oneShots, string terminal = null){
    var path = $"{Ctl}/{model}.controller";
    var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
    if (ac){ // rebuild in place so the asset GUID (and prefab references) stay stable
      foreach (var p in ac.parameters.ToArray()) ac.RemoveParameter(p);
      var old = ac.layers[0].stateMachine; foreach (var s in old.states.ToArray()) old.RemoveState(s.state);
      foreach (var t in old.anyStateTransitions.ToArray()) old.RemoveAnyStateTransition(t);
    } else ac = AnimatorController.CreateAnimatorControllerAtPath(path);
    var sm = ac.layers[0].stateMachine;
    var d = sm.AddState(def, new Vector3(250, 0)); d.motion = Clip(model, def); sm.defaultState = d;
    int i = 0;
    foreach (var st in oneShots.Concat(terminal != null ? new[]{ terminal } : new string[0])){
      ac.AddParameter(st, AnimatorControllerParameterType.Trigger);
      var s = sm.AddState(st, new Vector3(500, 80 * i++)); s.motion = Clip(model, st);
      var t = sm.AddAnyStateTransition(s); t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0, st);
      if (st != terminal){ var back = s.AddTransition(d); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f; }
    }
    EditorUtility.SetDirty(ac); return ac;
  }

  static void Prefab(string n, RuntimeAnimatorController ctl){
    var root = new GameObject("BA_" + n);
    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{n}.fbx"));
    model.name = "Model"; model.transform.SetParent(root.transform, false); model.transform.localRotation = Quaternion.Euler(0, ModelYaw, 0);
    if (Kids.Contains(n)) model.transform.localScale = Vector3.one * KidArtScale;
    foreach (var r in model.GetComponentsInChildren<Renderer>()){
      bool cheap = n.StartsWith("BG_") || n.StartsWith("FX_") || n == "Ghost";
      r.shadowCastingMode = cheap ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
      r.receiveShadows = !n.StartsWith("BG_");
      if (r is SkinnedMeshRenderer smr){ smr.quality = SkinQuality.Bone1; }
    }
    if (ctl){ if (!model.TryGetComponent<Animator>(out var a)) a = model.AddComponent<Animator>(); a.runtimeAnimatorController = ctl; a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
    if (n.StartsWith("FX_")) root.AddComponent<BroomFxSpin>();
    PrefabUtility.SaveAsPrefabAsset(root, $"{Out}/BA_{n}.prefab"); Object.DestroyImmediate(root);
  }

  /// <summary>Headless checks: every prefab loads, all materials are URP Lit, every Animator state has a clip; logs tris and bounds.</summary>
  public static void Validate(){
    int total = 0;
    foreach (var n in Static.Concat(Animated)){
      var p = BroomArt.Load(n) ?? throw new System.Exception("missing prefab " + n);
      var inst = Object.Instantiate(p); int t = 0; Bounds b = default; bool first = true;
      foreach (var r in inst.GetComponentsInChildren<Renderer>(true)){
        var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh; t += mesh.triangles.Length / 3;
        foreach (var m in r.sharedMaterials) if (!m || (m.shader.name != "Universal Render Pipeline/Lit" && !(m.name.StartsWith("BA_Outline") && m.shader.name == "Universal Render Pipeline/Unlit"))) throw new System.Exception(n + ": bad material " + (m ? m.name : "null"));
        if (first){ b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
      }
      var an = inst.GetComponentInChildren<Animator>();
      string states = an ? string.Join(",", ((AnimatorController)an.runtimeAnimatorController).layers[0].stateMachine.states.Select(s => s.state.name + (s.state.motion ? "" : "(NO CLIP)"))) : "-";
      if (states.Contains("NO CLIP")) throw new System.Exception(n + " state without clip");
      Debug.Log($"BroomArt VALID {n}: tris={t} center={b.center} size={b.size} states={states}");
      if (n == "Witch" && b.center.x >= 0) throw new System.Exception("Witch must face +X (broom bristles behind at -X); check ModelYaw");
      if (n == "LampPost" && b.center.x <= 0) throw new System.Exception("LampPost arm must point +X; check ModelYaw");
      total += t; Object.DestroyImmediate(inst);
    }
    Debug.Log("BroomArt VALIDATION OK total tris=" + total);
  }
}
