using UnityEngine;

// 按实际跑过的位置染色，不依赖固定跑速，因此也支持加速段。
public class RunwayLightTrail : MonoBehaviour
{
    public RunnerController runner;
    public Renderer[] strips;
    // HDR 颜色面板提供 Intensity；保留现有颜色值，不自动提高亮度。
    [ColorUsage(false,true)] public Color idleColor=new Color(1.5f,.08f,.7f,1f);
    [ColorUsage(false,true)] public Color passedColor=new Color(1.8f,.9f,.08f,1f);
    [Min(1f)] public float spacing=4f;
    [Min(.1f)] public float dashLength=.9f;
    public float markerOrigin;
    private MaterialPropertyBlock properties;
    private float spawnZ,passedZ;
    private bool wasRunning;

    void Awake()
    {
        properties=new MaterialPropertyBlock();
        spawnZ=runner!=null?runner.transform.position.z:markerOrigin;
        ResetProgress();
    }
    void OnEnable()
    {
        if(runner!=null && runner.musicClock!=null)runner.musicClock.RunStopped+=ResetProgress;
    }
    void LateUpdate()
    {
        if(runner==null)return;
        bool running=runner.isActiveAndEnabled && (runner.musicClock==null || runner.musicClock.IsRunning);
        if(!running)
        {
            if(wasRunning)ResetProgress();
            wasRunning=false;return;
        }
        wasRunning=true;
        passedZ=Mathf.Max(passedZ,runner.transform.position.z);
        Apply();
    }
    public void ResetProgress()
    {
        passedZ=spawnZ-.6f;
        wasRunning=false;Apply();
    }
    private void Apply()
    {
        if(properties==null || strips==null)return;
        properties.SetFloat("_ProgressZ",passedZ);
        properties.SetFloat("_OriginZ",markerOrigin);
        properties.SetFloat("_Period",Mathf.Max(1f,spacing));
        properties.SetFloat("_DashLength",Mathf.Min(dashLength,spacing*.8f));
        properties.SetColor("_IdleColor",idleColor);properties.SetColor("_PassedColor",passedColor);
        foreach(Renderer strip in strips)if(strip!=null)strip.SetPropertyBlock(properties);
    }
    void OnDisable()
    {
        if(runner!=null && runner.musicClock!=null)runner.musicClock.RunStopped-=ResetProgress;
        ResetProgress();
    }
}
