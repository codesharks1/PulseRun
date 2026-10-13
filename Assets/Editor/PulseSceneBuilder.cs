using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Generates a separate, editable scene. Existing scenes and project settings are untouched.
[InitializeOnLoad]
public static class PulseSceneBuilder
{
    const string Root = "Assets/PulseLook";
    const string ScenePath = Root + "/NeonRunway.unity";
    static Material road, dark, cyan, pink, core;
    static Transform parent;
    static PulseSceneBuilder() { EditorApplication.delayCall += FirstBuild; }
    static void FirstBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
        Build();
    }

    [MenuItem("PulseRun/Build New Neon Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        parent = new GameObject("NEON RUNWAY - editable scene objects").transform;
        road = Mat("Road", new Color(.025f,.04f,.065f), Color.black, .7f);
        dark = Mat("Architecture", new Color(.018f,.028f,.05f), Color.black, .35f);
        cyan = Mat("Cyan Light", new Color(.02f,.55f,.8f), new Color(.02f,.8f,1f)*5f, .6f);
        pink = Mat("Pink Light", new Color(.65f,.03f,.4f), new Color(1f,.04f,.65f)*4f, .5f);
        core = Mat("Player Core", new Color(.035f,.3f,.48f), new Color(.015f,.18f,.28f), .85f);

        // Straight centerline; runtime movement is always along world +Z.
        for (int i=-12;i<150;i++)
        {
            Vector3 a=Path(i), b=Path(i+1), forward=(b-a).normalized;
            var q=Quaternion.LookRotation(forward);
            var right=q*Vector3.right;
            Box("Road segment "+i,(a+b)*.5f+Vector3.down*.25f,new Vector3(7,.5f,1.035f),q,road);
            foreach (float side in new[]{-1f,1f})
                Box("Cyan edge",(a+b)*.5f+right*(side*3.44f)+Vector3.up*.035f,new Vector3(.075f,.055f,1.04f),q,cyan);
            if(i%6==0) Box("Beat marker",a+right*2.4f+Vector3.up*.035f,new Vector3(.06f,.035f,.9f),q,pink);
        }
        for(int i=0;i<5;i++)
        {
            float s=15+i*24;
            var p=Path(s); var q=Quaternion.LookRotation(Path(s+.1f)-p);
            var r=q*Vector3.right;
            if(i==0)
            {
                Box("Jump obstacle",p+Vector3.up*.65f,new Vector3(4,1.3f,.7f),q,dark);
                Box("Obstacle luminous face",p+Vector3.up*.72f-q*Vector3.forward*.36f,new Vector3(3.7f,.09f,.035f),q,pink);
            }
            else
            {
                foreach(float side in new[]{-1f,1f})
                {
                    Box("Gate pillar",p+r*(side*2.8f)+Vector3.up*3.7f,new Vector3(.45f,7.4f,.55f),q,dark);
                    Box("Gate light",p+r*(side*2.54f)+Vector3.up*3.7f,new Vector3(.045f,7.4f,.6f),q,pink);
                }
                Box("Gate lintel",p+Vector3.up*7.3f,new Vector3(6,.35f,.55f),q,dark);
            }
            LightAt("Pink road wash",p+Vector3.up*2,new Color(1,.03f,.5f),9,10);
        }
        var rng = new System.Random(72);
        for(int i=0;i<80;i++)
        {
            float s=-10+(float)rng.NextDouble()*185;
            var p=Path(s); var q=Quaternion.LookRotation(Path(s+.1f)-p);
            float side=i%2==0?-1:1;
            p+=q*Vector3.right*(side*(9+(float)rng.NextDouble()*34));
            float h=12+(float)rng.NextDouble()*35;
            p.y=-15+h*.5f;
            float w=1.5f+(float)rng.NextDouble()*3;
            Box("Background monolith "+i,p,new Vector3(w,h,w),Quaternion.identity,dark);
            if(i%3==0) Box("Monolith light",p+new Vector3(0,h*.2f,-w*.5f-.025f),new Vector3(.055f,h*.48f,.04f),Quaternion.identity,cyan);
        }
        var player=new GameObject("Player display cube (not controller)"); player.transform.parent=parent;
        player.transform.position=new Vector3(-.4f,1.15f,4);
        player.transform.rotation=Quaternion.Euler(12,20,-16);
        var c=Box("Core",Vector3.zero,Vector3.one,Quaternion.identity,core); c.transform.SetParent(player.transform,false);
        for(int axis=0;axis<3;axis++) for(int a=-1;a<=1;a+=2) for(int b=-1;b<=1;b+=2)
        {
            var pos=Vector3.zero; var size=Vector3.one*.045f;
            size[axis]=1.06f; pos[(axis+1)%3]=a*.51f; pos[(axis+2)%3]=b*.51f;
            var e=Box("Luminous cube edge",pos,size,Quaternion.identity,cyan);e.transform.SetParent(player.transform,false);
        }
        for(int i=0;i<12;i++) Box("Trail fragment",new Vector3(-.4f+Mathf.Sin(i*3)*.22f,.25f+i*.035f,3.3f-i*.38f),Vector3.one*(.04f+i*.006f),Quaternion.Euler(i*12,20,30),cyan);
        LightAt("Player cyan pool",new Vector3(-.4f,1.4f,4),new Color(.02f,.65f,1),10,8);
        var sun=new GameObject("Soft blue key").AddComponent<Light>();sun.transform.parent=parent;sun.type=LightType.Directional;sun.color=new Color(.4f,.57f,1);sun.intensity=.65f;sun.transform.rotation=Quaternion.Euler(35,-35,0);sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.11f,.16f,.25f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.065f,.10f,.18f);RenderSettings.fogDensity=.012f;

        var volume=new GameObject("Global Volume - Bloom and grading").AddComponent<Volume>();volume.transform.parent=parent;volume.isGlobal=true;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.85f);bloom.threshold.Override(1);bloom.scatter.Override(.65f);
        var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
        var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.23f);
        AssetDatabase.CreateAsset(profile,AssetDatabase.GenerateUniqueAssetPath(Root+"/Look.asset"));
        foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
        volume.sharedProfile=profile;

        var cam=new GameObject("Main Camera - side rear composition").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.parent=parent;
        cam.transform.position=new Vector3(9,5.5f,-8);cam.transform.LookAt(new Vector3(-.5f,1.7f,15));cam.fieldOfView=58;cam.nearClipPlane=.1f;cam.farClipPlane=240;cam.allowHDR=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=RenderSettings.fogColor;
        cam.gameObject.AddComponent<AudioListener>();cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var path=AssetDatabase.GenerateUniqueAssetPath(ScenePath);
        EditorSceneManager.SaveScene(scene,path);
        // Render a review image without changing any existing cameras or scenes.
        var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGBHalf);cam.targetTexture=rt;
        cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
        Directory.CreateDirectory("PulsePreview");File.WriteAllBytes("PulsePreview/NeonRunway.png",tex.EncodeToPNG());
        RenderTexture.active=old;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
        EditorSceneManager.CloseScene(scene,true);if(previous.IsValid())SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();Debug.Log("PulseRun scene created: "+path);
    }
    static Vector3 Path(float s) { return new Vector3(0,0,s); }
    static Material Mat(string name,Color color,Color emission,float smooth)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",.25f);
        if(emission.maxColorComponent>0){m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}
        AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Root+"/"+name+".mat"));return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Quaternion q,Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.SetPositionAndRotation(p,q);g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
        Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static void LightAt(string name,Vector3 p,Color color,float intensity,float range)
    {
        var l=new GameObject(name).AddComponent<Light>();l.transform.parent=parent;l.transform.position=p;l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
    }
}
