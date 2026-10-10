using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PulsePreviewCapture
{
    static PulsePreviewCapture() { EditorApplication.delayCall += Auto; }
    static void Auto()
    {
        if (!File.Exists("PulsePreview/NeonRunway-v4.png") && !SessionState.GetBool("PulsePreviewV4",false) && File.Exists("Assets/PulseLook/NeonRunway.unity"))
        { SessionState.SetBool("PulsePreviewV4",true); EditorApplication.delayCall += Capture; }
    }
    [MenuItem("PulseRun/Capture Scene Preview")]
    public static void Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prev=SceneManager.GetActiveScene();
        var scene=SceneManager.GetSceneByPath("Assets/PulseLook/NeonRunway.unity");
        bool opened=!scene.IsValid() || !scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene("Assets/PulseLook/NeonRunway.unity",OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        Camera cam=null;
        foreach(var root in scene.GetRootGameObjects()) {cam=root.GetComponentInChildren<Camera>();if(cam)break;}
        Polish(scene,cam);
        var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);rt.Create();
        cam.aspect=1600f/900;
        bool asyncCompile=UnityEditor.ShaderUtil.allowAsyncCompilation;
        UnityEditor.ShaderUtil.allowAsyncCompilation=false;
        EditorApplication.delayCall += () =>
        {
            try
            {
                RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest { destination=rt });
                var old=RenderTexture.active;RenderTexture.active=rt;
                var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();RenderTexture.active=old;
                Directory.CreateDirectory("PulsePreview");File.WriteAllBytes("PulsePreview/NeonRunway-v4.png",tex.EncodeToPNG());
                Object.DestroyImmediate(tex);Debug.Log("PulseRun preview captured successfully");
            }
            finally {UnityEditor.ShaderUtil.allowAsyncCompilation=asyncCompile;rt.Release();Object.DestroyImmediate(rt);if(opened)EditorSceneManager.CloseScene(scene,true);if(prev.IsValid())SceneManager.SetActiveScene(prev);}
        };
    }
    static void Polish(Scene scene,Camera cam)
    {
        foreach(string name in new[]{"Cyan Light","Pink Light","Player Core"})
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/PulseLook/"+name+".mat");
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            m.EnableKeyword("_EMISSION");EditorUtility.SetDirty(m);
        }
        var volume=cam.transform.parent.GetComponentInChildren<Volume>();
        if(!AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/PulseLook/NeonLookFixed.asset"))
        {
            var p=ScriptableObject.CreateInstance<VolumeProfile>();
            var b=p.Add<Bloom>(true);b.intensity.Override(1.1f);b.threshold.Override(.9f);b.scatter.Override(.72f);
            var t=p.Add<Tonemapping>(true);t.mode.Override(TonemappingMode.ACES);
            var v=p.Add<Vignette>(true);v.intensity.Override(.2f);
            AssetDatabase.CreateAsset(p,"Assets/PulseLook/NeonLookFixed.asset");
            foreach(var c in p.components)AssetDatabase.AddObjectToAsset(c,p);
        }
        volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/PulseLook/NeonLookFixed.asset");volume.priority=10;
        cam.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        if(!cam.transform.parent.Find("Atmosphere cards"))
        {
            var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float u=x/127f,v=y/127f;float r=Mathf.Pow(Mathf.Clamp01(1-((u-.5f)*(u-.5f)+(v-.5f)*(v-.5f))*4),2);
                float n=Mathf.PerlinNoise(u*6+17,v*6+29)*.65f+Mathf.PerlinNoise(u*15,v*15)*.35f;
                tex.SetPixel(x,y,new Color(1,1,1,r*n*.38f));
            }
            tex.Apply();tex.wrapMode=TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(tex,"Assets/PulseLook/MistTexture.asset");
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",new Color(.19f,.28f,.43f,.65f));
            mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
            AssetDatabase.CreateAsset(mat,"Assets/PulseLook/Mist.mat");
            var group=new GameObject("Atmosphere cards");group.transform.parent=cam.transform.parent;
            var random=new System.Random(123);
            for(int i=0;i<32;i++)
            {
                var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name="Soft mist "+i;g.transform.parent=group.transform;
                g.transform.position=new Vector3((i%2==0?-1:1)*(10+(float)random.NextDouble()*22),-5+(float)random.NextDouble()*5,8+(float)random.NextDouble()*125);
                g.transform.rotation=cam.transform.rotation;g.transform.localScale=new Vector3(30,13,1);
                g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());
            }
        }
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
    }
}
