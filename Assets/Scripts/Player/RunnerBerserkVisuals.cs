using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 只跟随谱面中的加速状态做表现，不修改速度、重力或碰撞。
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(RunnerController),typeof(RunnerRhythmMotion))]
public class RunnerBerserkVisuals : MonoBehaviour
{
    [ColorUsage(false,true)] public Color berserkColor=new Color(.65f,.12f,1f);
    [Range(0f,1f)] public float cameraTint=.65f;
    [Range(1f,4f)] public float trailMultiplier=2.2f;
    public bool enableLightning=true;
    public bool enableJet=true;
    [Header("向后拉出的尖锐闪电与镜头能量边缘")]
    [Tooltip("控制贴身电弧间隙，不再向四周扩散成圆环。")]
    [Range(.7f,1.6f)] public float lightningRadius=1.1f;
    [Range(.015f,.06f)] public float lightningWidth=.03f;
    [Tooltip("总粗细倍率，2表示比上一版加粗一倍。")]
    [Range(.5f,4f)] public float lightningWidthMultiplier=2f;
    [Range(1.3f,3f)] public float lightningTailLength=2.2f;
    [Range(6f,24f)] public float lightningActivity=16f;
    [Tooltip("电弧每次出现的寿命（秒）：X最短，Y最长。")]
    public Vector2 lightningLifetimeSeconds=new Vector2(.08f,.20f);
    [Tooltip("消失后等待多久再出现（秒）：X最短，Y最长。")]
    public Vector2 lightningGapSeconds=new Vector2(.04f,.16f);
    [Range(0f,1f)] public float screenEnergy=.8f;
    public Material arcMaterial;
    public Material jetMaterial;

    class MaterialState
    {
        public Material material;
        public Color baseColor,emission;
        public bool hasBase,hasColor,hasEmission;
    }
    class RendererState
    {
        public Renderer renderer;
        public Material[] original,local;
    }
    readonly List<MaterialState> materials=new List<MaterialState>();
    readonly List<RendererState> renderers=new List<RendererState>();
    readonly List<Light> lights=new List<Light>();
    readonly List<Color> lightColors=new List<Color>();
    RunnerController runner;
    RunnerRhythmMotion motion;
    RunMusicClock clock;
    ParticleSystem friction,jet;
    ParticleSystem.MinMaxGradient originalParticleColor;
    float originalEmission,originalSize;
    Color originalPulse;
    ParticleSystem.Particle[] liveParticles;
    GameObject effectRoot;
    LineRenderer[] arcs;
    float[] arcNextChange,arcEnergy;
    float[] arcBornAgainAt,arcDiesAt;
    bool[] arcAlive;
    int[] arcRoute;
    readonly System.Random arcRandom=new System.Random();
    Material localArc;
    Mesh particleMesh;
    Volume tintVolume;
    VolumeProfile tintProfile;
    ColorAdjustments grading;
    Camera gameCamera;
    NeoCitySequence citySequence;
    Texture2D edgeGlow,streakTexture;
    float entryPulse;
    float blend,enteredAt;
    bool wasBoosting,initialized;

    // +Z是前进方向：每条闪电从方块前侧掠过边缘，再沿-Z被甩向后方。
    // 使用少量明确折点，保留尖锐拐角，不使用绕屏圆环或径向支路。
    static readonly Vector3[,] ElectricPaths={
        {new Vector3(-.58f,.30f,.48f),new Vector3(-.63f,.34f,.12f),new Vector3(-.58f,.28f,-.15f),new Vector3(-.78f,.18f,-.38f),
            new Vector3(-.64f,.08f,-.68f),new Vector3(-.78f,-.05f,-1.05f),new Vector3(-.62f,-.12f,-1.45f),new Vector3(-.69f,-.22f,-2.05f)},
        {new Vector3(.58f,.35f,.55f),new Vector3(.62f,.42f,.16f),new Vector3(.57f,.28f,-.08f),new Vector3(.77f,.25f,-.33f),
            new Vector3(.63f,.12f,-.65f),new Vector3(.76f,.02f,-1.01f),new Vector3(.64f,-.12f,-1.52f),new Vector3(.68f,-.16f,-2.24f)},
        {new Vector3(-.12f,.56f,.46f),new Vector3(-.30f,.65f,.14f),new Vector3(-.05f,.56f,-.15f),new Vector3(-.28f,.66f,-.42f),
            new Vector3(-.02f,.54f,-.66f),new Vector3(-.19f,.50f,-1.05f),new Vector3(-.05f,.37f,-1.60f),new Vector3(-.12f,.22f,-2.15f)},
        {new Vector3(.35f,.55f,.42f),new Vector3(.50f,.65f,.08f),new Vector3(.38f,.55f,-.12f),new Vector3(.51f,.57f,-.44f),
            new Vector3(.38f,.45f,-.71f),new Vector3(.50f,.42f,-1.18f),new Vector3(.33f,.24f,-1.65f),new Vector3(.35f,.12f,-2.32f)}
    };

