using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HangingObstacleBuilder
{
    private const string GroupName = "Hanging Beat Gate";
    private const string Folder = "Assets/PulseLook/HangingObstacle";
    private const string RequestPath = "Temp/PulseRun_AddHanging.request";

    static HangingObstacleBuilder()
    {
        // A one-shot request for this edit, not an automatic scene generator on every launch.
        if (File.Exists(RequestPath)) EditorApplication.update += TryRequestedBuild;
    }

    private static void TryRequestedBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/PulseLook/NeonRunway.unity" &&
            scene.path != "Assets/Scenes/SampleScene.unity") return;
        if (FindRunner(scene) == null) return;
        EditorApplication.update -= TryRequestedBuild;
        if (BuildInScene(scene)) File.Delete(RequestPath);
    }

    [MenuItem("PulseRun/Add Hanging Obstacle")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请先退出 Play 模式，再添加悬挂障碍。");
            return;
        }
        if (BuildInScene(SceneManager.GetActiveScene()) && File.Exists(RequestPath))
            File.Delete(RequestPath);
    }

    private static RunnerController FindRunner(Scene scene)
    {
        RunnerController runner = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            {
                if (runner != null) return null;
                runner = candidate;
            }
        return runner;
    }

    private static bool BuildInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == GroupName)
            {
                Selection.activeGameObject = root;
                Debug.Log("当前场景已有悬挂障碍，已选中，不重复生成。", root);
                return true;
            }
        RunnerController runner = FindRunner(scene);
        if (runner == null || runner.GetComponent<RunnerRespawn>() == null)
        {
            Debug.LogWarning("悬挂障碍需要当前场景有一个 RunnerController 和已有的 RunnerRespawn。");
            return false;
        }

        Vector3 position = runner.transform.position + Vector3.forward * 22f;
        Vector3 forward = Vector3.forward;
        Physics.SyncTransforms();
        if (!Physics.Raycast(position + Vector3.up * 10f, Vector3.down,
            out RaycastHit ground, 30f, runner.groundMask, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("玩家前方22单位没有 Ground 道路，未放置障碍。");
            return false;
        }
        position.y = ground.point.y;
        BoxCollider playerBox = runner.GetComponent<BoxCollider>();
        float playerHeight = playerBox.bounds.size.y;
        float clearance = playerHeight + .35f;
        float depth = 1.1f, width = 6.2f, beamHeight = 1.3f;
        if (runner.jumpHeight <= .35f)
        {
            Debug.LogWarning("当前跳跃高度太低，不能做出跑过安全、起跳危险的悬挂障碍。");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/PulseLook", "HangingObstacle");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return false;
        Material dark = MaterialAsset("GateGraphite", shader, new Color(.06f,.025f,.07f), Color.black);
        Material neon = MaterialAsset("GateMagenta", shader, new Color(.65f,.02f,.15f), new Color(1f,.01f,.22f)*5f);
        Material warning = MaterialAsset("GateWarning", shader, new Color(.75f,.06f,.015f), new Color(1f,.045f,.01f)*3f);

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Hanging Obstacle");
        GameObject group = new GameObject(GroupName);
        SceneManager.MoveGameObjectToScene(group,scene);
        Undo.RegisterCreatedObjectUndo(group,"Create Hanging Gate");
        group.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward,Vector3.up));

        GameObject beam = Box("Dangerous suspended beam | DO NOT JUMP", group.transform,
            new Vector3(0,clearance+beamHeight*.5f,0),new Vector3(width,beamHeight,depth),dark,true);
        beam.AddComponent<RunnerHazard>();
        foreach (float z in new[]{-depth*.5f-.025f,depth*.5f+.025f})
        {
            Box("Bottom magenta edge",group.transform,new Vector3(0,clearance+.025f,z),new Vector3(width,.055f,.045f),neon);
            Box("Top magenta edge",group.transform,new Vector3(0,clearance+beamHeight-.025f,z),new Vector3(width,.055f,.045f),neon);
            for(int side=-1;side<=1;side+=2)
                Box("Vertical edge",group.transform,new Vector3(side*(width*.5f-.03f),clearance+beamHeight*.5f,z),new Vector3(.06f,beamHeight,.045f),neon);
            for(int i=-3;i<=3;i++)
            {
                GameObject slash=Box("Warning slash",group.transform,new Vector3(i*.63f,clearance+beamHeight*.5f,z),new Vector3(.07f,.58f,.03f),warning);
                slash.transform.localRotation=Quaternion.Euler(0,0,-24);
            }
        }
        for(int side=-1;side<=1;side+=2)
        {
            // Decorative supports remain outside the running lane and have no colliders.
            Box("Side support",group.transform,new Vector3(side*3.6f,1.8f,0),new Vector3(.24f,3.6f,.35f),dark);
            Box("Support light",group.transform,new Vector3(side*3.6f,1.8f,-.2f),new Vector3(.065f,3.35f,.045f),neon);
            Box("Upper bracket",group.transform,new Vector3(side*3.3f,3.5f,0),new Vector3(.8f,.2f,.35f),dark);
            Box("Hanger",group.transform,new Vector3(side*2.7f,(3.5f+clearance+beamHeight)*.5f,0),
                new Vector3(.085f,Mathf.Max(.05f,3.5f-clearance-beamHeight),.085f),dark);
        }
        GameObject glow = new GameObject("Magenta road spill");
        glow.transform.SetParent(group.transform,false);glow.transform.localPosition=new Vector3(0,clearance-.15f,0);
        Light light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.03f,.22f);light.intensity=3;light.range=5;light.shadows=LightShadows.None;
        string prefabPath=AssetDatabase.GenerateUniqueAssetPath(Folder+"/HangingBeatGate.prefab");
        PrefabUtility.SaveAsPrefabAsset(group,prefabPath);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject=group;
        Debug.Log($"PULSE_HANGING_BUILT：玩家前方22单位，底部净空{clearance:F2}，方块高度{playerHeight:F2}。正常跑通过，跳跃碰撞复用碎裂复活。请保存场景后测试。",group);
        return true;
    }

    private static Material MaterialAsset(string name,Shader shader,Color color,Color emission)
    {
        string path=Folder+"/"+name+".mat";
        Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing!=null)return existing;
        Material material=new Material(shader){name=name};
        material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",.25f);material.SetFloat("_Smoothness",.55f);
        if(emission.maxColorComponent>0)
        {
            material.SetColor("_EmissionColor",emission);
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
        }
        AssetDatabase.CreateAsset(material,path);return material;
    }

    private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool dangerous=false)
    {
        GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;
        obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=size;
        obj.GetComponent<Renderer>().sharedMaterial=material;
        if(!dangerous)Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }
}
