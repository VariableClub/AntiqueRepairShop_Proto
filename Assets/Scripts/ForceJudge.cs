using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// 억지 조작 규칙 (초안)
//  규칙 1. 급하게 당김 : 손 이동 속도가 speedLimit 이상
//  규칙 2. 거칠게 비틂 : 손 회전 속도가 twistLimit(도/초) 이상
//  - 한계의 warnStartRatio 이상부터 진동으로 경고 (가까워질수록 강해짐)
//  - 한계를 넘으면 손상 1단계 누적 (최대 3단계), 연속 판정 방지를 위해 hitCooldown 동안은 추가 손상 없음
//  - 물건이 원래 자리에서 pullZone 이상 빠져나오면 "꺼냄" 처리 후 판정 종료 (judgeUntilPulledOut 켜진 경우)
// 기억 손상 처리
//  - 손상 단계가 그대로 기억 상태가 됨: 0 온전 / 1 흐려짐 / 2 조각 소실 / 3 파손 (DamageLog.GetMemoryState)
//  - 손상 단계에 따라 물건 색이 바뀌어 회색 박스 단계에서도 상태 변화가 보이게 함
[RequireComponent(typeof(XRGrabInteractable))]
public class ForceJudge : MonoBehaviour
{
    [Header("식별")]
    public string itemId = "bear";

    [Header("판정 값 (테스트로 조정)")]
    [Tooltip("규칙 1: 손 이동 속도 한계 (m/s)")]
    public float speedLimit = 1.0f;
    [Tooltip("규칙 2: 손 회전 속도 한계 (도/초)")]
    public float twistLimit = 540f;
    [Range(0f, 1f)] public float warnStartRatio = 0.5f;
    [Tooltip("원래 자리에서 이만큼 빠져나오면 꺼낸 것으로 판정 (m)")]
    public float pullZone = 0.15f;
    [Tooltip("손상 후 다음 손상까지 최소 간격 (초)")]
    public float hitCooldown = 0.6f;
    [Tooltip("꺼낸 뒤에는 판정을 멈춤")]
    public bool judgeUntilPulledOut = true;

    [Header("손상 표시")]
    public Renderer damageTarget;
    public Color damagedColor = Color.red;
    [Tooltip("1단계(흠집)·2단계(손상) 색. 3단계는 damagedColor")]
    public Color scratchedColor = new Color(1f, 0.85f, 0.3f);
    public Color brokenColor = new Color(1f, 0.5f, 0.15f);

    [Header("이벤트")]
    public UnityEvent<int> onDamaged;
    public UnityEvent onPulledOut;

    XRGrabInteractable grab;
    Transform hand;
    HapticImpulsePlayer haptic;
    Vector3 home;
    Vector3 lastHandPos;
    Quaternion lastHandRot;
    Color originalColor = Color.white;
    float nextHapticTime;
    float nextDamageTime;
    bool pulledOut;

    public int DamageLevel => DamageLog.GetLevel(itemId);
    public MemoryState Memory => DamageLog.GetMemoryState(itemId);

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        home = transform.position;
        if (damageTarget == null) damageTarget = GetComponentInChildren<Renderer>();
        if (damageTarget != null) originalColor = damageTarget.material.color;
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        hand = args.interactorObject.transform;
        haptic = hand.GetComponentInParent<HapticImpulsePlayer>();
        if (haptic == null) Debug.LogWarning("HapticImpulsePlayer를 찾지 못함: 진동이 나지 않음");
        lastHandPos = hand.position;
        lastHandRot = hand.rotation;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        hand = null;
        haptic = null;
    }

    void Update()
    {
        if (hand == null) return;
        if (judgeUntilPulledOut && pulledOut) return;
        if (DamageLevel >= DamageLog.MaxLevel) return;

        if (!pulledOut && Vector3.Distance(transform.position, home) > pullZone)
        {
            pulledOut = true;
            Debug.Log($"[꺼냄] {itemId}: 손상 {DamageLevel}단계 상태로 꺼냄");
            onPulledOut?.Invoke();
            if (judgeUntilPulledOut) return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed = (hand.position - lastHandPos).magnitude / dt;
        float twist = Quaternion.Angle(lastHandRot, hand.rotation) / dt;
        lastHandPos = hand.position;
        lastHandRot = hand.rotation;

        float speedRatio = speed / Mathf.Max(0.0001f, speedLimit);
        float twistRatio = twist / Mathf.Max(0.0001f, twistLimit);
        float ratio = Mathf.Max(speedRatio, twistRatio);

        if (ratio >= 1f)
        {
            if (Time.time >= nextDamageTime)
                Damage(speedRatio >= twistRatio ? "급하게 당김" : "거칠게 비틂");
        }
        else if (ratio >= warnStartRatio && Time.time >= nextHapticTime)
        {
            float t = Mathf.InverseLerp(warnStartRatio, 1f, ratio);
            if (haptic != null) haptic.SendHapticImpulse(Mathf.Lerp(0.1f, 0.8f, t), 0.05f);
            nextHapticTime = Time.time + 0.05f;
        }
    }

    void Damage(string reason)
    {
        int level = DamageLog.AddDamage(itemId, reason);
        nextDamageTime = Time.time + hitCooldown;
        ApplyColor(level);
        if (haptic != null) haptic.SendHapticImpulse(Mathf.Lerp(0.6f, 1f, level / (float)DamageLog.MaxLevel), 0.25f);
        Debug.Log($"[억지] {itemId}: {reason} → 손상 {level}단계, 기억 {DamageLog.GetMemoryState(itemId)}");
        onDamaged?.Invoke(level);
    }

    void ApplyColor(int level)
    {
        if (damageTarget == null) return;
        Color c = originalColor;
        if (level == 1) c = scratchedColor;
        else if (level == 2) c = brokenColor;
        else if (level >= 3) c = damagedColor;
        damageTarget.material.color = c;
    }

    [ContextMenu("테스트용 초기화")]
    void ResetState()
    {
        pulledOut = false;
        nextDamageTime = 0f;
        DamageLog.Clear(itemId);
        if (damageTarget != null) damageTarget.material.color = originalColor;
    }

    [ContextMenu("테스트용 손상 1단계 추가")]
    void DebugAddDamage() { Damage("테스트"); }
}
