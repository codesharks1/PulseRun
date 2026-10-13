using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(RunnerController))]
public class RunnerRhythmMotion : MonoBehaviour
{
    public float baseSpeed = 8f;
    public float baseGravityMultiplier = 2f;
    public float baseGravity = 19.62f;
    public RunMotionSegment[] segments = new RunMotionSegment[0];
    public bool backgroundBlur = true;
    private RunnerController runner;
    private Volume volume;
    private VolumeProfile profile;
    private UniversalAdditionalCameraData cameraData;
    private bool previousDepth;
    private float blurTarget;
    private DepthOfField boostBlur;

    void Awake()
    {
        runner = GetComponent<RunnerController>();
        if(Mathf.Abs(Mathf.Abs(Physics.gravity.y)*baseGravityMultiplier-baseGravity)>.01f)
            Debug.LogWarning("重力已改变，请重新生成谱面以校准加速段位置。",this);
        CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>();
        if (follow != null && follow.target == transform)
        {
            Camera camera = follow.GetComponent<Camera>();
            if (camera != null)
            {
                cameraData = camera.GetUniversalAdditionalCameraData();
                previousDepth=cameraData.requiresDepthTexture;cameraData.requiresDepthTexture=true;
            }
        }
        volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=25f;volume.weight=0f;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.hideFlags=HideFlags.DontSave;
        boostBlur=profile.Add<DepthOfField>(true);
        boostBlur.mode.Override(DepthOfFieldMode.Off);
        boostBlur.gaussianStart.Override(35f);boostBlur.gaussianEnd.Override(95f);boostBlur.gaussianMaxRadius.Override(.65f);
        volume.sharedProfile=profile;
        ClearBlurImmediately();
    }

    void OnEnable()
    {
        if(runner!=null && runner.musicClock!=null)runner.musicClock.RunStopped+=ResetForRespawn;
        if(cameraData!=null)cameraData.requiresDepthTexture=true;
    }

    void FixedUpdate()
    {
        if (runner == null) return;
        bool playing=runner.enabled && runner.musicClock != null && runner.musicClock.IsRunning;
        float time=runner.musicClock != null ? (float)runner.musicClock.SongTime : 0f;
        float scale=playing ? RunChartMotion.ScaleAt(segments,time) : 1f;
        runner.forwardSpeed=baseSpeed*scale;
        runner.gravityMultiplier=baseGravityMultiplier*scale*scale;
        blurTarget=backgroundBlur && playing && scale>1f ? .7f : 0f;
        if(blurTarget<=0f)ClearBlurImmediately();
        else if(volume!=null)
        {volume.enabled=true;boostBlur.active=true;boostBlur.mode.Override(DepthOfFieldMode.Gaussian);}
    }

    void LateUpdate()
    {
        if(runner==null || !runner.enabled || runner.musicClock==null || !runner.musicClock.IsRunning ||
            !backgroundBlur || RunChartMotion.ScaleAt(segments,(float)runner.musicClock.SongTime)<=1f)
        {ClearBlurImmediately();return;}
        if(volume!=null && volume.enabled)volume.weight=Mathf.Lerp(volume.weight,blurTarget,1f-Mathf.Exp(-8f*Time.deltaTime));
    }

    public float DistanceAt(float time)
    {
        RunnerController target=runner!=null?runner:GetComponent<RunnerController>();
        float offset=target!=null && target.musicClock!=null ? target.musicClock.startOffsetSeconds : 0f;
        return RunChartMotion.DistanceAt(segments,offset,baseSpeed,time);
    }

    void OnDisable()
    {
        if(runner!=null && runner.musicClock!=null)runner.musicClock.RunStopped-=ResetForRespawn;
        ResetForRespawn();
        if(cameraData!=null)cameraData.requiresDepthTexture=previousDepth;
    }
    public void ResetForRespawn()
    {
        if(runner!=null){runner.forwardSpeed=baseSpeed;runner.gravityMultiplier=baseGravityMultiplier;}
        ClearBlurImmediately();
    }
    private void ClearBlurImmediately()
    {
        blurTarget=0f;
        if(volume!=null){volume.weight=0f;volume.enabled=false;}
        if(boostBlur!=null){boostBlur.mode.Override(DepthOfFieldMode.Off);boostBlur.active=false;}
    }
    void OnDestroy()
    {
        if(profile==null)return;
        foreach(VolumeComponent component in profile.components)Destroy(component);
        Destroy(profile);
    }
}
