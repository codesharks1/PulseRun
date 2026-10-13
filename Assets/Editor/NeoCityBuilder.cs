using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class NeoCityBuilder
{
    const string Request = "Temp/PulseRun_NeoCity.request";
    const string RootName = "NEO CITY - 23s Transition";
    const string Folder = "Assets/PulseLook/NeoCity";
    static Material dark, road, cyan, pink, glass, window, warm, cladding, advert, glow, haze;

    static NeoCityBuilder()
    { if (File.Exists(Request)) EditorApplication.update += BuildRequested; }

    static void BuildRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/PulseLook/NeonRunway.unity" || scene.isDirty) return;
        EditorApplication.update -= BuildRequested;
        if (Build())
        {
            EditorSceneManager.SaveScene(scene);
            File.Delete(Request);
            Debug.Log("NeoCity: scene saved successfully.");
            EditorApplication.delayCall += CapturePreview;
        }
    }

    [MenuItem("PulseRun/Build Neo City Transition")]
    public static void BuildFromMenu()
    {
        if (!Build()) return;
        if (File.Exists(Request)) File.Delete(Request);
        EditorApplication.delayCall += CapturePreview;
    }

    [MenuItem("PulseRun/Capture Neo City Preview")]
    public static void CapturePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        NeoCitySequence sequence = Object.FindFirstObjectByType<NeoCitySequence>();
        if (sequence == null || sequence.gameCamera == null || sequence.runner == null)
        { Debug.LogWarning("请先构建 Neo City，再截图。"); return; }
        Camera camera = sequence.gameCamera;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        float aspect = camera.aspect;
        bool compilation = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        RenderTexture previous = RenderTexture.active;
        RenderTexture texture = new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
        Texture2D capture = null;
        try
        {
            CameraFollow follow = camera.GetComponent<CameraFollow>();
            float exit = CityEntryZ(sequence.runner);
            Vector3 target = sequence.runner.transform.position;
            target.z = exit+30f; target.y = .5f;
            if (follow != null)
            {
                camera.transform.position = target+follow.offset;
                camera.transform.rotation = Quaternion.LookRotation(target+Vector3.forward*follow.lookAhead+Vector3.up*follow.lookHeight-camera.transform.position);
            }
            camera.aspect=1600f/900f; texture.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
            RenderTexture.active=texture;
            capture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            capture.ReadPixels(new Rect(0,0,1600,900),0,0);capture.Apply();
            Directory.CreateDirectory("PulsePreview");
            File.WriteAllBytes("PulsePreview/NeoCity-Preview.png",capture.EncodeToPNG());
            Debug.Log("Neo City 预览已保存到 PulsePreview/NeoCity-Preview.png（预览不包含玩家前移或动态泛白）。");
        }
        finally
        {
            RenderTexture.active=previous;
            camera.transform.SetPositionAndRotation(position,rotation);camera.aspect=aspect;
            ShaderUtil.allowAsyncCompilation=compilation;
            if(capture!=null)Object.DestroyImmediate(capture);
            texture.Release();Object.DestroyImmediate(texture);
        }
    }

    public static bool Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        Scene scene = SceneManager.GetActiveScene();
        RunnerController runner = null;
        GameObject straight = null, old = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Straight Neon Runway") straight = root;
            if (root.name == RootName) old = root;
            foreach (RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            { if (runner != null) { Debug.LogError("需要一个玩家。"); return false; } runner = candidate; }
        }
        if (runner == null || straight == null || runner.musicClock == null || runner.musicClock.music == null)
        { Debug.LogError("请打开已拉直的 NeonRunway 场景，并绑定玩家音乐。"); return false; }
        Transform ground = straight.transform.Find("Straight Ground - seamless collider");
        if (ground == null || ground.GetComponent<BoxCollider>() == null)
        { Debug.LogError("缺少连续 Ground 碰撞面，未修改场景。"); return false; }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/PulseLook","NeoCity");
        road = AssetDatabase.LoadAssetAtPath<Material>("Assets/PulseLook/Road.mat");
        dark = Material("City Graphite", new Color(.025f,.038f,.065f), Color.black);
        cyan = Material("City Cyan", new Color(.02f,.4f,.6f), new Color(.02f,.7f,1f)*3f);
        pink = Material("City Violet", new Color(.2f,.02f,.15f), new Color(.55f,.03f,.4f)*2f);
        window = Material("City Windows", new Color(.025f,.2f,.3f), new Color(.02f,.55f,.9f)*3f);
        warm = Material("Tunnel Lamps", new Color(.6f,.55f,.42f), new Color(1f,.83f,.6f)*2f);
        glass = Material("Maglev Translucent Deck", new Color(.025f,.15f,.22f,.26f), new Color(.01f,.035f,.055f), true);
        cladding = Material("Tunnel Panel Alloy",new Color(.06f,.085f,.11f),Color.black);
        advert = AdvertMaterial(); glow = ExitGlowMaterial();
        haze = CityHazeMaterial();
        if (road == null) return false;

        // 首次构建前保存一份副本，且不覆盖已有备份。
        string backup = "Assets/PulseLook/BeforeNeoCity.unity";
        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene,backup,true);
        float exit = CityEntryZ(runner);
        float tunnelStart = Mathf.Max(runner.transform.position.z+8f,exit-48f);
        Bounds bounds = ground.GetComponent<BoxCollider>().bounds;
        float end = bounds.max.z;
        if (exit+150f > end) { Debug.LogError("道路长度不足以容纳城市，未修改场景。"); return false; }

        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build Neo City Transition");
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject rootObject = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootObject,scene);
        Undo.RegisterCreatedObjectUndo(rootObject,"Create Neo City");
        Transform rootTransform = rootObject.transform;
        NeoCitySequence sequence = rootObject.AddComponent<NeoCitySequence>();
        sequence.runner = runner; sequence.musicClock = runner.musicClock;
        sequence.flashStrength = .28f;
        sequence.builtSpeed = runner.forwardSpeed; sequence.builtOffset = runner.musicClock.startOffsetSeconds;
        foreach (CameraFollow follow in Object.FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            if (follow.gameObject.scene == scene && follow.target == runner.transform) sequence.gameCamera = follow.GetComponent<Camera>();

        // 只替换路面外观，不动已验证的整条道路碰撞面。
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        Undo.RecordObject(groundRenderer,"Hide Opaque Road Visual"); groundRenderer.enabled = false;
        Box(rootTransform,"Intro solid road",new Vector3(0,-.25f,(bounds.min.z+exit)*.5f),new Vector3(7,.5f,exit-bounds.min.z),road);
        HideOldCityScenery(scene,exit);
        if (tunnelStart < exit-4f) BuildTunnel(rootTransform,tunnelStart,exit);
        GameObject exitCurtain=GameObject.CreatePrimitive(PrimitiveType.Quad);
        exitCurtain.name="Exit light haze - animated";exitCurtain.transform.SetParent(rootTransform,false);
        exitCurtain.transform.localPosition=new Vector3(0,4.7f,exit-.1f);exitCurtain.transform.localScale=new Vector3(21f,10f,1f);
        Object.DestroyImmediate(exitCurtain.GetComponent<Collider>());
        sequence.exitGlow=exitCurtain.GetComponent<Renderer>();sequence.exitGlow.sharedMaterial=glow;
        sequence.exitGlow.shadowCastingMode=ShadowCastingMode.Off;
        Light exitLight=new GameObject("Bright exit wash light").AddComponent<Light>();
        exitLight.transform.SetParent(rootTransform,false);exitLight.transform.localPosition=new Vector3(0,4,exit-3f);
        exitLight.type=LightType.Point;exitLight.color=new Color(.65f,.85f,1f);exitLight.range=28f;
        exitLight.intensity=0f;exitLight.shadows=LightShadows.None;sequence.exitLight=exitLight;

        var sections = new List<Transform>(); var traffic = new List<Transform>();
        var lowerTraffic = new List<NeoCitySequence.TrafficRoute>();
        var random = new System.Random(23012);
        for (float z = exit; z < end; z += 100f)
        {
            float length = Mathf.Min(100f,end-z);
            Transform section = Group(rootTransform,$"City block {sections.Count:D2}",new Vector3(0,0,z));
            sections.Add(section);
            BuildRoad(section,length);
            for (int i = 0; i < 16; i++)
            {
                float side = i%2 == 0 ? -1f : 1f;
                bool skyline = i>=8;
                float depth = (i%8)/2*24f+6f+(skyline?8f:0f);
                float height = (skyline?48f:28f)+(float)random.NextDouble()*(skyline?34f:22f);
                float x = side*((skyline?40f:19f)+(float)random.NextDouble()*8f);
                BuildTower(section,new Vector3(x,-18f+height*.5f,depth),height,i);
            }
            BuildCityDetails(section,length,sections.Count);
            if(sequence.gameCamera!=null)
            {
                GameObject atmosphere=GameObject.CreatePrimitive(PrimitiveType.Quad);
                atmosphere.name="Lower city blue-violet atmosphere";atmosphere.transform.SetParent(section,false);
                atmosphere.transform.localPosition=new Vector3(0,-11f,length*.55f);atmosphere.transform.localScale=new Vector3(90f,15f,1f);
                atmosphere.transform.rotation=sequence.gameCamera.transform.rotation;
                Object.DestroyImmediate(atmosphere.GetComponent<Collider>());atmosphere.GetComponent<Renderer>().sharedMaterial=haze;
            }
            BuildSpire(section,new Vector3(-32f,0,Mathf.Min(36f,length*.4f)),62f+sections.Count%3*9f);
            BuildSpire(section,new Vector3(34f,0,Mathf.Min(82f,length*.8f)),78f+sections.Count%4*6f);
            if (sections.Count%2 == 1) BuildSpectators(section,50f);
            // 静态物件按材质合并，避免窗灯每个都成为一个绘制调用。
            Bake(section,$"CityBlock{sections.Count:D2}");
            for (int i = 0; i < 6; i++)
            {
                float side = i%2 == 0 ? -1f : 1f;
                Transform car = Group(section,"Background traffic - decorative",new Vector3(side*11.5f,-2.45f,8f+i*13f));
                Box(car,"Car body",Vector3.zero,new Vector3(1.25f,.45f,2.4f),dark);
                Box(car,"Cabin",new Vector3(0,.3f,0),new Vector3(.95f,.3f,1.25f),window);
                Box(car,"Hood",new Vector3(0,.13f,side<0?.85f:-.85f),new Vector3(1.1f,.18f,.5f),dark);
                Box(car,"Headlights",new Vector3(0,0,side<0?1.22f:-1.22f),new Vector3(.85f,.08f,.05f),warm);
                foreach(float wheelSide in new[]{-1f,1f})foreach(float axle in new[]{-.75f,.75f})
                    Box(car,"Wheel",new Vector3(wheelSide*.64f,-.18f,axle),new Vector3(.15f,.32f,.32f),road);
                Box(car,"Tail lights",new Vector3(0,0,side<0?-1.22f:1.22f),new Vector3(1f,.08f,.05f),pink);
                traffic.Add(car);
            }
            BuildLowerTraffic(section,length,sections.Count,lowerTraffic);
        }
        sequence.citySections = sections.ToArray(); sequence.traffic = traffic.ToArray();
        sequence.lowerTraffic = lowerTraffic.ToArray();
        // 转场中已有障碍暂时隐藏，保留记录与物体，避免曝光遮挡造成死亡。
        RunnerRhythmMotion rhythmMotion=runner.GetComponent<RunnerRhythmMotion>();
        float safeStart=rhythmMotion!=null?runner.transform.position.z+rhythmMotion.DistanceAt(21.7f)-1.5f:exit-runner.forwardSpeed*1.3f-1.5f;
        float safeEnd=rhythmMotion!=null?runner.transform.position.z+rhythmMotion.DistanceAt(25.1f)+1.5f:exit+runner.forwardSpeed*2.1f+1.5f;
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            if (sceneRoot.name == "Generated Rhythm Obstacles")
                foreach (Transform obstacle in sceneRoot.transform)
                    if (obstacle.GetComponentInChildren<RunnerHazard>(true)!=null &&
                        obstacle.position.z > safeStart && obstacle.position.z < safeEnd)
                    { Undo.RecordObject(obstacle.gameObject,"Transition safe zone"); obstacle.gameObject.SetActive(false); }
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = rootObject;
        Debug.Log($"Neo City 已构建：歌曲23秒入口 Z={exit:F1}，隧道长{Mathf.Max(0,exit-tunnelStart):F1}，城市{sections.Count}区块。镜头和手感未改变；保存后 Play。",rootObject);
        return true;
    }

    static void HideOldCityScenery(Scene scene,float exit)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Straight Neon Runway" && root.name != "NEON RUNWAY - editable scene objects") continue;
            foreach (Transform child in root.transform)
                if ((child.name.StartsWith("Background monolith") || child.name.StartsWith("Monolith light") || child.name == "Beat marker") && child.position.z >= exit-12f)
                { Undo.RecordObject(child.gameObject,"Hide old backdrop in city"); child.gameObject.SetActive(false); }
        }
    }

    static float CityEntryZ(RunnerController runner)
    {
        RunnerRhythmMotion rhythm=runner.GetComponent<RunnerRhythmMotion>();
        float distance=rhythm!=null?rhythm.DistanceAt(23f):Mathf.Max(0f,23f-runner.musicClock.startOffsetSeconds)*runner.forwardSpeed;
        return runner.transform.position.z+distance;
    }

    static void BuildTunnel(Transform root,float start,float exit)
    {
        Transform tunnel = Group(root,"Dim transition tunnel",new Vector3(0,0,start));
        float length = exit-start;
        // 足够宽，侧后方相机不必改成正后方，也不会穿入墙面。
        Box(tunnel,"Roof shell",new Vector3(0,10.4f,length*.5f),new Vector3(24,1.3f,length+1f),dark);
        Box(tunnel,"Full-width closed floor",new Vector3(0,-.7f,length*.5f),new Vector3(24,1.2f,length+1f),dark);
        foreach(float side in new[]{-1f,1f})
        {
            Box(tunnel,"Thick closed side wall",new Vector3(side*11.4f,4.7f,length*.5f),new Vector3(1.6f,11,length+1f),dark);
            Box(tunnel,"Low guide strip",new Vector3(side*10.7f,.9f,length*.5f),new Vector3(.06f,.05f,length),cyan);
            Box(tunnel,"Lower wall cladding",new Vector3(side*10.65f,1.4f,length*.5f),new Vector3(.2f,2f,length),road);
        }
        for(float z=0;z<=length;z+=6f)
        {
            foreach(float side in new[]{-1f,1f})
            {
                Box(tunnel,"Heavy lower wall rib",new Vector3(side*10.25f,3.1f,z),new Vector3(.55f,6.2f,.65f),cladding);
                GameObject bevel=Box(tunnel,"Angled arch haunch",new Vector3(side*9.15f,7.7f,z),new Vector3(.55f,3.7f,.65f),cladding);
                bevel.transform.localRotation=Quaternion.Euler(0,0,side*40f);
                Box(tunnel,"Wall panel",new Vector3(side*10.54f,3.7f,z+2.5f),new Vector3(.13f,3.7f,4.6f),cladding);
                Box(tunnel,"Cable trough",new Vector3(side*10.37f,1.8f,z+2.5f),new Vector3(.2f,.25f,5.5f),dark);
                Box(tunnel,"Maintenance curb",new Vector3(side*8.6f,.15f,z+2.5f),new Vector3(3f,.45f,5.8f),road);
            }
            Box(tunnel,"Heavy ceiling rib",new Vector3(0,9.1f,z),new Vector3(16,.55f,.65f),cladding);
            Box(tunnel,"Ceiling inset panel",new Vector3(0,9.58f,z+2.5f),new Vector3(12,.12f,4.5f),cladding);
            Box(tunnel,"Overhead lamp",new Vector3(0,9.4f,z+1.8f),new Vector3(.3f,.07f,1.4f),warm);
            foreach(float side in new[]{-1f,1f})
            {
                Box(tunnel,"Service recess",new Vector3(side*10.43f,4f,z+2.5f),new Vector3(.14f,2.4f,1.8f),road);
                Box(tunnel,"Wall inset lamp",new Vector3(side*10.32f,4f,z+2.5f),new Vector3(.06f,1.2f,.07f),window);
            }
            Light light = new GameObject("Tunnel fill light").AddComponent<Light>();
            light.transform.SetParent(tunnel,false); light.transform.localPosition = new Vector3(0,5,z+2f);
            light.type=LightType.Point; light.color=new Color(.25f,.45f,.6f); light.intensity=1.2f; light.range=11f; light.shadows=LightShadows.None;
        }
        foreach(float side in new[]{-1f,1f})
        {
            Box(tunnel,"Exit structural bulkhead",new Vector3(side*11,4.6f,length),new Vector3(1.6f,10.5f,1.5f),cladding);
            Box(tunnel,"Exit cyan frame",new Vector3(side*10.55f,4.7f,length),new Vector3(.09f,9.4f,.1f),cyan);
        }
        Box(tunnel,"Exit bulkhead lintel",new Vector3(0,10f,length),new Vector3(24,1.3f,1.5f),cladding);
        Bake(tunnel,"Tunnel");
    }

    static void BuildRoad(Transform section,float length)
    {
        for(float z=0;z<length;z+=5f)
        {
            float panel=Mathf.Min(5f,length-z);
            Box(section,"Translucent maglev deck",new Vector3(0,-.13f,z+panel*.5f),new Vector3(6.85f,.25f,panel-.04f),glass);
            Box(section,"Magnetic cross frame",new Vector3(0,-.03f,z),new Vector3(7,.035f,.035f),cyan);
        }
        foreach(float side in new[]{-1f,1f})
        {
            Box(section,"Structural edge",new Vector3(side*3.5f,-.45f,length*.5f),new Vector3(.18f,.4f,length),dark);
            Box(section,"Adjacent traffic highway",new Vector3(side*11.5f,-3,length*.5f),new Vector3(4,.5f,length),road);
            Box(section,"Traffic edge light",new Vector3(side*13.45f,-2.71f,length*.5f),new Vector3(.04f,.05f,length),window);
        }
    }

    static void BuildTower(Transform root,Vector3 center,float height,int index)
    {
        float width=4f+index%3*1.5f;
        Box(root,"City tower",center,new Vector3(width,height,5),dark);
        Box(root,"Roof crown",center+Vector3.up*(height*.5f+.4f),new Vector3(width*.7f,.8f,3.5f),dark);
        if(index%2==0)
            Box(root,"Stepped upper facade",center+new Vector3(0,height*.36f,0),new Vector3(width+1.2f,height*.24f,6f),dark);
        if(index%3==0)
        {
            Box(root,"Roof antenna",center+new Vector3(0,height*.5f+3f,0),new Vector3(.12f,5f,.12f),dark);
            Box(root,"Roof beacon",center+new Vector3(0,height*.5f+5.5f,0),new Vector3(.16f,.25f,.16f),pink);
        }
        Material accent=index%4==0?pink:window;
        for(int column=0;column<3;column++)
            for(float y=-height*.5f+2f;y<height*.5f-1f;y+=4f)
                Box(root,"Window",center+new Vector3((column-1)*width*.24f,y,-2.52f),new Vector3(.48f,.85f,.04f),accent);
        // 两侧朝向赛道的立面也有灯带，从斜视镜头能看见。
        float towardRoad=center.x<0?1f:-1f;
        Box(root,"Tower corner strip",center+new Vector3(towardRoad*(width*.5f+.025f),0,-2.3f),new Vector3(.065f,height*.8f,.09f),accent);
        for(int column=0;column<3;column++)
            for(float y=-height*.5f+2f;y<height*.5f-2f;y+=3.5f)
                if((column+(int)y+index)%4!=0)
                    Box(root,"Road-facing window",center+new Vector3(towardRoad*(width*.5f+.035f),y,(column-1)*1.4f),new Vector3(.05f,.8f,.55f),accent);
        if(index%3==0)
        {
            float x=towardRoad*(width*.5f+.07f);
            Box(root,"Vertical luminous sign",center+new Vector3(x,2f,0),new Vector3(.08f,5.5f,1.1f),index%2==0?pink:cyan);
            Box(root,"Sign divider",center+new Vector3(x+towardRoad*.06f,2f,0),new Vector3(.03f,.12f,1.15f),dark);
        }
        Box(root,"Crown light",center+new Vector3(0,height*.5f+.85f,-1.8f),new Vector3(width*.65f,.055f,.055f),accent);
    }

    static void BuildCityDetails(Transform root,float length,int index)
    {
        // 下层横街和远景楼基座让半透明地板下方有空间深度。
        BuildBillboard(root,new Vector3(-17f,5f,Mathf.Min(22f,length*.3f)),index);
        BuildBillboard(root,new Vector3(18f,9f,Mathf.Min(64f,length*.7f)),index+1);
        Box(root,"Lower cross street",new Vector3(0,-16f,length*.65f),new Vector3(90,.35f,6),road);
        Box(root,"Lower cross street lights",new Vector3(0,-15.8f,length*.65f-2.9f),new Vector3(90,.06f,.06f),window);
        Box(root,"Under-track traffic road",new Vector3(0,-10.5f,length*.5f),new Vector3(8,.6f,length),road);
        foreach(float side in new[]{-1f,1f})
            Box(root,"Under-track highway light",new Vector3(side*3.85f,-10.16f,length*.5f),new Vector3(.08f,.08f,length),pink);
        // 第一段就能看到交错高架，不必跑到第三个区块。
        Transform interchange=Group(root,"Diagonal elevated interchange",new Vector3(0,9f,Mathf.Min(40f,length*.45f)));
        interchange.localRotation=Quaternion.Euler(0,index%2==0?18f:-18f,0);
        Box(interchange,"Elevated bridge deck",Vector3.zero,new Vector3(86,.7f,4f),dark);
        foreach(float edge in new[]{-1f,1f})
        {
            Box(interchange,"Bridge luminous railing",new Vector3(0,.5f,edge*1.95f),new Vector3(86,.06f,.08f),index%2==0?cyan:pink);
            Box(interchange,"Bridge edge guard",new Vector3(0,.1f,edge*2),new Vector3(86,.65f,.18f),dark);
        }
        foreach(float x in new[]{-14f,14f})
        {
            Box(interchange,"Bridge support pier",new Vector3(x,-11,0),new Vector3(1.2f,21f,1.4f),dark);
            Box(interchange,"Pier luminous trim",new Vector3(x,-10.5f,-.73f),new Vector3(.065f,19,.06f),window);
        }
        foreach(float side in new[]{-1f,1f})
        {
            Box(root,"Lower podium",new Vector3(side*28f,-23f,length*.5f),new Vector3(22,8,length*.85f),dark);
            Box(root,"Podium trim",new Vector3(side*17f,-19f,length*.5f),new Vector3(.08f,.08f,length*.8f),window);
            Box(root,"Traffic underside rail",new Vector3(side*11.5f,-3.4f,length*.5f),new Vector3(.12f,.2f,length),dark);
        }
        if(index%3==0)
        {
            Box(root,"Distant skybridge",new Vector3(0,16f,length*.7f),new Vector3(90,.5f,3),dark);
            Box(root,"Skybridge accent",new Vector3(0,15.7f,length*.7f-1.4f),new Vector3(90,.06f,.06f),pink);
        }
    }

    static void BuildSpectators(Transform root,float z)
    {
        Box(root,"Viewing balcony",new Vector3(-7,1.3f,z),new Vector3(4,.3f,12),dark);
        Box(root,"Balcony glass barrier",new Vector3(-5,2,z),new Vector3(.05f,1.2f,12),glass);
        Box(root,"Balcony rail",new Vector3(-5,2.6f,z),new Vector3(.06f,.04f,12),cyan);
        for(int i=0;i<7;i++)
        {
            Vector3 p=new Vector3(-6.5f,2,z-4.5f+i*1.4f);
            Box(root,"Audience silhouette",p,new Vector3(.35f,1.1f,.3f),dark);
            Box(root,"Audience head",p+Vector3.up*.72f,new Vector3(.28f,.28f,.28f),dark);
            Box(root,"Glow stick",p+new Vector3(.3f,.7f,0),new Vector3(.035f,.4f,.035f),pink);
        }
    }

    static void BuildSpire(Transform root,Vector3 location,float height)
    {
        float bottom=-18f;
        GameObject shaft=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name="Octagonal-style landmark tower";shaft.transform.SetParent(root,false);
        shaft.transform.localPosition=location+Vector3.up*(bottom+height*.5f);
        shaft.transform.localScale=new Vector3(7f,height*.5f,7f);
        Object.DestroyImmediate(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().sharedMaterial=dark;
        Mesh cone=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/LandmarkSpire.asset");
        if(cone==null)
        {
            cone=new Mesh{name="Landmark Spire"};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4f,b=(i+1)*Mathf.PI/4f;int n=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                vertices.Add(Vector3.up);
                vertices.Add(new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f));
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
            }
            cone.SetVertices(vertices);cone.SetTriangles(triangles,0);cone.RecalculateNormals();cone.RecalculateBounds();
            AssetDatabase.CreateAsset(cone,Folder+"/LandmarkSpire.asset");
        }
        Transform roof=Group(root,"Tapered landmark crown",location+Vector3.up*(bottom+height));
        roof.localScale=new Vector3(7.5f,16f,7.5f);
        roof.gameObject.AddComponent<MeshFilter>().sharedMesh=cone;
        roof.gameObject.AddComponent<MeshRenderer>().sharedMaterial=dark;
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4f;
            Vector3 edge=new Vector3(Mathf.Cos(angle)*3.53f,0,Mathf.Sin(angle)*3.53f);
            Box(root,"Spire vertical neon",location+edge+Vector3.up*(bottom+height*.5f),new Vector3(.08f,height*.9f,.08f),i%3==0?pink:window);
            Vector3 start=location+edge+Vector3.up*(bottom+height);
            Vector3 tip=location+Vector3.up*(bottom+height+16f);
            Beam(root,"Crown light rib",start,tip,.07f,i%3==0?pink:cyan);
        }
        Box(root,"Spire antenna",location+Vector3.up*(bottom+height+18),new Vector3(.12f,4,.12f),dark);
        Box(root,"Spire beacon",location+Vector3.up*(bottom+height+20),new Vector3(.22f,.4f,.22f),pink);
    }

    static void Beam(Transform parent,string name,Vector3 start,Vector3 end,float width,Material material)
    {
        GameObject beam=Box(parent,name,(start+end)*.5f,new Vector3(width,width,Vector3.Distance(start,end)),material);
        beam.transform.localRotation=Quaternion.LookRotation(end-start);
    }

    static void BuildLowerTraffic(Transform section,float length,int index,List<NeoCitySequence.TrafficRoute> routes)
    {
        for(int i=0;i<16;i++)
        {
            bool cross=i>=8;
            bool reverse=i%2==0;
            Vector3 from=cross?new Vector3(-43,-15.35f,length*.65f+(reverse?-1.3f:1.3f)):
                new Vector3(reverse?-1.5f:1.5f,-9.65f,2f);
            Vector3 to=cross?new Vector3(43,from.y,from.z):new Vector3(from.x,from.y,Mathf.Max(3f,length-2f));
            if(reverse){Vector3 swap=from;from=to;to=swap;}
            Transform car=Group(section,"Lower-level moving traffic",Vector3.Lerp(from,to,(i%8)/8f));
            car.localRotation=Quaternion.LookRotation(to-from);
            Box(car,"Car body",Vector3.zero,new Vector3(1.1f,.4f,2.1f),dark);
            Box(car,"Cabin",new Vector3(0,.26f,0),new Vector3(.85f,.25f,1.1f),window);
            Box(car,"Headlights",new Vector3(0,0,1.08f),new Vector3(.9f,.08f,.05f),warm);
            Box(car,"Tail lights",new Vector3(0,0,-1.08f),new Vector3(.9f,.08f,.05f),pink);
            Box(car,"Short traffic streak",new Vector3(0,-.08f,-2f),new Vector3(.055f,.03f,1.4f),index%2==0?pink:window);
            routes.Add(new NeoCitySequence.TrafficRoute{car=car,from=from,to=to,speed=11f+i%4*3f,phase=(i%8)/8f});
        }
    }

    static Material AdvertMaterial()
    {
        string texturePath=Folder+"/NeoWaveAdvert.asset";
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if(texture==null)
        {
            texture=new Texture2D(128,256,TextureFormat.RGBA32,false){name="Neo Wave Advert",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<256;y++)for(int x=0;x<128;x++)
            {
                float u=x/127f,v=y/255f;
                Color color=Color.Lerp(new Color(.02f,.09f,.2f),new Color(.22f,.015f,.3f),v);
                float ring=Mathf.Abs(Mathf.Sqrt((u-.5f)*(u-.5f)+(v-.63f)*(v-.63f))-.28f);
                float wave=Mathf.Abs(v-(.28f+.09f*Mathf.Sin(u*9f)));
                if(ring<.012f)color=new Color(.2f,.9f,1f);
                if(wave<.015f || Mathf.Abs(v-(.18f+.06f*Mathf.Sin(u*9f)))<.009f)color=new Color(.8f,.04f,.65f);
                if(y%20==0)color*=.6f;
                texture.SetPixel(x,y,color);
            }
            texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
        }
        Material material=Material("Neo Wave Advertising",Color.white,Color.white*2f);
        material.SetTexture("_BaseMap",texture);material.SetTexture("_EmissionMap",texture);EditorUtility.SetDirty(material);
        return material;
    }

    static Material ExitGlowMaterial()
    {
        string texturePath=Folder+"/ExitHaze.asset";
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if(texture==null)
        {
            texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Exit Haze",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float u=x/63f,v=y/63f;
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.SmoothStep(0f,1f,Mathf.Clamp01(edge*10f))));
            }
            texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
        }
        string path=Folder+"/Exit Exposure Haze.mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created=material==null;
        if(created)material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",new Color(2.6f,3.2f,3.8f,.13f));
        material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3010;
        if(created)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);
        return material;
    }

    static Material CityHazeMaterial()
    {
        string path=Folder+"/Lower City Atmosphere.mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);bool created=material==null;
        if(created)material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PulseLook/MistTexture.asset");
        if(texture!=null)material.SetTexture("_BaseMap",texture);
        material.SetColor("_BaseColor",new Color(.1f,.16f,.42f,.25f));
        material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=2995;
        if(created)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);
        return material;
    }

    static void BuildBillboard(Transform root,Vector3 position,int index)
    {
        Transform board=Group(root,"Neon advertising display",position);
        board.localRotation=Quaternion.Euler(0,position.x<0?22f:-22f,0);
        float width=index%2==0?5f:7f,height=index%2==0?9f:5f;
        Box(board,"Screen cabinet",Vector3.zero,new Vector3(width+.5f,height+.5f,.55f),dark);
        Box(board,"Animated-style neon artwork",new Vector3(0,0,-.31f),new Vector3(width,height,.05f),advert);
        foreach(float side in new[]{-1f,1f})
        {
            Box(board,"Screen vertical border",new Vector3(side*(width*.5f+.1f),0,-.35f),new Vector3(.07f,height+.2f,.06f),index%2==0?cyan:pink);
            Box(board,"Screen horizontal border",new Vector3(0,side*(height*.5f+.1f),-.35f),new Vector3(width+.2f,.07f,.06f),index%2==0?cyan:pink);
        }
        Transform label=Group(board,"Advertising title",new Vector3(0,-height*.3f,-.39f));
        TextMesh text=label.gameObject.AddComponent<TextMesh>();
        text.text=index%2==0?"NEO":"PULSE";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.fontSize=64;text.characterSize=.16f;text.color=new Color(.7f,.95f,1f);
    }

    static Transform Group(Transform parent,string name,Vector3 position)
    { GameObject obj=new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.localPosition=position; return obj.transform; }

    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
        obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=size;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        Renderer renderer=obj.GetComponent<Renderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        return obj;
    }

    static Material Material(string name,Color color,Color emission,bool transparent=false)
    {
        string path=Folder+"/"+name+".mat";
        Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        Material m=existing!=null?existing:new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name;
        if(existing!=null)Undo.RecordObject(m,"Polish Neo City Material");
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",transparent?.65f:.45f); m.SetFloat("_Metallic",.2f);
        if(emission.maxColorComponent>0)
        {
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetColor("_EmissionColor",emission);m.EnableKeyword("_EMISSION");
        }
        if(transparent)
        {
            m.SetFloat("_Surface",1); m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Blend",0);m.SetFloat("_BlendModePreserveSpecular",0);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
        }
        m.enableInstancing=true;
        if(existing==null)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);
        return m;
    }

    static void Bake(Transform root,string name)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();
        var originals=new List<GameObject>();
        foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.sharedMesh==null || filter.GetComponent<TextMesh>()!=null)continue;
            MeshRenderer renderer=filter.GetComponent<MeshRenderer>(); if(renderer==null || renderer.sharedMaterial==null)continue;
            Material mat=renderer.sharedMaterial;
            if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();
            groups[mat].Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            originals.Add(filter.gameObject);
        }
        foreach(var pair in groups)
        {
            Mesh mesh=new Mesh{name=name+" - "+pair.Key.name,indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(pair.Value.ToArray(),true,true); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+mesh.name+".asset"));
            Transform visual=Group(root,pair.Key.name+" geometry",Vector3.zero);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=visual.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        foreach(GameObject obj in originals)Object.DestroyImmediate(obj);
    }
}
