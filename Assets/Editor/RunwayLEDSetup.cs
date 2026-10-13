using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RunwayLEDSetup
{
    const string Folder="Assets/PulseLook/ReactiveLED";
    const string RootName="Symmetric Reactive LED Strips";
    const string Request="Temp/PulseRun_ReactiveLED.request";
    static RunwayLEDSetup(){EditorApplication.update+=TryRequested;}
    static void TryRequested()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/PulseLook/NeonRunway.unity" || scene.isDirty)return;
        if(AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/PassedLED.shader")==null)return;
        if(Build())EditorSceneManager.SaveScene(scene);
        File.Delete(Request);
    }
    [MenuItem("PulseRun/Setup Symmetric Reactive LEDs")]
    public static void BuildFromMenu(){Build();if(File.Exists(Request))File.Delete(Request);}

    public static bool Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return false;
        Scene scene=SceneManager.GetActiveScene();RunnerController runner=null;GameObject straight=null;
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            if(root.name==RootName)
            {
                RunwayLightTrail existing=root.GetComponent<RunwayLightTrail>();
                if(existing==null){Debug.LogError("灯带对象缺少 RunwayLightTrail 组件。");return false;}
                Undo.RecordObject(existing,"Increase LED density");
                existing.spacing=4f;
                UpdateMaterialSpacing(AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Reactive LED.mat"));
                EditorUtility.SetDirty(existing);EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject=root;
                Debug.Log("灯带间距已缩小到 4，密度增加一倍。",root);
                return true;
            }
            if(root.name=="Straight Neon Runway")straight=root;
            foreach(RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            {if(runner!=null){Debug.LogError("需要当前场景只有一个玩家。");return false;}runner=candidate;}
        }
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/PassedLED.shader");
        Transform ground=straight!=null?straight.transform.Find("Straight Ground - seamless collider"):null;
        if(shader==null || runner==null || ground==null || ground.GetComponent<BoxCollider>()==null)
        {Debug.LogError("请打开直线 NeonRunway 场景，等待 LED 着色器导入。");return false;}
        string materialPath=Folder+"/Reactive LED.mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        UpdateMaterialSpacing(material);
        Bounds bounds=ground.GetComponent<BoxCollider>().bounds;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Symmetric Reactive LEDs");
        GameObject group=new GameObject(RootName);SceneManager.MoveGameObjectToScene(group,scene);
        Undo.RegisterCreatedObjectUndo(group,"Create LED strips");
        RunwayLightTrail trail=group.AddComponent<RunwayLightTrail>();trail.runner=runner;
        trail.markerOrigin=runner.transform.position.z;trail.strips=new Renderer[2];
        int index=0;
        foreach(float side in new[]{-1f,1f})
        {
            GameObject strip=GameObject.CreatePrimitive(PrimitiveType.Cube);strip.name=side<0?"Left LED segments":"Right LED segments";
            strip.transform.SetParent(group.transform,false);
            strip.transform.position=new Vector3(bounds.center.x+side*3.1f,bounds.max.y+.035f,bounds.center.z);
            strip.transform.localScale=new Vector3(.065f,.035f,bounds.size.z);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            Renderer renderer=strip.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;trail.strips[index++]=renderer;
        }
        // 原来的单侧粉色短灯条保留但隐藏，不动青色安全边线。
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            if(root.name!="Straight Neon Runway" && root.name!="NEON RUNWAY - editable scene objects")continue;
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
                if(child.name=="Beat marker")
                {Undo.RecordObject(child.gameObject,"Hide old one-sided markers");child.gameObject.SetActive(false);}
        }
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=group;
        Debug.Log("对称灯带已创建：两侧同位置、同间距；跑过后粉紫变暖金，重生重置。原青色边线保留。",group);
        return true;
    }

    static void UpdateMaterialSpacing(Material material)
    {
        if(material==null)return;
        Undo.RecordObject(material,"Increase LED density");
        material.SetFloat("_Period",4f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }
}
