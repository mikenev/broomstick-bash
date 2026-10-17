using UnityEngine;
namespace BroomstickBash {
  /// <summary>Scene-assigned shared assets (meshes/materials built by the editor setup script).
  /// Runtime code builds objects from these instead of GameObject.CreatePrimitive (WebGL stripping-safe).</summary>
  public class GameConfig : MonoBehaviour {
    public static GameConfig I;
    public Mesh cube, sphere, cylinder, capsule;
    public Material kidMat, carMat, branchMat, batMat, ghostMat, bossMat, fireMat, spellMat, sparkleMat, houseMat, groundMat;
    [Header("World")] public float groundY = 0f, ceilingY = 10f, scrollSpeed = 6f;
    void Awake(){ I = this; }

    public GameObject MakeBlock(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Transform parent = null){
      var go = new GameObject(name);
      if (parent) go.transform.SetParent(parent, false);
      go.transform.position = pos; go.transform.localScale = scale;
      go.AddComponent<MeshFilter>().sharedMesh = mesh;
      go.AddComponent<MeshRenderer>().sharedMaterial = mat;
      return go;
    }
  }
}
