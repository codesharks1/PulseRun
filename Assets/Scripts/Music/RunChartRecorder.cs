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
    private readonly List<GameObject> hidden = new List<GameObject>();
    private bool musicStarted;

    public void Begin(RunnerController target)
    {
        runner=target;Clock=runner.musicClock;respawn=runner.GetComponent<RunnerRespawn>();body=runner.GetComponent<Rigidbody>();
        Recording=new RunChartRecording
        {
            startOffsetSeconds=Clock.music!=null ? (float)Mathf.Clamp(Mathf.RoundToInt(Clock.startOffsetSeconds*Clock.music.frequency),0,Clock.music.samples-1)/Clock.music.frequency : Clock.startOffsetSeconds, forwardSpeed=runner.forwardSpeed,
            jumpHeight=runner.jumpHeight,gravity=runner.EffectiveGravity,spawnPosition=body.position
        };
        runnerEnabled=runner.enabled;respawnEnabled=respawn!=null && respawn.enabled;kinematic=body.isKinematic;
        velocity=body.linearVelocity;angularVelocity=body.angularVelocity;
        runner.enabled=false;if(respawn!=null)respawn.enabled=false;
        if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        body.isKinematic=true;
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
        if(keyboard.backspaceKey.wasPressedThisFrame && Recording.marks.Count>0)
            Recording.marks.RemoveAt(Recording.marks.Count-1);
        if(!Clock.IsRunning)return;
        if(keyboard.spaceKey.wasPressedThisFrame)Add(RunChartMarkType.Jump);
        if(keyboard.hKey.wasPressedThisFrame)Add(RunChartMarkType.StayGrounded);
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
        IsRecording=false;Finished=true;Clock.StopRun();
        // Stay frozen until Play mode exits, so no unintended gameplay occurs after recording.
    }
    private void OnDisable()
    {
        if(runner==null || restored)return;
        restored=true;
        if(Clock!=null)Clock.StopRun();
        foreach(GameObject obj in hidden)if(obj!=null)obj.SetActive(true);
        if(body!=null)
        {
            body.isKinematic=kinematic;
            if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angularVelocity;}
        }
        if(respawn!=null)respawn.enabled=respawnEnabled;
        runner.enabled=runnerEnabled;
    }
    private void OnGUI()
    {
        if(Recording==null)return;
        GUI.Box(new Rect(12,75,420,105),$"CHART RECORDING - {Clock.SongTime:F2}s\nSpace = jump | H = pass below gate\nBackspace = undo | Enter = finish\nMarks: {Recording.marks.Count}");
    }
}
