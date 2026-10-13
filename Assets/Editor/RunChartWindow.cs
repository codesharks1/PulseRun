using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class RunChartWindow : EditorWindow
{
    const string Pending="PulseRun.Chart.StartPending";
    const string Snapshot="PulseRun.Chart.LastRecording";
    const string MusicAsset="PulseRun.Chart.LastMusic";
    const string ResumeSource="PulseRun.Chart.ResumeSource";
    const string ResumeSecond="PulseRun.Chart.ResumeSecond";
    [SerializeField] RunChart selectedChart;
    [SerializeField] RunChart resumeChart;
    [SerializeField] float checkpointSeconds;
    Vector2 scroll;

    static RunChartWindow()
    {
        EditorApplication.playModeStateChanged+=OnPlayState;
        EditorApplication.update+=ObserveRecording;
        EditorApplication.update+=TryRequestedPreview;
    }
    [MenuItem("PulseRun/Rhythm Chart Recorder")]
    public static void Open()=>GetWindow<RunChartWindow>("谱面录制");
    static RunnerController Runner()
    {
        RunnerController found=null;
        foreach(GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(RunnerController candidate in root.GetComponentsInChildren<RunnerController>(true))
            {if(found!=null)return null;found=candidate;}
        return found;
    }
    static void TryRequestedPreview()
    {
        const string request="Temp/PulseRun_ChartPreview.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SceneManager.GetActiveScene().path!="Assets/PulseLook/NeonRunway.unity" || Runner()==null)return;
        string path=File.ReadAllText(request).Trim();
        RunChart chart=path.StartsWith("Assets/Charts/")?AssetDatabase.LoadAssetAtPath<RunChart>(path):null;
        File.Delete(request);
        if(chart==null){Debug.LogError("没有找到请求的谱面。");return;}
        if(Generate(chart,true))File.WriteAllText("Temp/PulseRun_ChartPreview.result","Generated preview: "+path+"\nScene is modified; save after testing.");
    }
    static RunChartRecorder Recorder()=>Object.FindFirstObjectByType<RunChartRecorder>();
    static void Capture(RunChartRecorder recorder)
    {
        if(recorder==null || recorder.Recording==null)return;
        SessionState.SetString(Snapshot,JsonUtility.ToJson(recorder.Recording));
        SessionState.SetString(MusicAsset,AssetDatabase.GetAssetPath(recorder.Clock.music));
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
        {
            SessionState.SetBool(Pending,false);RunnerController runner=Runner();
            if(runner==null || runner.musicClock==null){Debug.LogError("录制需要玩家已绑定 Music Timeline。");EditorApplication.isPlaying=false;return;}
            var recorder=runner.musicClock.gameObject.AddComponent<RunChartRecorder>();
            string resumePath=SessionState.GetString(ResumeSource,"");
            RunChart seed=string.IsNullOrEmpty(resumePath)?null:AssetDatabase.LoadAssetAtPath<RunChart>(resumePath);
            if(!string.IsNullOrEmpty(resumePath) && (seed==null || seed.music==null || seed.recording==null))
            {Debug.LogError("续录原谱面不可用，已取消。");EditorApplication.isPlaying=false;return;}
            recorder.Begin(runner,seed,SessionState.GetFloat(ResumeSecond,0f));Capture(recorder);
        }
        if(state==PlayModeStateChange.ExitingPlayMode)
        {
            var recorder=Recorder();if(recorder!=null){recorder.Finish();Capture(recorder);}
        }
        if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Pending,false);
    }
    static void ObserveRecording()
    {
        if(!EditorApplication.isPlaying)return;
        var recorder=Recorder();if(recorder==null)return;
        Capture(recorder);
        if(recorder.Finished)EditorApplication.isPlaying=false;
    }
    static RunChartRecording LastRecording()
    {
        string json=SessionState.GetString(Snapshot,"");
        return string.IsNullOrEmpty(json)?null:JsonUtility.FromJson<RunChartRecording>(json);
    }
    void OnInspectorUpdate(){Repaint();}
    void OnGUI()
    {
        EditorGUILayout.LabelField("听音乐标记 → 保存谱面 → 生成障碍",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("空格：起跳。H：保持落地。X：开始加速光门。C：结束加速段。Backspace：撤销。Enter：结束录制。\n录制时玩家冻结，练习障碍暂时隐藏。请先保存当前场景。",MessageType.Info);
        EditorGUILayout.HelpBox("生成使用玩家当前的重力倍率；调整倍率后可选择原谱面重新生成，无需重新打点。跑速和跳高沿用谱面记录。",MessageType.Info);
        RunnerController runner=Runner();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || runner==null || runner.musicClock==null))
            if(GUILayout.Button("1. 开始录制（进入 Play）",GUILayout.Height(32)))
            {
                SessionState.SetString(ResumeSource,"");
                SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
            }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("从歌曲时间检查点续录",EditorStyles.boldLabel);
        resumeChart=(RunChart)EditorGUILayout.ObjectField("续录原谱面",resumeChart,typeof(RunChart),false);
        checkpointSeconds=EditorGUILayout.FloatField("起始歌曲时间（秒）",checkpointSeconds);
        using(new EditorGUI.DisabledScope(resumeChart==null || resumeChart.recording==null))
            if(GUILayout.Button("使用原谱面最后录制时间"))checkpointSeconds=resumeChart.recording.endSongTime;
        EditorGUILayout.HelpBox("例如从12秒续录：前12秒保留，只替换本次录制覆盖的区间，后面的旧标记也保留。保存为新文件，不覆盖原谱面。Backspace只撤销本次新标记。",MessageType.Info);
        bool validResume=resumeChart!=null && resumeChart.music!=null && resumeChart.recording!=null &&
            resumeChart.recording.marks!=null && checkpointSeconds>=resumeChart.recording.startOffsetSeconds &&
            checkpointSeconds<resumeChart.music.length-.01f && !float.IsNaN(checkpointSeconds);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || runner==null || runner.musicClock==null || !validResume))
            if(GUILayout.Button("从这个检查点继续录制（进入 Play）",GUILayout.Height(30)))
            {
                SessionState.SetString(ResumeSource,AssetDatabase.GetAssetPath(resumeChart));
                SessionState.SetFloat(ResumeSecond,checkpointSeconds);
                SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
            }
        if(EditorApplication.isPlaying && Recorder()!=null && GUILayout.Button("结束录制并退出 Play"))
        {var recorder=Recorder();recorder.Finish();Capture(recorder);EditorApplication.isPlaying=false;}
        RunChartRecording last=LastRecording();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || last==null || last.marks.Count==0))
            if(GUILayout.Button("2. 将本次录制保存为新谱面"))Save(last);
        selectedChart=(RunChart)EditorGUILayout.ObjectField("用于生成的谱面",selectedChart,typeof(RunChart),false);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || selectedChart==null))
            if(GUILayout.Button("3. 按谱面生成障碍",GUILayout.Height(32)))Generate(selectedChart);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || selectedChart==null))
            if(GUILayout.Button("生成试玩预览（仅允许空中通过X/C）"))Generate(selectedChart,true);
        if(last!=null)
        {
            EditorGUILayout.LabelField($"本次录制：{last.marks.Count} 个标记，起始歌曲时间 {last.startOffsetSeconds:F2}s");
            scroll=EditorGUILayout.BeginScrollView(scroll);
            for(int i=0;i<last.marks.Count;i++)
                EditorGUILayout.LabelField($"{i+1}. {MarkLabel(last.marks[i].type)}    {last.marks[i].songTime:F3}s");
            EditorGUILayout.EndScrollView();
        }
    }
    void Save(RunChartRecording recording)
    {
        if(!AssetDatabase.IsValidFolder("Assets/Charts"))AssetDatabase.CreateFolder("Assets","Charts");
        RunChart chart=CreateInstance<RunChart>();chart.recording=JsonUtility.FromJson<RunChartRecording>(JsonUtility.ToJson(recording));
        chart.music=AssetDatabase.LoadAssetAtPath<AudioClip>(SessionState.GetString(MusicAsset,""));
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Charts/NeonRainbowChart.asset");
        AssetDatabase.CreateAsset(chart,path);AssetDatabase.SaveAssets();selectedChart=chart;resumeChart=chart;
        Debug.Log("谱面已保存："+path,chart);
    }

    static string MarkLabel(RunChartMarkType type)
    {
        switch(type){case RunChartMarkType.Jump:return "起跳";case RunChartMarkType.StayGrounded:return "保持落地";
            case RunChartMarkType.BoostStart:return "X 加速光门";case RunChartMarkType.BoostEnd:return "C 恢复速度";default:return "未知";}
    }
    class Placement{public RunChartMark mark;public Vector3 position,forward;public float distance;}
    public static bool Generate(RunChart chart,bool allowAirSwitch=false)
    {
        RunnerController runner=Runner();
        if(runner==null || runner.musicClock==null || chart.music==null){Debug.LogError("生成需要玩家、音乐时间轴和有效谱面音乐。");return false;}
        RunChartRecording data=chart.recording;
        if(data==null || data.marks==null || data.marks.Count==0 || data.marks.Exists(mark=>mark==null) || data.forwardSpeed<=0 || data.jumpHeight<=0 || data.gravity<=0)
        {Debug.LogError("谱面为空或移动参数无效。");return false;}
        GameObject spikes=null,gate=null,old=null;
        foreach(GameObject root in runner.gameObject.scene.GetRootGameObjects())
        {
            if(root.name=="Red Spike Practice")spikes=root;
            if(root.name=="Hanging Beat Gate")gate=root;
            if(root.name=="Generated Rhythm Obstacles")old=root;
        }
        var marks=new List<RunChartMark>(data.marks);marks.Sort((a,b)=>a.songTime.CompareTo(b.songTime));
        var errors=new List<string>();var placements=new List<Placement>();
        var previewWarnings=new List<string>();
        float baseGravity=runner.EffectiveGravity;
        RunMotionSegment[] motionSegments=RunChartMotion.Build(data,baseGravity,errors);
        float previousJump=-1000;
        float previousFlight=0f;
        BoxCollider playerBox=runner.GetComponent<BoxCollider>();
        float halfPlayer=Mathf.Abs(playerBox.size.z*playerBox.transform.lossyScale.z)*.5f;
        float gateHalf=.55f;
        if(gate!=null)
        {
            RunnerHazard hazard=gate.GetComponentInChildren<RunnerHazard>(true);
            BoxCollider beam=hazard!=null?hazard.GetComponent<BoxCollider>():null;
            if(beam!=null)gateHalf=Mathf.Abs(beam.size.z*beam.transform.lossyScale.z)*.5f;
        }
        Physics.SyncTransforms();
        foreach(RunChartMark mark in marks)
        {
            if(float.IsNaN(mark.songTime) || float.IsInfinity(mark.songTime) || mark.songTime<data.startOffsetSeconds || mark.songTime>chart.music.length)
            {errors.Add("存在歌曲范围外的标记。");continue;}
            float elapsed=mark.songTime-data.startOffsetSeconds;
            float scale=RunChartMotion.ScaleAt(motionSegments,mark.songTime);
            float apex=Mathf.Sqrt(2*data.jumpHeight/baseGravity)/scale,flight=2*apex;
            if(mark.type==RunChartMarkType.Jump)
            {
                if(spikes==null)errors.Add("缺少 Red Spike Practice 模板。");
                if(elapsed<.25f)errors.Add($"{mark.songTime:F2}s：起跑后需先留0.25秒落地。");
                if(mark.songTime-previousJump<previousFlight+RunChartMotion.SafetySeconds)errors.Add($"{mark.songTime:F2}s：上一次跳跃还没安全落地，需间隔 {previousFlight+RunChartMotion.SafetySeconds:F2} 秒。");
                previousJump=mark.songTime;
                previousFlight=flight;
            }
            else if(mark.type==RunChartMarkType.StayGrounded)
            {
                if(gate==null)errors.Add("缺少 Hanging Beat Gate 模板。");
                float gateMargin=(gateHalf+halfPlayer)/(data.forwardSpeed*scale)+.06f;
                foreach(RunChartMark jump in marks)
                    if(jump.type==RunChartMarkType.Jump && mark.songTime+gateMargin>jump.songTime &&
                        mark.songTime-gateMargin<jump.songTime+2*Mathf.Sqrt(2*data.jumpHeight/baseGravity)/RunChartMotion.ScaleAt(motionSegments,jump.songTime))
                        errors.Add($"{mark.songTime:F2}s：悬挂障碍与 {jump.songTime:F2}s 的跳跃轨迹冲突。");
            }
            else if(mark.type==RunChartMarkType.BoostStart || mark.type==RunChartMarkType.BoostEnd)
            {
                foreach(RunChartMark jump in marks)
                    if(jump.type==RunChartMarkType.Jump && jump.songTime<mark.songTime &&
                        mark.songTime<jump.songTime+2*Mathf.Sqrt(2*data.jumpHeight/baseGravity)/RunChartMotion.ScaleAt(motionSegments,jump.songTime)+.04f)
                    {
                        string warning=$"{mark.songTime:F2}s：光门位于 {jump.songTime:F2}s 跳跃的空中阶段，会改变这次跳跃的后半段轨迹。";
                        if(allowAirSwitch)previewWarnings.Add(warning);
                        else errors.Add(warning+" 请调整时间，或使用试玩预览按钮。");
                    }
            }
            else {errors.Add("未知标记类型。");continue;}
            float travel=RunChartMotion.DistanceAt(motionSegments,data.startOffsetSeconds,data.forwardSpeed,mark.songTime+(mark.type==RunChartMarkType.Jump?apex:0));
            float distance=travel;
            Vector3 forward=Vector3.forward;
            Vector3 pos=runner.transform.position+Vector3.forward*travel;
            if(!Physics.Raycast(pos+Vector3.up*10,Vector3.down,out RaycastHit hit,30,runner.groundMask,QueryTriggerInteraction.Ignore))
            {errors.Add($"{mark.songTime:F2}s：对应位置没有 Ground 道路。");continue;}
            pos.y=hit.point.y;placements.Add(new Placement{mark=mark,position=pos,forward=forward,distance=distance});
        }
        var hazards=placements.FindAll(p=>p.mark.type==RunChartMarkType.Jump || p.mark.type==RunChartMarkType.StayGrounded);
        hazards.Sort((a,b)=>a.distance.CompareTo(b.distance));
        for(int i=1;i<hazards.Count;i++)
        {
            Placement a=hazards[i-1],b=hazards[i];
            float halfA=a.mark.type==RunChartMarkType.Jump?.42f:gateHalf;
            float halfB=b.mark.type==RunChartMarkType.Jump?.42f:gateHalf;
            if(b.distance-a.distance<halfA+halfB+.1f)
                errors.Add($"{a.mark.songTime:F2}s 与 {b.mark.songTime:F2}s：障碍位置重叠，请移开其中一个标记。");
        }
        if(errors.Count>0)
        {
            string message=string.Join("\n",errors);Debug.LogWarning("谱面未生成：\n"+message);
            EditorUtility.DisplayDialog("需要调整谱面",message,"知道了");return false;
        }
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Generate Rhythm Chart");
        if(old!=null)Undo.DestroyObjectImmediate(old);
        GameObject generated=new GameObject("Generated Rhythm Obstacles");SceneManager.MoveGameObjectToScene(generated,runner.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(generated,"Create Chart Obstacles");
        RunnerRhythmMotion rhythm=runner.GetComponent<RunnerRhythmMotion>();
        if(rhythm==null)rhythm=Undo.AddComponent<RunnerRhythmMotion>(runner.gameObject);
        Undo.RecordObject(rhythm,"Apply Rhythm Motion");rhythm.baseSpeed=data.forwardSpeed;
        rhythm.baseGravityMultiplier=runner.gravityMultiplier;rhythm.baseGravity=baseGravity;rhythm.segments=motionSegments;
        BerserkVisualSetup.Configure(runner);
        NeoCitySequence city=null;
        foreach(GameObject sceneRoot in runner.gameObject.scene.GetRootGameObjects())
            if(sceneRoot.GetComponent<NeoCitySequence>()!=null)city=sceneRoot.GetComponent<NeoCitySequence>();
        int transitionHidden=0;
        foreach(Placement p in placements)
        {
            if(p.mark.type==RunChartMarkType.BoostStart || p.mark.type==RunChartMarkType.BoostEnd)
            {BuildSpeedGate(generated.transform,p);continue;}
            GameObject source=p.mark.type==RunChartMarkType.Jump?spikes:gate;
            GameObject obstacle=Object.Instantiate(source,generated.transform);
            obstacle.name=$"{p.mark.type} @ {p.mark.songTime:F3}s";
            obstacle.transform.SetPositionAndRotation(p.position,Quaternion.LookRotation(p.forward,Vector3.up));obstacle.SetActive(true);
            if(p.mark.type==RunChartMarkType.StayGrounded)RhythmRewardBuilder.Attach(obstacle.transform,runner);
            if(city!=null)
            {
                float safeStart=RunChartMotion.DistanceAt(motionSegments,data.startOffsetSeconds,data.forwardSpeed,city.transitionSongTime-1.3f)-1.5f;
                float safeEnd=RunChartMotion.DistanceAt(motionSegments,data.startOffsetSeconds,data.forwardSpeed,city.transitionSongTime+2.1f)+1.5f;
                if(p.distance>safeStart && p.distance<safeEnd)
                {obstacle.SetActive(false);transitionHidden++;}
            }
        }
        foreach(GameObject template in new[]{spikes,gate})if(template!=null){Undo.RecordObject(template,"Hide Practice Template");template.SetActive(false);}
        Undo.RecordObject(runner,"Apply Chart Run Parameters");runner.forwardSpeed=data.forwardSpeed;runner.jumpHeight=data.jumpHeight;
        Undo.RecordObject(runner.musicClock,"Apply Chart Music");runner.musicClock.music=chart.music;runner.musicClock.startOffsetSeconds=data.startOffsetSeconds;
        if(city!=null && city.citySections.Length>0 && city.citySections[0]!=null)
        {
            float expected=runner.transform.position.z+RunChartMotion.DistanceAt(motionSegments,data.startOffsetSeconds,data.forwardSpeed,city.transitionSongTime);
            if(Mathf.Abs(city.citySections[0].position.z-expected)>.1f)NeoCityBuilder.Build();
        }
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);Selection.activeGameObject=generated;
        if(transitionHidden>0)Debug.Log($"转场安全区内 {transitionHidden} 个障碍已隐藏，谱面原始标记仍保留。");
        if(previewWarnings.Count>0)Debug.LogWarning("已按原始标记生成试玩预览（未修改谱面）：\n"+string.Join("\n",previewWarnings),generated);
        foreach(RunMotionSegment segment in motionSegments)
            Debug.Log($"加速段 {segment.start:F2}～{segment.end:F2}s：跑速 {data.forwardSpeed*segment.scale:F1}，重力倍率 {rhythm.baseGravityMultiplier*segment.scale*segment.scale:F2}，滞空约 {2*Mathf.Sqrt(2*data.jumpHeight/baseGravity)/segment.scale:F2}s。");
        Debug.Log($"生成完成：{placements.Count} 个障碍。旧练习组已隐藏，可撤销。请保存场景后正常 Play 测试。",generated);
        return true;
    }

    static void BuildSpeedGate(Transform parent,Placement placement)
    {
        bool start=placement.mark.type==RunChartMarkType.BoostStart;
        GameObject gate=new GameObject((start?"X BOOST":"C NORMAL")+$" @ {placement.mark.songTime:F3}s");
        gate.transform.SetParent(parent);gate.transform.SetPositionAndRotation(placement.position,Quaternion.identity);
        SpeedGateBuilder.Build(gate.transform,start);
    }
}
