using System;
using System.Collections.Generic;

// 同一份分段数据同时用于运行时移动和编辑器障碍位置计算。
[Serializable]
public class RunMotionSegment
{
    public float start, end, scale;
}

public static class RunChartMotion
{
    public const float SafetySeconds = .08f;
    public const float MaximumScale = 3f;

    public static RunMotionSegment[] Build(RunChartRecording data,float gravity,List<string> errors)
    {
        var marks = new List<RunChartMark>(data.marks);
        if(marks.RemoveAll(mark=>mark==null)>0)errors.Add("谱面存在空标记。");
        marks.Sort((a,b)=>a.songTime.CompareTo(b.songTime));
        var result = new List<RunMotionSegment>();
        float start = -1f;
        foreach (RunChartMark mark in marks)
        {
            if (mark == null || float.IsNaN(mark.songTime) || float.IsInfinity(mark.songTime))
            { errors.Add("谱面存在无效标记。"); continue; }
            if (mark.type == RunChartMarkType.BoostStart)
            {
                if (start >= 0f) errors.Add($"{mark.songTime:F2}s：连续按了X，请先用C结束上一段。");
                else start = mark.songTime;
            }
            if (mark.type == RunChartMarkType.BoostEnd)
            {
                if (start < 0f) { errors.Add($"{mark.songTime:F2}s：C之前没有X。"); continue; }
                if (mark.songTime-start < .25f) errors.Add("加速段至少留0.25秒。");
                result.Add(new RunMotionSegment{start=start,end=mark.songTime,scale=1.25f}); start=-1f;
            }
        }
        if (start >= 0f) errors.Add("最后一个X没有对应的C，请结束加速段再保存。");
        double baseFlight = 2*Math.Sqrt(2*data.jumpHeight/gravity);
        foreach (RunMotionSegment segment in result)
        {
            float previousJump = -1f;
            double targetFlight = baseFlight/1.25;
            foreach (RunChartMark mark in marks)
            {
                if (mark.songTime < segment.start || mark.songTime >= segment.end) continue;
                if (mark.type == RunChartMarkType.Jump)
                {
                    if (previousJump >= 0f) targetFlight = Math.Min(targetFlight,mark.songTime-previousJump-SafetySeconds);
                    previousJump=mark.songTime;
                }
                if (mark.type == RunChartMarkType.StayGrounded && previousJump >= 0f)
                    targetFlight=Math.Min(targetFlight,mark.songTime-previousJump-1.05/data.forwardSpeed-.06);
            }
            if (previousJump >= 0f) targetFlight=Math.Min(targetFlight,segment.end-previousJump-SafetySeconds);
            double required = targetFlight > 0 ? baseFlight/targetFlight : double.PositiveInfinity;
            if (required > MaximumScale)
                errors.Add($"{segment.start:F2}～{segment.end:F2}s：标记过密，即使3倍加速也无法安全落地。请拉开跳跃、H或C。");
            segment.scale=(float)Math.Min(MaximumScale,Math.Max(1.25,required));
        }
        return result.ToArray();
    }

    public static float ScaleAt(RunMotionSegment[] segments,float time)
    {
        if (segments != null) foreach (RunMotionSegment segment in segments)
            if (time >= segment.start && time < segment.end) return segment.scale;
        return 1f;
    }

    public static float DistanceAt(RunMotionSegment[] segments,float offset,float speed,float time)
    {
        double distance = Math.Max(0,time-offset)*speed;
        if (segments != null) foreach (RunMotionSegment segment in segments)
        {
            double overlap=Math.Max(0,Math.Min(time,segment.end)-Math.Max(offset,segment.start));
            distance += overlap*speed*(segment.scale-1f);
        }
        return (float)distance;
    }
}
