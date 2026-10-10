using System.Collections.Generic;
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
    RunChart selectedChart;
    Vector2 scroll;

    static RunChartWindow()
    {
        EditorApplication.playModeStateChanged+=OnPlayState;
        EditorApplication.update+=ObserveRecording;
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
            recorder.Begin(runner);Capture(recorder);
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
        EditorGUILayout.HelpBox("空格：起跳时间。H：通过悬挂障碍的时间。Backspace：撤销。Enter：结束录制并退出 Play。\n录制时玩家冻结，练习障碍暂时隐藏。请先保存当前场景。",MessageType.Info);
        EditorGUILayout.HelpBox("生成使用玩家当前的重力倍率；调整倍率后可选择原谱面重新生成，无需重新打点。跑速和跳高沿用谱面记录。",MessageType.Info);
        RunnerController runner=Runner();
        if(runner!=null && runner.followNeonRunway && runner.forwardSpeed>0)
        {
            float seconds=(NeonRunwayPath.EndDistance-NeonRunwayPath.FindDistance(runner.transform.position))/runner.forwardSpeed;
            EditorGUILayout.HelpBox($"当前道路最多约支持 {seconds:F1} 秒行程。建议先录10～15秒；可在 Music Timeline 设置 Start Offset Seconds 跳过前奏。",MessageType.Info);
        }
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || runner==null || runner.musicClock==null))
            if(GUILayout.Button("1. 开始录制（进入 Play）",GUILayout.Height(32)))
            {
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
        if(last!=null)
        {
            EditorGUILayout.LabelField($"本次录制：{last.marks.Count} 个标记，起始歌曲时间 {last.startOffsetSeconds:F2}s");
            scroll=EditorGUILayout.BeginScrollView(scroll);
            for(int i=0;i<last.marks.Count;i++)
                EditorGUILayout.LabelField($"{i+1}. {(last.marks[i].type==RunChartMarkType.Jump?"起跳":"保持落地")}    {last.marks[i].songTime:F3}s");
            EditorGUILayout.EndScrollView();
        }
    }
    void Save(RunChartRecording recording)
    {
        if(!AssetDatabase.IsValidFolder("Assets/Charts"))AssetDatabase.CreateFolder("Assets","Charts");
        RunChart chart=CreateInstance<RunChart>();chart.recording=JsonUtility.FromJson<RunChartRecording>(JsonUtility.ToJson(recording));
        chart.music=AssetDatabase.LoadAssetAtPath<AudioClip>(SessionState.GetString(MusicAsset,""));
        string path=AssetDatabase.GenerateUniqueAssetPath("Assets/Charts/NeonRainbowChart.asset");
        AssetDatabase.CreateAsset(chart,path);AssetDatabase.SaveAssets();selectedChart=chart;
        Debug.Log("谱面已保存："+path,chart);
    }

    class Placement{public RunChartMark mark;public Vector3 position,forward;public float distance;}
    public static void Generate(RunChart chart)
    {
        RunnerController runner=Runner();
        if(runner==null || runner.musicClock==null || chart.music==null){Debug.LogError("生成需要玩家、音乐时间轴和有效谱面音乐。");return;}
        RunChartRecording data=chart.recording;
        if(data==null || data.marks==null || data.marks.Count==0 || data.forwardSpeed<=0 || data.jumpHeight<=0 || data.gravity<=0)
        {Debug.LogError("谱面为空或移动参数无效。");return;}
        GameObject spikes=null,gate=null,old=null;
        foreach(GameObject root in runner.gameObject.scene.GetRootGameObjects())
        {
            if(root.name=="Red Spike Practice")spikes=root;
            if(root.name=="Hanging Beat Gate")gate=root;
            if(root.name=="Generated Rhythm Obstacles")old=root;
        }
        var marks=new List<RunChartMark>(data.marks);marks.Sort((a,b)=>a.songTime.CompareTo(b.songTime));
        float apex=Mathf.Sqrt(2*data.jumpHeight/runner.EffectiveGravity),flight=2*apex;
        var errors=new List<string>();var placements=new List<Placement>();
        float previousJump=-1000;
        float originDistance=runner.followNeonRunway?NeonRunwayPath.FindDistance(runner.transform.position):0;
        float lane=runner.followNeonRunway?Vector3.Dot(runner.transform.position-NeonRunwayPath.GetPoint(originDistance),Vector3.Cross(Vector3.up,NeonRunwayPath.GetForward(originDistance))):0;
        BoxCollider playerBox=runner.GetComponent<BoxCollider>();
        float halfPlayer=Mathf.Abs(playerBox.size.z*playerBox.transform.lossyScale.z)*.5f;
        float gateHalf=.55f;
        if(gate!=null)
        {
            RunnerHazard hazard=gate.GetComponentInChildren<RunnerHazard>(true);
            BoxCollider beam=hazard!=null?hazard.GetComponent<BoxCollider>():null;
            if(beam!=null)gateHalf=Mathf.Abs(beam.size.z*beam.transform.lossyScale.z)*.5f;
        }
        float gateMargin=(gateHalf+halfPlayer)/data.forwardSpeed+.06f;
        Physics.SyncTransforms();
        foreach(RunChartMark mark in marks)
        {
            if(float.IsNaN(mark.songTime) || float.IsInfinity(mark.songTime) || mark.songTime<data.startOffsetSeconds || mark.songTime>chart.music.length)
            {errors.Add("存在歌曲范围外的标记。");continue;}
            float elapsed=mark.songTime-data.startOffsetSeconds;
            if(mark.type==RunChartMarkType.Jump)
            {
                if(spikes==null)errors.Add("缺少 Red Spike Practice 模板。");
                if(elapsed<.25f)errors.Add($"{mark.songTime:F2}s：起跑后需先留0.25秒落地。");
                if(mark.songTime-previousJump<flight+.06f)errors.Add($"{mark.songTime:F2}s：两次起跳太近，至少间隔 {flight+.06f:F2} 秒。");
                previousJump=mark.songTime;
            }
            else
            {
                if(gate==null)errors.Add("缺少 Hanging Beat Gate 模板。");
                foreach(RunChartMark jump in marks)
                    if(jump.type==RunChartMarkType.Jump && mark.songTime+gateMargin>jump.songTime && mark.songTime-gateMargin<jump.songTime+flight)
                        errors.Add($"{mark.songTime:F2}s：悬挂障碍与 {jump.songTime:F2}s 的跳跃轨迹冲突。");
            }
            float travel=data.forwardSpeed*(elapsed+(mark.type==RunChartMarkType.Jump?apex:0));
            float distance=originDistance+travel;
            if(runner.followNeonRunway && distance+1>NeonRunwayPath.EndDistance)
            {errors.Add($"{mark.songTime:F2}s：超出现有跑道末端。");continue;}
            Vector3 forward=runner.followNeonRunway?NeonRunwayPath.GetForward(distance):Vector3.forward;
            Vector3 pos=runner.followNeonRunway?NeonRunwayPath.GetPoint(distance)+Vector3.Cross(Vector3.up,forward)*lane:runner.transform.position+Vector3.forward*travel;
            if(!Physics.Raycast(pos+Vector3.up*10,Vector3.down,out RaycastHit hit,30,runner.groundMask,QueryTriggerInteraction.Ignore))
            {errors.Add($"{mark.songTime:F2}s：对应位置没有 Ground 道路。");continue;}
            pos.y=hit.point.y;placements.Add(new Placement{mark=mark,position=pos,forward=forward,distance=distance});
        }
        placements.Sort((a,b)=>a.distance.CompareTo(b.distance));
        for(int i=1;i<placements.Count;i++)
        {
            Placement a=placements[i-1],b=placements[i];
            float halfA=a.mark.type==RunChartMarkType.Jump?.42f:gateHalf;
            float halfB=b.mark.type==RunChartMarkType.Jump?.42f:gateHalf;
            if(b.distance-a.distance<halfA+halfB+.1f)
                errors.Add($"{a.mark.songTime:F2}s 与 {b.mark.songTime:F2}s：障碍位置重叠，请移开其中一个标记。");
        }
        if(errors.Count>0)
        {
            string message=string.Join("\n",errors);Debug.LogWarning("谱面未生成：\n"+message);
            EditorUtility.DisplayDialog("需要调整谱面",message,"知道了");return;
        }
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Generate Rhythm Chart");
        if(old!=null)Undo.DestroyObjectImmediate(old);
        GameObject generated=new GameObject("Generated Rhythm Obstacles");SceneManager.MoveGameObjectToScene(generated,runner.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(generated,"Create Chart Obstacles");
        foreach(Placement p in placements)
        {
            GameObject source=p.mark.type==RunChartMarkType.Jump?spikes:gate;
            GameObject obstacle=Object.Instantiate(source,generated.transform);
            obstacle.name=$"{p.mark.type} @ {p.mark.songTime:F3}s";
            obstacle.transform.SetPositionAndRotation(p.position,Quaternion.LookRotation(p.forward,Vector3.up));obstacle.SetActive(true);
        }
        foreach(GameObject template in new[]{spikes,gate})if(template!=null){Undo.RecordObject(template,"Hide Practice Template");template.SetActive(false);}
        Undo.RecordObject(runner,"Apply Chart Run Parameters");runner.forwardSpeed=data.forwardSpeed;runner.jumpHeight=data.jumpHeight;
        Undo.RecordObject(runner.musicClock,"Apply Chart Music");runner.musicClock.music=chart.music;runner.musicClock.startOffsetSeconds=data.startOffsetSeconds;
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);Selection.activeGameObject=generated;
        Debug.Log($"生成完成：{placements.Count} 个障碍。旧练习组已隐藏，可撤销。请保存场景后正常 Play 测试。",generated);
    }
}
