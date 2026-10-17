using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BroomstickBash;

/// <summary>Headless scene setup + WebGL build. Usage:
/// Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
/// (no -quit; this calls EditorApplication.Exit). Menu: Broomstick Bash/...</summary>
public static class BuildWeb {
  const string ScenePath = "Assets/Scenes/Main.unity";
  const string Gen = "Assets/Generated";
  const string TablePath = "Assets/Data/DefaultSpawnTable.asset";

  static Mesh Prim(PrimitiveType t){ var g = GameObject.CreatePrimitive(t); var m = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g); return m; }

  [MenuItem("Broomstick Bash/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory(Gen); Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Data");
    EnsureURP();
    var table = AssetDatabase.LoadAssetAtPath<SpawnTable>(TablePath);
    if (!table){ table = SpawnTable.CreateDefault(); AssetDatabase.CreateAsset(table, TablePath); AssetDatabase.SaveAssets(); }

    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.32f, 0.27f, 0.45f);
    RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.16f, 0.1f, 0.26f); RenderSettings.fogMode = FogMode.Linear;
    RenderSettings.fogStartDistance = 25; RenderSettings.fogEndDistance = 70;

    var cfg = new GameObject("GameConfig").AddComponent<GameConfig>();
    cfg.cube = Prim(PrimitiveType.Cube); cfg.sphere = Prim(PrimitiveType.Sphere); cfg.cylinder = Prim(PrimitiveType.Cylinder); cfg.capsule = Prim(PrimitiveType.Capsule);
    cfg.kidMat = Mat("Kid", new Color(0.3f, 0.75f, 0.35f));
    cfg.carMat = Mat("Car", new Color(0.35f, 0.45f, 0.65f));
    cfg.branchMat = Mat("Branch", new Color(0.3f, 0.2f, 0.12f));
    cfg.batMat = Mat("Bat", new Color(0.12f, 0.1f, 0.14f));
    cfg.ghostMat = Mat("Ghost", new Color(0.9f, 0.92f, 1f), new Color(0.25f, 0.25f, 0.3f));
    cfg.bossMat = Mat("WolfFur", new Color(0.42f, 0.36f, 0.3f));
    cfg.fireMat = Mat("Fire", new Color(1f, 0.45f, 0.05f), new Color(1.5f, 0.5f, 0f));
    cfg.spellMat = Mat("Spell", new Color(0.6f, 1f, 0.4f), new Color(0.6f, 1.5f, 0.3f));
    cfg.sparkleMat = Mat("Sparkle", new Color(1f, 0.95f, 0.5f), new Color(1.5f, 1.3f, 0.4f));
    cfg.houseMat = Mat("House", new Color(0.25f, 0.22f, 0.3f));
    cfg.groundMat = Mat("Street", new Color(0.2f, 0.19f, 0.22f));
    var witchMat = Mat("Witch", new Color(0.45f, 0.2f, 0.6f));
    var skinMat = Mat("Skin", new Color(0.55f, 0.8f, 0.5f));
    var windowMat = Mat("Window", new Color(1f, 0.8f, 0.3f), new Color(1.2f, 0.8f, 0.2f));

    var moon = new GameObject("Moonlight").AddComponent<Light>(); moon.type = LightType.Directional; moon.color = new Color(0.75f, 0.75f, 1f);
    moon.transform.rotation = Quaternion.Euler(40, -25, 0); moon.intensity = 1.1f; moon.shadows = LightShadows.Soft;

    // Witch (gray-box): robe capsule, head, hat, broom. Single trigger collider on root.
    var player = new GameObject("Witch"); player.layer = Layers.Player;
    Part(cfg.capsule, witchMat, player.transform, new Vector3(0, 0.05f, 0), new Vector3(0.55f, 0.4f, 0.55f), Vector3.zero);
    Part(cfg.sphere, skinMat, player.transform, new Vector3(0.05f, 0.55f, 0), Vector3.one * 0.38f, Vector3.zero);
    Part(cfg.cylinder, witchMat, player.transform, new Vector3(0.05f, 0.72f, 0), new Vector3(0.7f, 0.02f, 0.7f), Vector3.zero);
    Part(cfg.cylinder, witchMat, player.transform, new Vector3(0.0f, 0.92f, 0), new Vector3(0.28f, 0.2f, 0.28f), new Vector3(0, 0, 10));
    Part(cfg.cylinder, witchMat, player.transform, new Vector3(-0.06f, 1.18f, 0), new Vector3(0.12f, 0.12f, 0.12f), new Vector3(0, 0, 25));
    Part(cfg.cylinder, cfg.branchMat, player.transform, new Vector3(0, -0.3f, 0), new Vector3(0.08f, 0.9f, 0.08f), new Vector3(0, 0, 90));
    Part(cfg.cube, Mat("Bristles", new Color(0.8f, 0.65f, 0.3f)), player.transform, new Vector3(-1.0f, -0.3f, 0), new Vector3(0.45f, 0.3f, 0.3f), Vector3.zero);
    var col = player.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.42f; col.center = new Vector3(0, 0.1f, 0);
    var rb = player.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
    var pc = player.AddComponent<PlayerController>();

    var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
    var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor;
    cam.fieldOfView = 38; cam.farClipPlane = 120; camGo.AddComponent<UniversalAdditionalCameraData>();
    camGo.AddComponent<SideCamera>().target = player.transform; camGo.transform.position = new Vector3(5, 5, -17);

    // Scenery: street tiles + houses + lamps, recycled around the camera.
    var street = new GameObject("Street").AddComponent<Scenery>(); street.cam = camGo.transform; street.span = 60;
    for (int i = 0; i < 6; i++){
      var g = Block(cfg, "StreetTile", cfg.cube, cfg.groundMat, new Vector3(-25 + i * 10, -0.5f, 2), new Vector3(10, 1, 14), street.transform);
      Block(cfg, "Curb", cfg.cube, cfg.houseMat, new Vector3(-25 + i * 10, -0.35f, -4.6f), new Vector3(10, 0.4f, 0.6f), street.transform);
    }
    var houses = new GameObject("Houses").AddComponent<Scenery>(); houses.cam = camGo.transform; houses.span = 72;
    for (int i = 0; i < 8; i++){
      float h = 4 + (i * 37 % 5); float x = -30 + i * 9;
      var hs = Block(cfg, "House", cfg.cube, cfg.houseMat, new Vector3(x, h * 0.5f, 9), new Vector3(6, h, 5), houses.transform);
      var roof = Block(cfg, "Roof", cfg.cube, cfg.branchMat, Vector3.zero, Vector3.one, hs.transform);
      roof.transform.localPosition = new Vector3(0, 0.6f, 0); roof.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f); roof.transform.localRotation = Quaternion.Euler(0, 0, 45);
      var w = Block(cfg, "Window", cfg.cube, windowMat, Vector3.zero, Vector3.one, hs.transform);
      w.transform.localPosition = new Vector3(0.15f * ((i % 2) * 2 - 1), 0.1f, -0.51f); w.transform.localScale = new Vector3(0.18f, 0.18f / h * 4, 0.02f);
      var lamp = Block(cfg, "Lamp", cfg.cylinder, cfg.houseMat, new Vector3(x + 4.5f, 1.6f, 4f), new Vector3(0.15f, 1.6f, 0.15f), houses.transform);
      Block(cfg, "LampLight", cfg.sphere, windowMat, new Vector3(x + 4.5f, 3.3f, 4f), Vector3.one * 0.4f, lamp.transform);
    }
    Block(cfg, "Moon", cfg.sphere, Mat("Moon", new Color(1f, 1f, 0.85f), new Color(0.8f, 0.8f, 0.6f)), new Vector3(0, 0, 0), Vector3.one * 6, camGo.transform)
      .transform.localPosition = new Vector3(14, 8, 60);

    var spawner = new GameObject("Spawner").AddComponent<Spawner>();
    spawner.table = table; spawner.cam = camGo.transform; spawner.player = player.transform;

    // Werewolf boss (gray-box). Art swap point: Werewolf.visualPrefab (hides the "GrayBox" child when assigned).
    var bossGo = new GameObject("Werewolf"); bossGo.layer = Layers.Enemy; bossGo.transform.position = new Vector3(40, 5, 0);
    var gb = new GameObject("GrayBox").transform; gb.SetParent(bossGo.transform, false); gb.gameObject.layer = Layers.Enemy;
    var fur = cfg.bossMat; var dark = Mat("WolfDark", new Color(0.18f, 0.15f, 0.13f));
    var eyes = Mat("WolfEyes", new Color(1f, 0.9f, 0.2f), new Color(2f, 1.6f, 0.2f));
    Part(cfg.cube, fur, gb, new Vector3(0, -0.1f, 0), new Vector3(1.5f, 1.8f, 1.1f), new Vector3(0, 0, -8));     // torso (hunched)
    Part(cfg.cube, fur, gb, new Vector3(-0.55f, 1.15f, 0), new Vector3(1.0f, 0.85f, 0.9f), Vector3.zero);       // head
    Part(cfg.cube, dark, gb, new Vector3(-1.2f, 1.0f, 0), new Vector3(0.6f, 0.4f, 0.5f), Vector3.zero);         // snout
    Part(cfg.cube, dark, gb, new Vector3(-0.35f, 1.7f, 0.25f), new Vector3(0.22f, 0.45f, 0.15f), new Vector3(0, 0, 15)); // ears
    Part(cfg.cube, dark, gb, new Vector3(-0.35f, 1.7f, -0.25f), new Vector3(0.22f, 0.45f, 0.15f), new Vector3(0, 0, 15));
    Part(cfg.cube, eyes, gb, new Vector3(-0.95f, 1.3f, -0.46f), new Vector3(0.14f, 0.1f, 0.05f), Vector3.zero);
    Part(cfg.cube, eyes, gb, new Vector3(-0.65f, 1.3f, -0.46f), new Vector3(0.14f, 0.1f, 0.05f), Vector3.zero);
    Part(cfg.cube, fur, gb, new Vector3(-1.0f, 0.35f, -0.35f), new Vector3(1.1f, 0.35f, 0.35f), new Vector3(0, 0, 20)); // throwing arm
    Part(cfg.cube, fur, gb, new Vector3(-0.4f, -0.3f, 0.4f), new Vector3(0.35f, 1.0f, 0.35f), new Vector3(0, 0, -15));
    Part(cfg.cube, dark, gb, new Vector3(0.2f, -1.3f, 0.3f), new Vector3(0.4f, 0.9f, 0.4f), new Vector3(0, 0, 10));     // legs
    Part(cfg.cube, dark, gb, new Vector3(0.45f, -1.3f, -0.3f), new Vector3(0.4f, 0.9f, 0.4f), new Vector3(0, 0, -10));
    Part(cfg.cube, fur, gb, new Vector3(0.95f, -0.4f, 0), new Vector3(0.9f, 0.25f, 0.25f), new Vector3(0, 0, -35));    // tail
    foreach (Transform c in gb) c.gameObject.layer = Layers.Enemy;
    var bcol = bossGo.AddComponent<BoxCollider>(); bcol.center = new Vector3(-0.3f, 0.2f, 0); bcol.size = new Vector3(2.0f, 3.2f, 1.2f);
    var boss = bossGo.AddComponent<Werewolf>(); boss.cam = camGo.transform; boss.player = player.transform; boss.grayBox = gb;
    var ba = new GameObject("BossAudio").AddComponent<BossAudio>();
    AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + n);
    ba.bossTheme = Clip("Music/boss_theme_loop.ogg"); ba.victory = Clip("Music/victory_sting.ogg");
    ba.roar = Clip("SFX/werewolf_roar.ogg"); ba.hurt = Clip("SFX/boss_hurt.ogg"); ba.throwSfx = Clip("SFX/fireball_throw.ogg"); ba.defeat = Clip("SFX/boss_defeat.ogg");

    var gm = new GameObject("GameManager").AddComponent<GameManager>();
    gm.player = pc; gm.spawner = spawner; gm.boss = boss; gm.cam = camGo.transform;
    bossGo.SetActive(false);

    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[]{ new EditorBuildSettingsScene(ScenePath, true) };
    AssetDatabase.SaveAssets();
  }

  static GameObject Block(GameConfig cfg, string n, Mesh m, Material mat, Vector3 pos, Vector3 scale, Transform parent){
    var g = new GameObject(n); if (parent) g.transform.SetParent(parent, false);
    g.transform.position = pos; g.transform.localScale = scale;
    g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat; return g;
  }
  static void Part(Mesh m, Material mat, Transform parent, Vector3 lp, Vector3 ls, Vector3 euler){
    var g = new GameObject("Part"); g.layer = parent.gameObject.layer; g.transform.SetParent(parent, false);
    g.transform.localPosition = lp; g.transform.localScale = ls; g.transform.localRotation = Quaternion.Euler(euler);
    g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
  }

  static void EnsureURP(){
    var path = Gen + "/URP_Asset.asset";
    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
    if (!asset){
      var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
      AssetDatabase.CreateAsset(rd, Gen + "/URP_Renderer.asset");
      asset = UniversalRenderPipelineAsset.Create(rd);
      AssetDatabase.CreateAsset(asset, path);
      AssetDatabase.SaveAssets();
    }
    GraphicsSettings.defaultRenderPipeline = asset;
    for (int i = 0; i < QualitySettings.names.Length; i++){ QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
  }

  static Material Mat(string name, Color c, Color? emission = null){
    var p = $"{Gen}/{name}.mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
    if (!m){ m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
    m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.15f);
    if (emission.HasValue){ m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
    EditorUtility.SetDirty(m);
    return m;
  }

  [MenuItem("Broomstick Bash/Build WebGL to docs")]
  public static void Build(){
    int code = 1;
    try {
      if (!BossFairnessTest.Run()) throw new System.Exception("Boss fairness sweep found unfair patterns; see [Fairness] warnings");
      SetupScene();
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
      PlayerSettings.WebGL.decompressionFallback = true;
      PlayerSettings.WebGL.threadsSupport = false;
      PlayerSettings.productName = "Broomstick Bash"; PlayerSettings.companyName = "mikenev";
      bool dev = System.Environment.GetEnvironmentVariable("BB_DEV") == "1";   // dev build w/ stack traces, for debugging only
      if (dev){ PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full); }
      else PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
      var outDir = Path.GetFullPath(dev ? "Builds/WebGLDev" : "docs");
      var r = BuildPipeline.BuildPlayer(new[]{ ScenePath }, outDir, BuildTarget.WebGL, dev ? BuildOptions.Development : BuildOptions.None);
      Debug.Log("BUILD RESULT: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded){ File.WriteAllText(Path.Combine(outDir, ".nojekyll"), ""); code = 0; }
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}
