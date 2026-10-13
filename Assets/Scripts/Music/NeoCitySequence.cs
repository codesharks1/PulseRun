using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 只管理场景表现，不参与玩家移动、跳跃或死亡判定。
public class NeoCitySequence : MonoBehaviour
{
    public RunMusicClock musicClock;
    public RunnerController runner;
    public Camera gameCamera;
    public float transitionSongTime = 23f;
    [Range(0f, .5f)] public float flashStrength = .28f;
    [Header("出隧道后的轻微残像（可关闭）")]
    public bool enableAfterimage = true;
    [Range(0f, .1f)] public float afterimageStrength = .055f;
    [Range(.3f, 1.5f)] public float afterimageDuration = 1.1f;
    public Transform[] citySections;
    public Transform[] traffic;
    public float builtSpeed;
    public float builtOffset;
    public float sectionLength = 100f;
    public Renderer exitGlow;
    public Light exitLight;
    [System.Serializable]
    public struct TrafficRoute
    {
        public Transform car;
        public Vector3 from, to;
        public float speed, phase;
    }
    public TrafficRoute[] lowerTraffic = new TrafficRoute[0];

    private Vector3[] carOrigins;
    private float initialFieldOfView;
    private float flash;
    private bool cameraCaptured;
    private MaterialPropertyBlock glowProperties;
    private Volume exposureVolume;
    private VolumeProfile exposureProfile;
    private ColorAdjustments exposure;
    private Texture2D afterimage;
    private Coroutine afterimageCapture;
    private bool capturedThisRun;
    private float afterimageSongTime;

    void Awake()
    {
        carOrigins = new Vector3[traffic.Length];
        for (int i = 0; i < traffic.Length; i++)
            if (traffic[i] != null) carOrigins[i] = traffic[i].localPosition;
        if (gameCamera != null)
        { initialFieldOfView = gameCamera.fieldOfView; cameraCaptured = true; }
        glowProperties = new MaterialPropertyBlock();
        exposureVolume = gameObject.AddComponent<Volume>();
        exposureVolume.isGlobal = true; exposureVolume.priority = 100f; exposureVolume.weight = 0f;
        exposureProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        exposureProfile.hideFlags = HideFlags.DontSave;
        // 只覆盖曝光，不用默认白色ColorFilter冲掉狂暴段的紫色调色。
        exposure = exposureProfile.Add<ColorAdjustments>(false);
        exposure.postExposure.Override(.65f);
        var aberration = exposureProfile.Add<ChromaticAberration>(true);
        aberration.intensity.Override(.1f);
        exposureVolume.sharedProfile = exposureProfile;
        // Awake先隐藏，避免第一帧提前看到城市；LateUpdate再按歌曲进度显现。
        SetCityVisibility(musicClock != null ? musicClock.startOffsetSeconds : 0f);
        UpdateExitGlow(0f);
        if (runner != null && musicClock != null &&
            (Mathf.Abs(runner.forwardSpeed-builtSpeed) > .01f || Mathf.Abs(musicClock.startOffsetSeconds-builtOffset) > .01f))
            Debug.LogWarning("跑速或音乐起始偏移已改变，请使用 PulseRun/Build Neo City Transition 重新定位城市入口。", this);
    }

