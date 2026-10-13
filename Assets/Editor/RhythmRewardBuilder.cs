using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RhythmRewardBuilder
{
    const string NoteName="Grounded Beat Note";
    const string Folder="Assets/PulseLook/RhythmReward";
    const string Request="Temp/PulseRun_RhythmRewards.request";
    static RhythmRewardBuilder(){EditorApplication.update+=TryRequested;}
    static void TryRequested()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/PulseLook/NeonRunway.unity" || scene.isDirty)return;
        if(UpgradeScene())
        {EditorSceneManager.SaveScene(scene);File.Delete(Request);}
    }
    [MenuItem("PulseRun/Add Grounded Note Rewards")]
    public static void UpgradeFromMenu()
    {
        if(UpgradeScene() && File.Exists(Request))File.Delete(Request);
    }
    static bool UpgradeScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {Debug.LogWarning("请先退出 Play，再添加音符奖励。");return false;}
        Scene scene=SceneManager.GetActiveScene();RunnerController runner=null;
        foreach(GameObject root in scene.GetRootGameObjects())
            foreach(RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            {if(runner!=null){Debug.LogError("需要当前场景只有一个玩家。");return false;}runner=candidate;}
        if(runner==null)return false;
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Add Note Rewards and Remove Gate Labels");
        int notes=0,labels=0;
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            if(root.name=="Hanging Beat Gate")
            {
                bool existed=root.transform.Find(NoteName)!=null;
                Transform note=Attach(root.transform,runner);
                if(!existed){Undo.RegisterCreatedObjectUndo(note.gameObject,"Add practice note");notes++;}
            }
            if(root.name!="Generated Rhythm Obstacles")continue;
            foreach(Transform obstacle in root.transform)
            {
                if(obstacle.name.StartsWith("StayGrounded @"))
                {
                    bool existed=obstacle.Find(NoteName)!=null;
                    Transform note=Attach(obstacle,runner);
                    if(!existed){Undo.RegisterCreatedObjectUndo(note.gameObject,"Add chart note");notes++;}
                }
                if(obstacle.name.StartsWith("X BOOST @") || obstacle.name.StartsWith("C NORMAL @"))
                    labels+=SpeedGateBuilder.RemoveGateLabels(obstacle);
            }
        }
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        Debug.Log($"已添加 {notes} 个落地音符，隐藏 {labels} 个光门标牌。位置、谱面和加速逻辑未改变。请保存场景并试玩。");
        return true;
    }

    public static Transform Attach(Transform obstacle,RunnerController runner)
    {
        Transform existing=obstacle.Find(NoteName);
        if(existing!=null)
        {
            RhythmNotePickup pickup=existing.GetComponent<RhythmNotePickup>();
            if(pickup!=null){Undo.RecordObject(pickup,"Bind note runner");pickup.runner=runner;}
            return existing;
        }
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/PulseLook","RhythmReward");
        Material light=MaterialAsset("Note Gold",new Color(1.8f,1.05f,.15f));
        GameObject note=new GameObject(NoteName);note.transform.SetParent(obstacle,false);
        // 脚本在无缩放的容器上，所有造型无碰撞；触发器位于横梁下的安全通道。
        BoxCollider trigger=note.AddComponent<BoxCollider>();trigger.isTrigger=true;
        trigger.center=new Vector3(0,.5f,0);trigger.size=new Vector3(.95f,.7f,.12f);
        AudioSource source=note.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0f;
        RhythmNotePickup reward=note.AddComponent<RhythmNotePickup>();reward.runner=runner;
        Transform visual=new GameObject("Floating double note").transform;visual.SetParent(note.transform,false);
        visual.localPosition=new Vector3(0,.5f,0);reward.noteVisual=visual;
        foreach(float side in new[]{-1f,1f})
        {
            Shape(visual,"Note head",PrimitiveType.Sphere,new Vector3(side*.15f,-.13f,0),new Vector3(.22f,.14f,.13f),light);
            Shape(visual,"Note stem",PrimitiveType.Cube,new Vector3(side*.15f+.07f,.055f,0),new Vector3(.05f,.38f,.065f),light);
        }
        Shape(visual,"Note beam",PrimitiveType.Cube,new Vector3(.07f,.23f,0),new Vector3(.35f,.085f,.065f),light);
        Transform burst=new GameObject("Pickup sparkles").transform;burst.SetParent(note.transform,false);
        burst.localPosition=new Vector3(0,.5f,0);reward.burstVisual=burst;
        for(int i=0;i<8;i++)
            Shape(burst,"Spark",PrimitiveType.Cube,Vector3.zero,Vector3.one*.085f,light);
        burst.gameObject.SetActive(false);
        return note.transform;
    }
    static Material MaterialAsset(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",new Color(.85f,.6f,.09f));material.SetFloat("_Smoothness",.65f);
        material.SetColor("_EmissionColor",color);material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        AssetDatabase.CreateAsset(material,path);return material;
    }
    static void Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        GameObject shape=GameObject.CreatePrimitive(type);shape.name=name;shape.transform.SetParent(parent,false);
        shape.transform.localPosition=position;shape.transform.localScale=scale;
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        Renderer renderer=shape.GetComponent<Renderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
}
