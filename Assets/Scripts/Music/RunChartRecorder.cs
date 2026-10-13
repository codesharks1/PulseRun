using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Added only for the recording Play session by the editor window.
public class RunChartRecorder : MonoBehaviour
{
    public RunChartRecording Recording { get; private set; }
    public RunMusicClock Clock { get; private set; }
    public bool IsRecording { get; private set; }
    public bool Finished { get; private set; }
    private RunnerController runner;
    private RunnerRespawn respawn;
    private Rigidbody body;
    private bool runnerEnabled, respawnEnabled, kinematic, restored;
    private Vector3 velocity, angularVelocity;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private readonly List<GameObject> hidden = new List<GameObject>();
    private bool musicStarted;
    private RunChartRecording originalRecording;
    private float recordingFrom;
    private int lockedMarks;
    private AudioClip originalMusic;
    private float originalOffset;

    public void Begin(RunnerController target,RunChart seed=null,float resumeAt=0f)
    {
        runner=target;Clock=runner.musicClock;respawn=runner.GetComponent<RunnerRespawn>();body=runner.GetComponent<Rigidbody>();
        originalMusic=Clock.music;originalOffset=Clock.startOffsetSeconds;
        originalPosition=body.position;originalRotation=body.rotation;
        Recording=new RunChartRecording
        {
            startOffsetSeconds=Clock.music!=null ? (float)Mathf.Clamp(Mathf.RoundToInt(Clock.startOffsetSeconds*Clock.music.frequency),0,Clock.music.samples-1)/Clock.music.frequency : Clock.startOffsetSeconds, forwardSpeed=runner.forwardSpeed,
            jumpHeight=runner.jumpHeight,gravity=runner.EffectiveGravity,spawnPosition=body.position
        };
        if(seed!=null)
        {
            originalRecording=RunChartRecordingEdit.Copy(seed.recording);
            recordingFrom=Mathf.Clamp(resumeAt,seed.recording.startOffsetSeconds,seed.music.length-.01f);
            Clock.music=seed.music;Clock.startOffsetSeconds=recordingFrom;
            Recording=RunChartRecordingEdit.Copy(originalRecording);
            Recording.marks.RemoveAll(mark=>mark.songTime>=recordingFrom);
            lockedMarks=Recording.marks.Count;
        }
        else recordingFrom=Recording.startOffsetSeconds;
        runnerEnabled=runner.enabled;respawnEnabled=respawn!=null && respawn.enabled;kinematic=body.isKinematic;
        velocity=body.linearVelocity;angularVelocity=body.angularVelocity;
        runner.enabled=false;if(respawn!=null)respawn.enabled=false;
        if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        body.isKinematic=true;
        if(originalRecording!=null)
        {
            var warnings=new List<string>();
            RunMotionSegment[] segments=RunChartMotion.Build(originalRecording,runner.EffectiveGravity,warnings);
            float distance=RunChartMotion.DistanceAt(segments,originalRecording.startOffsetSeconds,originalRecording.forwardSpeed,recordingFrom);
            body.position=originalRecording.spawnPosition+Vector3.forward*distance;
            runner.transform.position=body.position;Physics.SyncTransforms();
            foreach(CameraFollow follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
                if(follow.target==runner.transform)follow.SnapToTarget();
            if(warnings.Count>0)Debug.LogWarning("原谱面分段存在问题，续录后的新谱面请重新检查："+string.Join("\n",warnings));
        }
        foreach(GameObject root in runner.gameObject.scene.GetRootGameObjects())
            if(root.activeSelf && (root.name=="Red Spike Practice" || root.name=="Hanging Beat Gate" || root.name=="Generated Rhythm Obstacles"))
            {hidden.Add(root);root.SetActive(false);}
        IsRecording=true;Finished=false;Clock.RestartRun();
    }
    private void Update()
    {
        if(!IsRecording)return;
        if(Clock.IsRunning)musicStarted=true;
        else if(musicStarted){Finish();return;}
        Keyboard keyboard=Keyboard.current;if(keyboard==null)return;
        if(keyboard.enterKey.wasPressedThisFrame){Finish();return;}
        if(keyboard.backspaceKey.wasPressedThisFrame && Recording.marks.Count>lockedMarks)
            Recording.marks.RemoveAt(Recording.marks.Count-1);
        if(!Clock.IsRunning)return;
        if(keyboard.spaceKey.wasPressedThisFrame)Add(RunChartMarkType.Jump);
        if(keyboard.hKey.wasPressedThisFrame)Add(RunChartMarkType.StayGrounded);
        if(keyboard.xKey.wasPressedThisFrame)Add(RunChartMarkType.BoostStart);
        if(keyboard.cKey.wasPressedThisFrame)Add(RunChartMarkType.BoostEnd);
    }
    private void Add(RunChartMarkType type)
    {
        Recording.marks.Add(new RunChartMark(type,(float)Clock.SongTime));
        Debug.Log($"谱面标记：{type} @ {Clock.SongTime:F3}s");
    }
    public void Finish()
    {
        if(!IsRecording)return;
        Recording.endSongTime=(float)Clock.SongTime;
        if(originalRecording!=null)
        {
            var added=Recording.marks.GetRange(lockedMarks,Recording.marks.Count-lockedMarks);
            Recording=RunChartRecordingEdit.ReplaceWindow(originalRecording,added,recordingFrom,Recording.endSongTime);
        }
        IsRecording=false;Finished=true;Clock.StopRun();
        // Stay frozen until Play mode exits, so no unintended gameplay occurs after recording.
    }
    private void OnDisable()
    {
        if(runner==null || restored)return;
        restored=true;
        if(Clock!=null){Clock.StopRun();Clock.music=originalMusic;Clock.startOffsetSeconds=originalOffset;}
        foreach(GameObject obj in hidden)if(obj!=null)obj.SetActive(true);
        if(body!=null)
        {
            body.position=originalPosition;body.rotation=originalRotation;
            runner.transform.SetPositionAndRotation(originalPosition,originalRotation);
            body.isKinematic=kinematic;
            if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angularVelocity;}
        }
        if(respawn!=null)respawn.enabled=respawnEnabled;
        runner.enabled=runnerEnabled;
    }
    private void OnGUI()
    {
        if(Recording==null)return;
        GUI.Box(new Rect(12,75,460,145),$"CHART RECORDING - {Clock.SongTime:F2}s\nSpace = jump | H = pass below gate\nX = boost start | C = boost end\nBackspace = undo | Enter = finish\nFrom: {recordingFrom:F2}s | Earlier locked marks: {lockedMarks}\nMarks: {Recording.marks.Count}");
    }
}