    void LateUpdate()
    {
        bool playing = musicClock != null && musicClock.IsRunning && runner != null && runner.enabled;
        float time = musicClock != null ? (float)musicClock.SongTime : 0f;
        float relative = time-transitionSongTime;
        // 出口先渐亮，再短暂泛白，随后退光；不再只是瞬间闪一下。
        float rise = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-1f,0f,relative));
        float fall = 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,2f,relative));
        // 限制旧场景中遗留的高强度设置，避免未重建时仍然过曝。
        flash = playing ? rise*fall*Mathf.Min(flashStrength,.4f) : 0f;
        if (exposureVolume != null) exposureVolume.weight = playing ? rise*fall : 0f;
        UpdateExitGlow(playing ? Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-2f,0f,relative))*fall : 0f);
        if (!playing || relative < 0f || !enableAfterimage) ClearAfterimage();
        else if (!capturedThisRun && relative >= .6f && relative < 1.1f)
        { capturedThisRun = true; afterimageCapture = StartCoroutine(CaptureAfterimage()); }
        if (afterimage != null && time-afterimageSongTime >= afterimageDuration)
        { Destroy(afterimage); afterimage = null; }
        if (cameraCaptured)
        {
            float anticipation = playing ? Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-1.2f,0f,relative)) *
                (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0f,2f,relative))) : 0f;
            gameCamera.fieldOfView = initialFieldOfView+5f*anticipation;
        }
        SetCityVisibility(time);
        for (int i = 0; i < traffic.Length; i++)
        {
            if (traffic[i] == null || !traffic[i].gameObject.activeInHierarchy) continue;
            Vector3 position = carOrigins[i];
            float speed = (i%2 == 0 ? 1f : -1f)*(12f+i%3*3f);
            position.z = Mathf.Repeat(carOrigins[i].z+time*speed, sectionLength-8f)+4f;
            traffic[i].localPosition = position;
        }
        foreach (TrafficRoute route in lowerTraffic)
        {
            if (route.car == null || !route.car.gameObject.activeInHierarchy) continue;
            float distance = Vector3.Distance(route.from,route.to);
            float progress = Mathf.Repeat(time*route.speed/Mathf.Max(.1f,distance)+route.phase,1f);
            route.car.localPosition = Vector3.Lerp(route.from,route.to,progress);
        }
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        if (afterimage != null && enableAfterimage && musicClock != null && runner != null && runner.enabled && musicClock.IsRunning)
        {
            float elapsed = (float)musicClock.SongTime-afterimageSongTime;
            float fade = 1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(elapsed/Mathf.Max(.1f,afterimageDuration)));
            GUI.color = new Color(.9f,.96f,1f,fade*afterimageStrength);
            // 剪掉顶部调试UI；残像只有几像素偏移，不做强烈双重画面。
            float top = Screen.height*.12f;
            GUI.BeginGroup(new Rect(0,top,Screen.width,Screen.height-top));
            float offset = Mathf.Min(4f,Screen.width*.002f)*fade;
            GUI.DrawTexture(new Rect(offset,-top,Screen.width,Screen.height),afterimage);
            GUI.EndGroup();
        }
        if (flash > 0f)
        {
            GUI.color = new Color(.86f,.94f,1f,flash);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        }
        GUI.color = previous;
    }

    void OnDisable()
    {
        flash = 0f;
        ClearAfterimage();
        if (exitGlow != null) exitGlow.enabled = false;
        if (exitLight != null) exitLight.enabled = false;
        if (exposureVolume != null) exposureVolume.weight = 0f;
        if (cameraCaptured && gameCamera != null) gameCamera.fieldOfView = initialFieldOfView;
    }

    private void SetCityVisibility(float songTime)
    {
        if (runner == null || citySections == null) return;
        float z = runner.transform.position.z;
        bool revealed = songTime >= transitionSongTime-.05f;
        foreach (Transform section in citySections)
        {
            if (section == null) continue;
            bool visible = revealed && section.position.z+sectionLength >= z-75f && section.position.z <= z+360f;
            if (section.gameObject.activeSelf != visible) section.gameObject.SetActive(visible);
        }
    }

    private void UpdateExitGlow(float approach)
    {
        // 出口灯不再按歌曲某一秒突然开启；远处也有柔和的常驻基底。
        float alpha = Mathf.Lerp(.13f,.42f,approach);
        if (exitGlow != null)
        {
            exitGlow.enabled = true;
            glowProperties.SetColor("_BaseColor",new Color(2.6f,3.2f,3.8f,alpha));
            exitGlow.SetPropertyBlock(glowProperties);
        }
        if (exitLight != null)
        { exitLight.enabled = true; exitLight.intensity = Mathf.Lerp(.5f,3f,approach); }
    }

    private IEnumerator CaptureAfterimage()
    {
        yield return new WaitForEndOfFrame();
        if (musicClock != null && musicClock.IsRunning && runner != null && runner.enabled && enableAfterimage)
        {
            // 只取游戏画面作短暂残像，不保存到磁盘、不截取桌面。
            try
            {
                afterimage = ScreenCapture.CaptureScreenshotAsTexture();
                afterimageSongTime = (float)musicClock.SongTime;
            }
            catch (System.Exception exception)
            { Debug.LogWarning("残像采样不可用，已跳过；其他转场照常播放。"+exception.Message,this); }
        }
        afterimageCapture = null;
    }

    private void ClearAfterimage()
    {
        if (afterimageCapture != null) { StopCoroutine(afterimageCapture); afterimageCapture = null; }
        if (afterimage != null) { Destroy(afterimage); afterimage = null; }
        capturedThisRun = false;
    }

    void OnDestroy()
    {
        ClearAfterimage();
        if (exposureProfile == null) return;
        foreach (VolumeComponent component in exposureProfile.components) Destroy(component);
        Destroy(exposureProfile);
    }
}
