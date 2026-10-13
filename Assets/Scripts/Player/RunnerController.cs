using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class RunnerController : MonoBehaviour
{
    [Header("移动")]
    public float forwardSpeed = 8f;
    public float jumpHeight = 1.5f;

    [Header("跳跃预输入（落地前提前按键）")]
    [Range(0f, 0.25f)] public float jumpBufferSeconds = 0.1f;

    [Header("起跳霓虹光圈")]
    public bool showJumpPulse = true;
    public Color jumpPulseColor = new Color(0.1f, 0.85f, 1f, 1f);
    [Range(0.08f, 0.4f)] public float jumpPulseDuration = 0.18f;

    [Header("玩家独立重力（不影响死亡碎片）")]
    [Min(0.1f)] public float gravityMultiplier = 2f;
    public float EffectiveGravity => Mathf.Max(0.01f, Mathf.Abs(Physics.gravity.y) * Mathf.Max(0.1f, gravityMultiplier));

    [Header("地面检测")]
    public LayerMask groundMask;
    public float groundCheckDistance = 0.06f;

    [Header("显示模型")]
    public Transform visual;

    [Header("落地摩擦粒子（CubeTrail）")]
    public ParticleSystem frictionParticles;

    [Header("音乐时间轴（未绑定时沿用原来的起跑方式）")]
    public RunMusicClock musicClock;

    private Rigidbody body;
    private BoxCollider box;

    // 提供只读的落地状态，供音符奖励判断；不改变跳跃逻辑。
    public bool IsGrounded => body != null && box != null &&
        body.linearVelocity.y <= 0.1f && CheckGrounded();

    private bool jumpRequested;
    private float jumpPressedAt;
    private LineRenderer jumpPulse;
    private Material jumpPulseMaterial;
    private float pulseStartedAt;
    private bool pulseActive;
    private bool isFlipping;
    private float flipTime;
    private float flightDuration;
    private Quaternion restingRotation;
    // 初始化引用与物理约束。
    void Awake()
    {
        body = GetComponent<Rigidbody>();
        box = GetComponent<BoxCollider>();

        // 直线跑酷：锁定左右位置和实体旋转，只允许向前移动与上下跳跃。
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX;

        if (visual != null)
            restingRotation = visual.localRotation;

        // 兼容已有场景：只自动寻找 CubeTrail，不接管其他粒子特效。
        if (frictionParticles == null)
        {
            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.name == "CubeTrail")
                {
                    frictionParticles = particles;
                    break;
                }
            }
        }
        SetFrictionEmission(false);
    }

    void OnDisable()
    {
        jumpRequested = false;
        ClearJumpPulse();
        // 死亡时 RunnerRespawn 会禁用本组件，随即停止生成摩擦碎屑。
        SetFrictionEmission(false);
    }

    private void SetFrictionEmission(bool emit)
    {
        if (frictionParticles == null) return;

        var emission = frictionParticles.emission;
        emission.enabled = emit;
        // 只控制新粒子的发射，空中已有粒子继续自然消失。
        // false代表子物体不开启粒子效果
        if (emit && !frictionParticles.isPlaying)
            frictionParticles.Play(false);
    }

    // Update is called once per frame
    void Update()
    {
        UpdateJumpPulse();
        if (musicClock != null && !musicClock.IsRunning)
        {
            jumpRequested = false;
            return;
        }
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
            jumpPressedAt = Time.time;
        }
        if (isFlipping && visual != null)
        {
            flipTime += Time.deltaTime;
            float progress =
                Mathf.Clamp01(flipTime / flightDuration);

            // 绕 X 轴翻转半圈；仅旋转显示模型，不转动物理碰撞盒。
            visual.localRotation =
                restingRotation *
                Quaternion.Euler(progress * 180f, 0f, 0f);
        }
    }
    void FixedUpdate()
    {
        // Rigidbody 的 Use Gravity 使用全局重力，这里仅为玩家补充额外加速度。
        // 未勾选 Use Gravity 时，由本脚本施加完整的玩家重力。
        if (!body.isKinematic)
            body.AddForce(Vector3.down * (EffectiveGravity - (body.useGravity ? Mathf.Abs(Physics.gravity.y) : 0f)), ForceMode.Acceleration);
        if (musicClock != null && !musicClock.IsRunning)
        {
            jumpRequested = false;
            Vector3 waitingVelocity = body.linearVelocity;
            waitingVelocity.x = waitingVelocity.z = 0f;
            body.linearVelocity = waitingVelocity;
            SetFrictionEmission(false);
            return;
        }
        Vector3 velocity = body.linearVelocity;
        bool grounded = velocity.y <= 0.1f && CheckGrounded();
        if (grounded && isFlipping)
        {
            isFlipping = false;
            if (visual != null)
                visual.localRotation = restingRotation;
        }
        // 至少保留到下一次物理更新；空中输入只在短暂窗口内等待落地。
        if (jumpRequested && Time.time - jumpPressedAt > Mathf.Max(jumpBufferSeconds, Time.fixedDeltaTime))
            jumpRequested = false;
        if (jumpRequested && grounded)
        {
            jumpRequested = false;
            PlayJumpPulse();
            float gravity = EffectiveGravity;
            float jumpSpeed =
                Mathf.Sqrt(2f * gravity * jumpHeight);

            velocity.y = jumpSpeed;

            // 平地上，从起跳到返回原高度的预计时间。
            flightDuration = 2f * jumpSpeed / gravity;
            flipTime = 0f;
            isFlipping = true;
            // 这一帧地面检测仍然为真，但已经起跳，需要立即停止发射。
            grounded = false;
        }
        // 每个物理帧固定向前速度，保留跳跃和重力产生的竖直速度。
        velocity.x = 0f;
        velocity.z = Mathf.Max(0f, forwardSpeed);
        body.linearVelocity = velocity;
        bool moving = velocity.x * velocity.x + velocity.z * velocity.z > 0.01f;
        SetFrictionEmission(grounded && moving);
    }
    public void ResetAfterRespawn()
    {
        jumpRequested = false;
        ClearJumpPulse();
        isFlipping = false;
        flipTime = 0f;
        SetFrictionEmission(false);
        if (frictionParticles != null)
            frictionParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (visual != null)
            visual.localRotation = restingRotation;
    }

    private void PlayJumpPulse()
    {
        if (!showJumpPulse) return;
        if (jumpPulse == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            jumpPulseMaterial = new Material(shader);
            jumpPulseMaterial.hideFlags = HideFlags.DontSave;
            if (jumpPulseMaterial.HasProperty("_Surface"))
            {
                jumpPulseMaterial.SetFloat("_Surface", 1f);
                jumpPulseMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                jumpPulseMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                jumpPulseMaterial.SetFloat("_ZWrite", 0f);
                jumpPulseMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                jumpPulseMaterial.SetOverrideTag("RenderType", "Transparent");
            }
            jumpPulseMaterial.renderQueue = 3000;
            GameObject pulse = new GameObject("Runner Jump Pulse");
            pulse.hideFlags = HideFlags.DontSave;
            jumpPulse = pulse.AddComponent<LineRenderer>();
            jumpPulse.sharedMaterial = jumpPulseMaterial;
            jumpPulse.useWorldSpace = false;
            jumpPulse.loop = true;
            jumpPulse.positionCount = 48;
            jumpPulse.alignment = LineAlignment.View;
            jumpPulse.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            jumpPulse.receiveShadows = false;
        }
        Bounds bounds = box.bounds;
        jumpPulse.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.025f, bounds.center.z);
        pulseStartedAt = Time.time;
        pulseActive = true;
        jumpPulse.enabled = true;
        UpdateJumpPulse();
    }
    // 光圈的播放
    private void UpdateJumpPulse()
    {
        if (!pulseActive || jumpPulse == null) return;
        float progress = (Time.time - pulseStartedAt) / Mathf.Max(0.01f, jumpPulseDuration);
        // 进度大于1或不开启光圈的时候清空
        if (progress >= 1f || !showJumpPulse) { ClearJumpPulse(); return; }
        float radius = Mathf.Lerp(0.35f, 0.85f, progress);
        for (int i = 0; i < jumpPulse.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / jumpPulse.positionCount;
            jumpPulse.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        jumpPulse.widthMultiplier = Mathf.Lerp(0.035f, 0.008f, progress);
        Color color = jumpPulseColor * 3f;
        color.a = 1f - progress;
        if (jumpPulseMaterial.HasProperty("_BaseColor")) jumpPulseMaterial.SetColor("_BaseColor", color);
        else jumpPulseMaterial.color = color;
    }

    private void ClearJumpPulse()
    {
        pulseActive = false;
        if (jumpPulse != null) jumpPulse.enabled = false;
    }

    private void OnDestroy()
    {
        if (jumpPulse != null) Destroy(jumpPulse.gameObject);
        if (jumpPulseMaterial != null) Destroy(jumpPulseMaterial);
    }

    private bool CheckGrounded()
    {
        Bounds bounds = box.bounds;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.08f,
            bounds.center.z
        );

        Vector3 colliderSize = Vector3.Scale(box.size, transform.lossyScale);
        Vector3 halfExtents = new Vector3(
            Mathf.Abs(colliderSize.x) * 0.45f,
            0.02f,
            Mathf.Abs(colliderSize.z) * 0.45f
        );
        return Physics.BoxCast(
            origin,
            halfExtents,
            Vector3.down,
            out _,
            body.rotation,
            0.08f + groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }
}
