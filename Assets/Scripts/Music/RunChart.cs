using System;
using System.Collections.Generic;
using UnityEngine;

// 保持旧值0、1不变，已有谱面仍然兼容。
public enum RunChartMarkType { Jump, StayGrounded, BoostStart, BoostEnd }

[Serializable]
public class RunChartMark
{
    public RunChartMarkType type;
    public float songTime;
    public RunChartMark(RunChartMarkType type, float time) { this.type = type; songTime = time; }
}

[Serializable]
public class RunChartRecording
{
    public float startOffsetSeconds;
    public float forwardSpeed;
    public float jumpHeight;
    public float gravity;
    public Vector3 spawnPosition;
    public float endSongTime;
    public List<RunChartMark> marks = new List<RunChartMark>();
}

[CreateAssetMenu(menuName="PulseRun/Rhythm Chart")]
public class RunChart : ScriptableObject
{
    public AudioClip music;
    public RunChartRecording recording = new RunChartRecording();
}
