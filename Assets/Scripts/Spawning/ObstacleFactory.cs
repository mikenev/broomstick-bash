using UnityEngine;
namespace BroomstickBash {
  /// <summary>Builds gray-box obstacles (or instantiates the entry prefab). Colliders are added explicitly (see link.xml).</summary>
  public static class ObstacleFactory {
    public static ObstacleBase Create(SpawnEntry e, float x, GameConfig cfg){
      GameObject go; ObstacleBase ob;
      if (e.prefab){
        go = Object.Instantiate(e.prefab, new Vector3(x, Y(e, cfg), 0), Quaternion.identity);
        ob = go.GetComponent<ObstacleBase>(); if (!ob) ob = go.AddComponent<ObstacleBase>();
        ob.kind = e.kind; return ob;
      }
      switch (e.kind){
        case ObstacleKind.Kid: {
          float h = Random.Range(1.1f, 1.5f);
          go = cfg.MakeBlock("Kid", cfg.capsule, cfg.kidMat, new Vector3(x, cfg.groundY + h * 0.5f, 0), new Vector3(0.7f, h * 0.5f, 0.7f));
          // pumpkin bucket
          var b = cfg.MakeBlock("Bucket", cfg.sphere, cfg.fireMat, Vector3.zero, Vector3.one * 0.5f, go.transform);
          b.transform.localPosition = new Vector3(-0.6f, -0.2f, -0.3f);
          go.AddComponent<CapsuleCollider>();
          go.layer = Layers.Kid; ob = go.AddComponent<Kid>(); ob.speed = Random.Range(0.3f, 1.0f); break;
        }
        case ObstacleKind.Car: {
          go = cfg.MakeBlock("Car", cfg.cube, cfg.carMat, new Vector3(x, cfg.groundY + 0.6f, 0), new Vector3(3.2f, 1.2f, 1.6f));
          var top = cfg.MakeBlock("Cabin", cfg.cube, cfg.carMat, Vector3.zero, Vector3.one, go.transform);
          top.transform.localPosition = new Vector3(-0.05f, 0.8f, 0); top.transform.localScale = new Vector3(0.55f, 0.6f, 0.9f);
          go.AddComponent<BoxCollider>().size = new Vector3(1, 1.6f, 1); go.GetComponent<BoxCollider>().center = new Vector3(0, 0.3f, 0);
          go.layer = Layers.Obstacle; ob = go.AddComponent<ObstacleBase>(); ob.speed = Random.Range(1.5f, 3f); break;
        }
        case ObstacleKind.Branch: {
          float len = Random.Range(2.5f, 4.2f);
          go = cfg.MakeBlock("Branch", cfg.cube, cfg.branchMat, new Vector3(x, cfg.ceilingY - len * 0.5f, 0), new Vector3(0.8f, len, 0.8f));
          go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-12f, 12f));
          go.AddComponent<BoxCollider>();
          go.layer = Layers.Obstacle; ob = go.AddComponent<ObstacleBase>(); ob.speed = 0; break;
        }
        case ObstacleKind.Ghost: {
          go = cfg.MakeBlock("Ghost", cfg.capsule, cfg.ghostMat, new Vector3(x, Y(e, cfg), 0), new Vector3(0.9f, 0.6f, 0.9f));
          go.AddComponent<CapsuleCollider>();
          go.layer = Layers.Enemy; ob = go.AddComponent<Ghost>(); ob.speed = 1.2f; ob.hp = 2; ob.scoreValue = 25; break;
        }
        default: {
          go = cfg.MakeBlock("Bat", cfg.cube, cfg.batMat, new Vector3(x, Y(e, cfg), 0), new Vector3(1.3f, 0.35f, 0.5f));
          go.AddComponent<BoxCollider>();
          go.layer = Layers.Enemy; ob = go.AddComponent<Bat>(); ob.speed = 2.5f; ob.scoreValue = 15; break;
        }
      }
      ob.kind = e.kind; return ob;
    }
    static float Y(SpawnEntry e, GameConfig cfg) => e.maxY > e.minY ? Random.Range(e.minY, e.maxY) : (cfg.groundY + cfg.ceilingY) * 0.5f;
  }
}
