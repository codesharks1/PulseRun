using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BerserkVisualSetup
{
    const string Folder="Assets/PulseLook/Berserk";
    const string Request="Temp/PulseRun_PurpleBerserk.request";
    static BerserkVisualSetup(){EditorApplication.update+=TryRequested;}
    static void TryRequested()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/PulseLook/NeonRunway.unity" || scene.isDirty)return;
        if(Setup()){EditorSceneManager.SaveScene(scene);File.Delete(Request);}
    }
    [MenuItem("PulseRun/Enable Purple Berserk Mode")]
    public static void SetupFromMenu(){if(Setup() && File.Exists(Request))File.Delete(Request);}
    static bool Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("请先退出 Play。");return false;}
        RunnerController runner=null;Scene scene=SceneManager.GetActiveScene();
        foreach(GameObject root in scene.GetRootGameObjects())
            foreach(RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            {if(runner!=null){Debug.LogError("需要一个玩家。");return false;}runner=candidate;}
        if(runner==null || runner.GetComponent<RunnerRhythmMotion>()==null)
        {Debug.LogWarning("请打开已有 X/C 加速谱面的场景；不需要重新录制。");return false;}
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Purple Berserk Visuals");
        if(!Configure(runner))return false;
        Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=runner.gameObject;
        Debug.Log("紫色狂暴表现已启用：方块、粒子、电弧和轻紫镜头滤镜跟随 X/C；死亡和重试清理，不改变谱面或跳跃手感。");return true;
    }
    public static bool Configure(RunnerController runner)
    {
        Shader arcShader=Shader.Find("Universal Render Pipeline/Unlit");
        Shader jetShader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if(arcShader==null || jetShader==null){Debug.LogError("缺少 URP 电弧或粒子着色器。");return false;}
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/PulseLook","Berserk");
        Material arc=MaterialAsset("Purple Electricity",arcShader,new Color(1.8f,.25f,3f,1f));
        Material jet=MaterialAsset("Purple Cube Jet",jetShader,new Color(2f,.55f,3f,1f));
        RunnerBerserkVisuals effects=runner.GetComponent<RunnerBerserkVisuals>();
        if(effects==null)effects=Undo.AddComponent<RunnerBerserkVisuals>(runner.gameObject);
        Undo.RecordObject(effects,"Bind Berserk Materials");effects.arcMaterial=arc;effects.jetMaterial=jet;
        EditorUtility.SetDirty(effects);AssetDatabase.SaveAssets();return true;
    }
    static Material MaterialAsset(string name,Shader shader,Color color)
    {
        string path=Folder+"/"+name+".mat";Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing!=null)return existing;
        var material=new Material(shader){name=name};material.SetColor("_BaseColor",color);
        material.SetFloat("_Surface",1f);material.SetFloat("_Blend",0f);material.SetFloat("_ZWrite",0f);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_Cull",(float)CullMode.Off);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
        AssetDatabase.CreateAsset(material,path);return material;
    }
}