    void Awake()
    {
        runner=GetComponent<RunnerController>();motion=GetComponent<RunnerRhythmMotion>();clock=runner.musicClock;
        foreach(CameraFollow follow in Object.FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            if(follow.target==transform){gameCamera=follow.GetComponent<Camera>();break;}
        foreach(NeoCitySequence city in Object.FindObjectsByType<NeoCitySequence>(FindObjectsSortMode.None))
            if(city.runner==runner){citySequence=city;break;}
        originalPulse=runner.jumpPulseColor;
        var copies=new Dictionary<Material,Material>();
        if(runner.visual!=null)
            foreach(Renderer renderer in runner.visual.GetComponentsInChildren<Renderer>(true))CacheRenderer(renderer,copies);
        friction=runner.frictionParticles;
        if(friction!=null)
        {
            CacheRenderer(friction.GetComponent<Renderer>(),copies);
            var main=friction.main;var emission=friction.emission;
            originalParticleColor=main.startColor;originalSize=main.startSizeMultiplier;
            originalEmission=emission.rateOverTimeMultiplier;
            liveParticles=new ParticleSystem.Particle[main.maxParticles];
        }
        foreach(Light light in GetComponentsInChildren<Light>(true))
        {lights.Add(light);lightColors.Add(light.color);}
        CreateEffects();CreateCameraFilter();CreateScreenTextures();initialized=true;
        ResetEffects();
    }
    void CacheRenderer(Renderer renderer,Dictionary<Material,Material> copies)
    {
        if(renderer==null || renderers.Exists(state=>state.renderer==renderer))return;
        var state=new RendererState{renderer=renderer,original=renderer.sharedMaterials};
        state.local=(Material[])state.original.Clone();
        for(int i=0;i<state.local.Length;i++)
        {
            Material source=state.original[i];if(source==null)continue;
            if(!copies.TryGetValue(source,out Material copy))
            {
                copy=new Material(source){name=source.name+" - player berserk instance",hideFlags=HideFlags.DontSave};
                copies.Add(source,copy);
                var item=new MaterialState{material=copy,hasBase=copy.HasProperty("_BaseColor"),hasColor=copy.HasProperty("_Color"),hasEmission=copy.HasProperty("_EmissionColor")};
                item.baseColor=item.hasBase?copy.GetColor("_BaseColor"):(item.hasColor?copy.GetColor("_Color"):Color.white);
                item.emission=item.hasEmission?copy.GetColor("_EmissionColor"):Color.black;
                materials.Add(item);
            }
            state.local[i]=copy;
        }
        renderer.sharedMaterials=state.local;renderers.Add(state);
    }
    void OnEnable()
    {
        if(!initialized)return;
        foreach(RendererState state in renderers)if(state.renderer!=null)state.renderer.sharedMaterials=state.local;
        if(clock!=null)clock.RunStopped+=ResetEffects;
        ResetEffects();
    }
    void LateUpdate()
    {
        if(!initialized)return;
        bool playing=runner.isActiveAndEnabled && clock!=null && clock.IsRunning;
        if(!playing){if(blend>0f || wasBoosting)ResetEffects();return;}
        bool boosting=motion.isActiveAndEnabled && RunChartMotion.ScaleAt(motion.segments,(float)clock.SongTime)>1.001f;
        if(!boosting && !wasBoosting && blend<=0f)return;
        if(boosting && !wasBoosting)
        {
            enteredAt=Time.time;
            BeginArcCycles();
            if(enableJet && jet!=null){jet.Play(false);EmitEntrySparks();}
        }
        if(!boosting && wasBoosting && jet!=null)jet.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        wasBoosting=boosting;
        blend=Mathf.MoveTowards(blend,boosting?1f:0f,Time.deltaTime/(boosting?.12f:.32f));
        float pulse=boosting?Mathf.Pow(1f-Mathf.Clamp01((Time.time-enteredAt)/.38f),2f):0f;
        entryPulse=pulse;
        ApplyColors(blend);ApplyTrail(blend);
        if(tintVolume!=null)
        {
            tintVolume.weight=blend*cameraTint;tintVolume.enabled=blend>0f;
            grading.postExposure.Override(.04f+pulse*.12f);
        }
        if(localArc!=null)
        {
            Color color=berserkColor*(3.2f+pulse*1.3f);color.a=blend*.9f;
            localArc.SetColor("_BaseColor",color);
            if(enableLightning)UpdateArcs();
            for(int i=0;i<arcs.Length;i++)
            {
                arcs[i].enabled=enableLightning && blend>.03f && arcAlive[i];
                float crackle=arcEnergy[i];
                arcs[i].widthMultiplier=(Mathf.Clamp(lightningWidth*.55f,.008f,.022f)*(i<4?1f:.5f)*crackle+pulse*.004f)*lightningWidthMultiplier;
            }
        }
        if(jet!=null)
        {
            if(boosting && enableJet && !jet.isPlaying)jet.Play(false);
            var emission=jet.emission;emission.rateOverTime=boosting && enableJet?75f:0f;
            if(!enableJet && jet.isPlaying)jet.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    void ApplyColors(float amount)
    {
        foreach(MaterialState state in materials)
        {
            Color target=berserkColor*Mathf.Max(.35f,state.baseColor.maxColorComponent);target.a=state.baseColor.a;
            Color color=Color.Lerp(state.baseColor,target,amount);
            if(state.hasBase)state.material.SetColor("_BaseColor",color);
            if(state.hasColor)state.material.SetColor("_Color",color);
            if(state.hasEmission)
            {
                Color emission=berserkColor*Mathf.Max(.45f,state.emission.maxColorComponent*1.1f);emission.a=state.emission.a;
                state.material.SetColor("_EmissionColor",Color.Lerp(state.emission,emission,amount));
            }
        }
        for(int i=0;i<lights.Count;i++)if(lights[i]!=null)lights[i].color=Color.Lerp(lightColors[i],berserkColor,amount);
        if(runner!=null)runner.jumpPulseColor=Color.Lerp(originalPulse,berserkColor,amount);
    }
    void ApplyTrail(float amount)
    {
        if(friction==null)return;
        var main=friction.main;var emission=friction.emission;
        Color color=Color.Lerp(originalParticleColor.color,new Color(.78f,.46f,1f,1f),amount);
        main.startColor=amount<=0f?originalParticleColor:new ParticleSystem.MinMaxGradient(color);
        main.startSizeMultiplier=originalSize*Mathf.Lerp(1f,1.15f,amount);
        emission.rateOverTimeMultiplier=originalEmission*Mathf.Lerp(1f,trailMultiplier,amount);
        // 已经发射的蓝色碎块也一起变色，不必等待它们消失。
        int count=friction.GetParticles(liveParticles);
        for(int i=0;i<count;i++)liveParticles[i].startColor=color;
        friction.SetParticles(liveParticles,count);
        // 不修改emission.enabled：空中/死亡是否发射仍由RunnerController负责。
    }
    void CreateCameraFilter()
    {
        tintVolume=gameObject.AddComponent<Volume>();tintVolume.isGlobal=true;tintVolume.priority=35f;tintVolume.weight=0f;
        tintVolume.hideFlags=HideFlags.DontSave;
        tintProfile=ScriptableObject.CreateInstance<VolumeProfile>();tintProfile.hideFlags=HideFlags.DontSave;
        // 仅覆盖需要的字段，避免覆盖隧道转场和原有Bloom。
        grading=tintProfile.Add<ColorAdjustments>(false);
        grading.colorFilter.Override(new Color(.88f,.66f,1f));grading.contrast.Override(12f);
        grading.postExposure.Override(.04f);
        var vignette=tintProfile.Add<Vignette>(false);vignette.intensity.Override(.36f);
        vignette.color.Override(new Color(.04f,.008f,.08f));
        var aberration=tintProfile.Add<ChromaticAberration>(false);aberration.intensity.Override(.075f);
        tintVolume.sharedProfile=tintProfile;
    }
    void CreateEffects()
    {
        effectRoot=new GameObject("Purple Berserk - runtime effects"){hideFlags=HideFlags.DontSave};effectRoot.transform.SetParent(transform,false);
        if(arcMaterial!=null)
        {
            localArc=new Material(arcMaterial){hideFlags=HideFlags.DontSave};arcs=new LineRenderer[6];
            arcNextChange=new float[arcs.Length];arcEnergy=new float[arcs.Length];
            arcBornAgainAt=new float[arcs.Length];arcDiesAt=new float[arcs.Length];
            arcAlive=new bool[arcs.Length];arcRoute=new int[arcs.Length];
            for(int i=0;i<arcs.Length;i++)
            {
                var line=new GameObject("Local electric arc "+i).AddComponent<LineRenderer>();line.transform.SetParent(effectRoot.transform,false);
                line.sharedMaterial=localArc;line.useWorldSpace=false;line.positionCount=i<4?8:5;
                line.widthCurve=new AnimationCurve(new Keyframe(0f,.25f),new Keyframe(.16f,1f),new Keyframe(.6f,.7f),new Keyframe(1f,.035f));
                line.alignment=LineAlignment.View;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                line.numCornerVertices=0;line.enabled=false;arcs[i]=line;
            }
        }
        if(jetMaterial==null)return;
        GameObject emitter=new GameObject("Purple rearward cube jet");emitter.transform.SetParent(effectRoot.transform,false);
        emitter.transform.localPosition=new Vector3(0,-.18f,-.55f);emitter.transform.localRotation=Quaternion.Euler(0,180f,0);
        jet=emitter.AddComponent<ParticleSystem>();jet.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=jet.main;main.playOnAwake=false;main.loop=true;main.maxParticles=256;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.34f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(3f,5f);main.startSize=new ParticleSystem.MinMaxCurve(.045f,.10f);
        main.startColor=new Color(.83f,.55f,1f);main.gravityModifier=0f;
        var emission=jet.emission;emission.rateOverTime=0f;
        var shape=jet.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=14f;shape.radius=.13f;
        var overLife=jet.colorOverLifetime;overLife.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(.7f,.6f),new GradientAlphaKey(0f,1f)});overLife.color=gradient;
        var size=jet.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,1f,1f,.15f));
        var renderer=jet.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
        particleMesh=CreateCubeMesh();renderer.mesh=particleMesh;renderer.sharedMaterial=jetMaterial;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    void EmitEntrySparks()
    {
        for(int i=0;i<22;i++)
        {
            Vector3 direction=Random.onUnitSphere;
            var spark=new ParticleSystem.EmitParams{position=transform.position+direction*.48f,
                velocity=new Vector3(direction.x*.6f,direction.y*.6f,-Random.Range(4f,7f)),startLifetime=Random.Range(.16f,.28f),startSize=Random.Range(.035f,.065f)};
            jet.Emit(spark,1);
        }
    }
    void UpdateArcs()
    {
        float clearance=Mathf.Clamp(lightningRadius/1.1f,.95f,1.15f);
        for(int i=0;i<arcs.Length;i++)
        {
            if(arcAlive[i] && Time.time>=arcDiesAt[i])
            {
                arcAlive[i]=false;arcBornAgainAt[i]=Time.time+ArcRange(lightningGapSeconds);
                continue;
            }
            if(!arcAlive[i])
            {
                // C之后只让已有电弧退出，不再劈出新的；下次X重新启动生命周期。
                if(!wasBoosting || Time.time<arcBornAgainAt[i])continue;
                arcRoute[i]=PickArcRoute(i);arcAlive[i]=true;
                arcDiesAt[i]=Time.time+ArcRange(lightningLifetimeSeconds);
                arcNextChange[i]=0f;
            }
            if(Time.time<arcNextChange[i])continue;
            // 每条闪电独立重劈：约45～95ms，不同步整圈闪，不影响游戏的随机数。
            arcNextChange[i]=Time.time+ArcRandom(.7f,1.5f)/Mathf.Max(6f,lightningActivity);
            arcEnergy[i]=ArcRandom(i<4?.72f:.45f,1.15f);
            float length=lightningTailLength/2.32f*ArcRandom(.82f,1.18f);
            for(int j=0;j<arcs[i].positionCount;j++)
            {
                Vector3 point;
                int route=arcRoute[i];
                if(i<4)point=ElectricPaths[route,j];
                else
                {
                    // 短支路反复劈出/消失，也始终向后，不形成放射电场。
                    float progress=j/4f;float side=route==0 || route==2?-1f:1f;
                    point=ElectricPaths[route,3]+new Vector3(side*(j%2==0?.035f:.13f),progress*.08f,-progress*.75f);
                }
                point.x*=clearance;point.y*=clearance;
                if(point.z<0f)point.z*=length;
                float jitter=j==0?.035f:(j==arcs[i].positionCount-1?.19f:.16f);
                point.x+=ArcRandom(-jitter,jitter);point.y+=ArcRandom(-jitter,jitter)*.85f;
                // Z扰动小于相邻折点距离，保持向后拉的方向，不折回前方。
                point.z+=ArcRandom(-.035f,.035f);
                if(i<4 && route<2 && j<4)point.x=(route==0?-1f:1f)*Mathf.Max(.54f,Mathf.Abs(point.x));
                if(i<4 && route>=2 && j<4)point.y=Mathf.Max(.54f,point.y);
                point.y=Mathf.Max(-.36f,point.y);arcs[i].SetPosition(j,point);
            }
        }
    }
    float ArcRandom(float min,float max){return min+(float)arcRandom.NextDouble()*(max-min);}
    float ArcRange(Vector2 seconds)
    {
        float min=Mathf.Max(.02f,Mathf.Min(seconds.x,seconds.y));
        float max=Mathf.Max(min,Mathf.Max(seconds.x,seconds.y));
        return ArcRandom(min,max);
    }
    void BeginArcCycles()
    {
        if(arcs==null)return;
        for(int i=0;i<arcs.Length;i++)
        {
            arcAlive[i]=false;arcNextChange[i]=0f;
            // 前两条先劈出，其余错开，避免一进门就整组同时出现。
            arcBornAgainAt[i]=Time.time+ArcRandom(i<2?0f:.035f,i<2?.02f:.18f);
        }
    }
    int PickArcRoute(int slot)
    {
        int first=arcRandom.Next(4);
        if(slot>=4)return first;
        for(int offset=0;offset<4;offset++)
        {
            int route=(first+offset)%4;bool occupied=false;
            for(int i=0;i<4;i++)if(i!=slot && arcAlive[i] && arcRoute[i]==route){occupied=true;break;}
            if(!occupied)return route;
        }
        return first;
    }

