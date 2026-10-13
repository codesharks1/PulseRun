using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SpikePracticeBuilder
{
    private const string SessionKey = "PulseRun.SpikePracticeBuilt.v1";
    private const string GroupName = "Red Spike Practice";

    static SpikePracticeBuilder()
    {
        if (!SessionState.GetBool(SessionKey, false))
            EditorApplication.update += TryFirstBuild;
    }

    private static void TryFirstBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        string path = SceneManager.GetActiveScene().path;
        if (path != "Assets/PulseLook/NeonRunway.unity" && path != "Assets/Scenes/SampleScene.unity") return;
        EditorApplication.update -= TryFirstBuild;
        Build();
    }

    [MenuItem("PulseRun/Add Red Spike Practice")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        RunnerController runner = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            {
                if (runner != null)
                {
                    Debug.LogWarning("地刺生成需要当前场景中只有一个 RunnerController。");
                    return;
                }
                runner = candidate;
            }
        if (runner == null) { Debug.LogWarning("没有找到 RunnerController。"); return; }
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == GroupName) { SessionState.SetBool(SessionKey, true); return; }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogError("没有找到 URP Lit Shader。"); return; }
        string folder = "Assets/PulseLook";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "PulseLook");
        Material dark = MaterialAsset(folder + "/SpikeDark.mat", shader, new Color(.055f,.018f,.025f), Color.black);
        Material red = MaterialAsset(folder + "/SpikeRed.mat", shader, new Color(.65f,.012f,.015f), new Color(1f,.008f,.012f)*5f);
        Mesh mesh = SpikeMesh();
        string meshPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/SpikePyramid.asset");
        AssetDatabase.CreateAsset(mesh, meshPath);

        Undo.IncrementCurrentGroup();
        int groupId = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Red Spike Practice");
        GameObject group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Create Spike Group");
        SceneManager.MoveGameObjectToScene(group, scene);
        Transform player = runner.transform;
        Vector3 forward = Vector3.forward;
        Vector3 position = player.position + forward * 10f;
        // The generated road's top is y=0; for another scene, probe its Ground surface.
        {
            if (!Physics.Raycast(position + Vector3.up*10f, Vector3.down, out RaycastHit hit,
                30f, runner.groundMask, QueryTriggerInteraction.Ignore))
            {
                Undo.DestroyObjectImmediate(group);
                Debug.LogWarning("玩家前方10单位没有找到 Ground，未放置地刺。");
                return;
            }
            position.y = hit.point.y;
        }
        group.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

        for (int i = 0; i < 5; i++)
        {
            float x = (i-2)*1.05f;
            GameObject spike = new GameObject("Spike " + (i+1));
            spike.transform.SetParent(group.transform, false);
            spike.transform.localPosition = new Vector3(x,0f,0f);
            spike.AddComponent<MeshFilter>().sharedMesh = mesh;
            spike.AddComponent<MeshRenderer>().sharedMaterial = dark;
            MeshCollider collider = spike.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            spike.AddComponent<RunnerHazard>();
            // Three luminous edges around the square base. No decorative colliders.
            Strip(group.transform, red, new Vector3(x, .035f,-.40f), new Vector3(.84f,.045f,.045f));
            Strip(group.transform, red, new Vector3(x-.40f,.035f,0f), new Vector3(.045f,.045f,.84f));
            Strip(group.transform, red, new Vector3(x+.40f,.035f,0f), new Vector3(.045f,.045f,.84f));
            Strip(group.transform, red, new Vector3(x,.035f,.40f), new Vector3(.84f,.045f,.045f));
        }
        GameObject lightObject = new GameObject("Red ground wash");
        lightObject.transform.SetParent(group.transform,false);
        lightObject.transform.localPosition = new Vector3(0f,1.4f,0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f,.015f,.02f);
        light.intensity = 3f; light.range = 5f; light.shadows = LightShadows.None;
        if (runner.GetComponent<RunnerRespawn>() == null)
            Undo.AddComponent<RunnerRespawn>(runner.gameObject);
        Undo.CollapseUndoOperations(groupId);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        SessionState.SetBool(SessionKey,true);
        Selection.activeGameObject = group;
        Debug.Log("红色地刺已放在玩家前方10单位，自动复活已添加。请保存场景并运行测试。",group);
    }

    private static Material MaterialAsset(string path, Shader shader, Color color, Color emission)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader);
        material.SetColor("_BaseColor",color);
        material.SetFloat("_Smoothness",.65f);
        if (emission.maxColorComponent > 0)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor",emission);
        }
        AssetDatabase.CreateAsset(material,path);
        return material;
    }

    private static void Strip(Transform parent, Material material, Vector3 position, Vector3 size)
    {
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "Red base strip";
        strip.transform.SetParent(parent,false);
        strip.transform.localPosition = position;
        strip.transform.localScale = size;
        Object.DestroyImmediate(strip.GetComponent<Collider>());
        strip.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Mesh SpikeMesh()
    {
        Vector3[] corners = { new Vector3(-.42f,0,-.42f), new Vector3(.42f,0,-.42f),
            new Vector3(.42f,0,.42f),new Vector3(-.42f,0,.42f) };
        Vector3[] vertices = new Vector3[18];
        int[] triangles = new int[18];
        int index = 0;
        for (int i=0;i<4;i++)
        {
            vertices[index++] = corners[i];
            vertices[index++] = new Vector3(0,.9f,0);
            vertices[index++] = corners[(i+1)%4];
        }
        vertices[index++] = corners[0]; vertices[index++] = corners[1]; vertices[index++] = corners[2];
        vertices[index++] = corners[0]; vertices[index++] = corners[2]; vertices[index++] = corners[3];
        for(int i=0;i<triangles.Length;i++) triangles[i]=i;
        Mesh mesh = new Mesh { name = "Spike pyramid 0.9" };
        mesh.vertices=vertices; mesh.triangles=triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}
