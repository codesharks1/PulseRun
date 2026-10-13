using System;
using System.Collections.Generic;

// 非破坏式续录：只替换指定的歌曲时间区间，旧谱面对象始终不改。
public static class RunChartRecordingEdit
{
    public static RunChartRecording Copy(RunChartRecording source)
    {
        var result=new RunChartRecording
        {
            startOffsetSeconds=source.startOffsetSeconds,forwardSpeed=source.forwardSpeed,jumpHeight=source.jumpHeight,
            gravity=source.gravity,spawnPosition=source.spawnPosition,endSongTime=source.endSongTime
        };
        foreach(RunChartMark mark in source.marks)
            if(mark!=null)result.marks.Add(new RunChartMark(mark.type,mark.songTime));
        return result;
    }

    public static RunChartRecording ReplaceWindow(RunChartRecording source,IEnumerable<RunChartMark> recorded,float start,float end)
    {
        if(end<=start)return Copy(source);
        RunChartRecording result=Copy(source);
        result.marks.RemoveAll(mark=>mark.songTime>=start && mark.songTime<end);
        foreach(RunChartMark mark in recorded)
            if(mark!=null && mark.songTime>=start && mark.songTime<end)
                result.marks.Add(new RunChartMark(mark.type,mark.songTime));
        result.marks.Sort((a,b)=>a.songTime.CompareTo(b.songTime));
        result.endSongTime=Math.Max(source.endSongTime,end);
        return result;
    }
}