    void CreateScreenTextures()
    {
        edgeGlow=new Texture2D(192,108,TextureFormat.RGBA32,false,true){name="Berserk purple edge glow",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp};
        var colors=new Color[192*108];
        for(int y=0;y<108;y++)for(int x=0;x<192;x++)
        {
            float dx=(x/191f-.5f)*2f,dy=(y/107f-.5f)*2f;
            float radius=Mathf.Sqrt(dx*dx+dy*dy);
            float edge=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.38f,1.15f,radius));
            colors[y*192+x]=new Color(.45f,.075f,.78f,.018f+edge*.28f);
        }
        edgeGlow.SetPixels(colors);edgeGlow.Apply(false,true);
        streakTexture=new Texture2D(64,4,TextureFormat.RGBA32,false,true){name="Berserk peripheral streak",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp};
        var pixels=new Color[64*4];
        for(int y=0;y<4;y++)for(int x=0;x<64;x++)
            pixels[y*64+x]=new Color(1f,1f,1f,Mathf.Pow(Mathf.Max(0f,Mathf.Sin(x/63f*Mathf.PI)),1.3f)*(y==0 || y==3?.3f:1f));
        streakTexture.SetPixels(pixels);streakTexture.Apply(false,true);
    }
    void OnGUI()
    {
        if(Event.current.type!=EventType.Repaint || !initialized || blend<=.001f || screenEnergy<=0f || cameraTint<=0f)return;
        if(runner==null || !runner.isActiveAndEnabled || clock==null || !clock.IsRunning || gameCamera==null || !gameCamera.isActiveAndEnabled)return;
        float reveal=0f;
        if(citySequence!=null)
        {
            float relative=(float)clock.SongTime-citySequence.transitionSongTime;
            reveal=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-1f,0f,relative))*
                (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,2f,relative)));
        }
        float strength=blend*screenEnergy*cameraTint*(1f-reveal);
        if(strength<=.001f)return;
        Rect viewport=gameCamera.pixelRect;
        float width=viewport.width,height=viewport.height,top=height*.12f;
        Color previousColor=GUI.color;Matrix4x4 previousMatrix=GUI.matrix;
        // 剪掉顶部调试文字；中心只有很轻的紫色，能量线只出现在周边。
        GUI.BeginGroup(new Rect(viewport.x,Screen.height-viewport.yMax+top,width,height-top));
        GUI.color=new Color(1f,1f,1f,strength*(1f+entryPulse*.25f));
        GUI.DrawTexture(new Rect(0,-top,width,height),edgeGlow);
        Vector2 center=new Vector2(width*.52f,height*.48f-top);
        for(int i=0;i<24;i++)
        {
            float angle=i*Mathf.PI*2f/24f+.05f;
            float phase=Mathf.Repeat(Time.time*.8f+i*.371f,1f);
            Vector2 direction=new Vector2(Mathf.Cos(angle)*width,Mathf.Sin(angle)*height);
            Vector2 point=center+direction*(.42f+phase*.20f);
            float length=width*(.035f+phase*.07f);
            GUI.matrix=previousMatrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,point);
            GUI.color=new Color(.78f,.42f,1f,strength*.65f*(1f-phase));
            GUI.DrawTexture(new Rect(point.x-length*.5f,point.y,length,Mathf.Max(1f,height*.0018f)),streakTexture);
        }
        GUI.matrix=previousMatrix;GUI.color=previousColor;GUI.EndGroup();
    }
    static Mesh CreateCubeMesh()
    {
        var mesh=new Mesh{name="Berserk particle cube",hideFlags=HideFlags.DontSave};
        mesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
        mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    public void ResetEffects()
    {
        if(!initialized)return;
        blend=0f;entryPulse=0f;wasBoosting=false;ApplyColors(0f);ApplyTrail(0f);
        if(jet!=null)jet.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(arcs!=null)for(int i=0;i<arcs.Length;i++)
        {arcs[i].enabled=false;arcNextChange[i]=0f;arcEnergy[i]=1f;arcAlive[i]=false;arcBornAgainAt[i]=float.PositiveInfinity;}
        if(tintVolume!=null){tintVolume.weight=0f;tintVolume.enabled=false;}
    }
    void OnDisable()
    {
        if(clock!=null)clock.RunStopped-=ResetEffects;
        ResetEffects();
        foreach(RendererState state in renderers)if(state.renderer!=null)state.renderer.sharedMaterials=state.original;
    }
    void OnDestroy()
    {
        foreach(MaterialState state in materials)if(state.material!=null)Destroy(state.material);
        if(localArc!=null)Destroy(localArc);if(particleMesh!=null)Destroy(particleMesh);
        if(edgeGlow!=null)Destroy(edgeGlow);if(streakTexture!=null)Destroy(streakTexture);
        if(effectRoot!=null)Destroy(effectRoot);if(tintVolume!=null)Destroy(tintVolume);
        if(tintProfile!=null){foreach(VolumeComponent component in tintProfile.components)Destroy(component);Destroy(tintProfile);}
    }
}
