using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RunMusicClock : MonoBehaviour
{
    [Header("音乐")]
    public AudioClip music;
    [Min(0f)] public float startOffsetSeconds;
    [Min(.05f)] public float scheduleLeadSeconds = .15f;
    [Range(0f,1f)] public float volume = .65f;

    [Header("谱面校准（0 表示 BPM 尚未确认）")]
    [Min(0f)] public float bpm;
    public float firstBeatSeconds;
    public bool showTiming = true;

    private AudioSource source;
    private Coroutine preparing;
    private double scheduledStart;
    private float actualOffset;
    private bool scheduled;
    public event System.Action RunStopped;

    public bool IsRunning => scheduled && AudioSettings.dspTime >= scheduledStart &&
        source != null && source.isPlaying;
    public double SongTime => !scheduled ? actualOffset :
        System.Math.Min(music.length, actualOffset + System.Math.Max(0, AudioSettings.dspTime - scheduledStart));
    public double BeatPosition => bpm > 0f ? (SongTime - firstBeatSeconds) * bpm / 60.0 : 0.0;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.Stop();
    }

    private void Start() { RestartRun(); }

    public void StopRun()
    {
        if (preparing != null) { StopCoroutine(preparing); preparing = null; }
        scheduled = false;
        if (source != null) source.Stop();
        RunStopped?.Invoke();
    }

    public void RestartRun()
    {
        StopRun();
        if (music == null)
        {
            Debug.LogError("RunMusicClock 没有绑定音乐。", this);
            return;
        }
        preparing = StartCoroutine(PrepareAndSchedule());
    }

    private IEnumerator PrepareAndSchedule()
    {
        if (music.loadState == AudioDataLoadState.Unloaded) music.LoadAudioData();
        while (music.loadState == AudioDataLoadState.Loading) yield return null;
        if (music.loadState != AudioDataLoadState.Loaded)
        {
            Debug.LogError("音乐数据加载失败，玩家保持等待。",this);
            preparing = null;
            yield break;
        }
        source.clip = music;
        source.volume = volume;
        int sample = Mathf.Clamp(Mathf.RoundToInt(startOffsetSeconds * music.frequency),0,music.samples-1);
        source.timeSamples = sample;
        actualOffset = (float)sample / music.frequency;
        scheduledStart = AudioSettings.dspTime + Mathf.Max(.05f,scheduleLeadSeconds);
        source.PlayScheduled(scheduledStart);
        scheduled = true;
        preparing = null;
    }

    private void OnDisable() { StopRun(); }

    private void OnGUI()
    {
        if (!showTiming) return;
        string timing = scheduled ? $"Music: {SongTime:F2}s" : "Music: waiting / retry";
        string beat = bpm > 0f ? $"Beat: {BeatPosition:F2}" : "BPM: not calibrated";
        GUI.Box(new Rect(12,12,240,55),timing + "\n" + beat);
    }
}
