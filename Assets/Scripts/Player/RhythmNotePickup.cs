using UnityEngine;

// 悬挂障碍下的奖励：落地触碰一次，播放声音和小爆闪；重试恢复。
[RequireComponent(typeof(BoxCollider), typeof(AudioSource))]
public class RhythmNotePickup : MonoBehaviour
{
    public RunnerController runner;
    public Transform noteVisual;
    public Transform burstVisual;
    public AudioClip pickupSound;
    [Range(0f,1f)] public float soundVolume=.25f;
    private BoxCollider trigger;
    private AudioSource source;
    private AudioClip generatedSound;
    private RunMusicClock clock;
    private Vector3 restPosition;
    private bool collected;
    private float collectedAt;

    void Awake()
    {
        trigger=GetComponent<BoxCollider>();trigger.isTrigger=true;
        source=GetComponent<AudioSource>();source.playOnAwake=false;
        source.loop=false;source.spatialBlend=0f;source.volume=1f;
        if(noteVisual!=null)restPosition=noteVisual.localPosition;
        if(pickupSound==null)generatedSound=CreateBling();
    }
    void OnEnable()
    {
        clock=runner!=null?runner.musicClock:null;
        if(clock!=null)clock.RunStopped+=ResetPickup;
        ResetPickup();
    }
    void Update()
    {
        if(!collected && noteVisual!=null)
        {
            float time=clock!=null && clock.IsRunning?(float)clock.SongTime:0f;
            noteVisual.localPosition=restPosition+Vector3.up*(Mathf.Sin(time*4f)*.025f);
            noteVisual.localRotation=Quaternion.Euler(0f,Mathf.Sin(time*2f)*25f,0f);
        }
        if(collected && burstVisual!=null && burstVisual.gameObject.activeSelf)
        {
            float progress=Mathf.Clamp01((Time.time-collectedAt)/.22f);
            for(int i=0;i<burstVisual.childCount;i++)
            {
                Transform spark=burstVisual.GetChild(i);
                float angle=i*Mathf.PI*2f/burstVisual.childCount;
                spark.localPosition=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f)*Mathf.Lerp(.13f,.55f,progress);
                spark.localScale=Vector3.one*(.085f*(1f-progress));
            }
            if(progress>=1f)burstVisual.gameObject.SetActive(false);
        }
    }
    void OnTriggerEnter(Collider other){TryCollect(other);}
    void OnTriggerStay(Collider other){TryCollect(other);}
    void TryCollect(Collider other)
    {
        if(collected || runner==null || !runner.isActiveAndEnabled)return;
        if(clock!=null && !clock.IsRunning)return;
        if(other.GetComponentInParent<RunnerController>()!=runner || !runner.IsGrounded)return;
        collected=true;collectedAt=Time.time;trigger.enabled=false;
        if(noteVisual!=null)noteVisual.gameObject.SetActive(false);
        if(burstVisual!=null)burstVisual.gameObject.SetActive(true);
        AudioClip sound=pickupSound!=null?pickupSound:generatedSound;
        if(sound!=null)source.PlayOneShot(sound,soundVolume);
    }
    public void ResetPickup()
    {
        collected=false;
        if(trigger!=null)trigger.enabled=true;
        if(source!=null)source.Stop();
        if(noteVisual!=null)
        {
            noteVisual.gameObject.SetActive(true);
            noteVisual.localPosition=restPosition;noteVisual.localRotation=Quaternion.identity;
        }
        if(burstVisual!=null)burstVisual.gameObject.SetActive(false);
    }
    void OnDisable()
    {
        if(clock!=null)clock.RunStopped-=ResetPickup;
        ResetPickup();
    }
    void OnDestroy(){if(generatedSound!=null)Destroy(generatedSound);}

    static AudioClip CreateBling()
    {
        const int rate=44100;const float duration=.24f;
        float[] samples=new float[Mathf.CeilToInt(rate*duration)];
        for(int i=0;i<samples.Length;i++)
        {
            float t=(float)i/rate;
            float attack=Mathf.Clamp01(t/.004f);
            float end=Mathf.Clamp01((duration-t)/.025f);
            // 两个明亮音高，快速衰减，避免尖锐点击和拖尾盖住下一拍。
            float bell=Mathf.Sin(2f*Mathf.PI*1567.98f*t)*Mathf.Exp(-t*20f)*.55f+
                Mathf.Sin(2f*Mathf.PI*2093f*t)*Mathf.Exp(-t*27f)*.28f+
                Mathf.Sin(2f*Mathf.PI*3135.96f*t)*Mathf.Exp(-t*40f)*.12f;
            samples[i]=bell*attack*end;
        }
        AudioClip clip=AudioClip.Create("PulseRun soft bling",samples.Length,1,rate,false);
        clip.hideFlags=HideFlags.DontSave;clip.SetData(samples,0);return clip;
    }
}
