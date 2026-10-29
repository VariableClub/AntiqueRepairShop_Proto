using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// 억지 조작 규칙 (초안)
//  물건은 자기 자리(소켓)에 끼워진 상태로 시작하고, 손으로 잡아 자리에서 뽑아내는 동안 판정한다.
//  규칙 1. 급하게 당김 : 자리 기준 손 이동 속도가 speedLimit 이상
//  규칙 2. 거칠게 비틂 : 손 회전 속도가 twistLimit(도/초) 이상
//  - 한계의 warnStartRatio 이상부터 진동으로 경고 (가까워질수록 강해짐)
//  - 한계를 넘으면 손상 1단계 누적 (최대 3단계), hitCooldown 동안은 추가 손상 없음
//  - 자리에서 pullZone 이상 멀어지면 "꺼냄" 처리 후 판정 종료
//  - 다시 자리에 끼우면 다음에 뽑을 때 또 판정한다 (손상은 누적 유지)
//  - 자리를 기준으로 속도를 재므로, 다른 손으로 몸통을 같이 움직여도 억지로 치지 않는다
// 기억 손상 처리
//  - 손상 단계가 그대로 기억 상태가 됨: 0 온전 / 1 흐려짐 / 2 조각 소실 / 3 파손 (DamageLog.GetMemoryState)
//  - 손상 단계에 따라 damageTarget 색이 바뀐다
[RequireComponent(typeof(XRGrabInteractable))]
public class ForceJudge : MonoBehaviour
{
    [Header("식별")]
    public string itemId = "bear";

    [Header("자리")]
    [Tooltip("끼워져 있을 자리. 비워 두면 시작 위치에 자리를 자동으로 만든다")]
    public XRSocketInteractor slot;
    [Tooltip("자리가 비어 있으면 시작 위치(부모 기준)에 자동 생성")]
    public bool autoCreateSlot = true;

    [Header("판정 값 (테스트로 조정)")]
    [Tooltip("규칙 1: 손 이동 속도 한계 (m/s)")]
    public float speedLimit = 1.0f;
    [Tooltip("규칙 2: 손 회전 속도 한계 (도/초)")]
    public float twistLimit = 540f;
    [Range(0f, 1f)] public float warnStartRatio = 0.5f;
    [Tooltip("자리에서 이만큼 멀어지면 꺼낸 것으로 판정 (m)")]
    public float pullZone = 0.15f;
    [Tooltip("손상 후 다음 손상까지 최소 간격 (초)")]
    public float hitCooldown = 0.6f;
    [Tooltip("속도를 평균 내는 구간 (초). 프레임마다 튀는 값을 걸러낸다")]
    public float speedWindow = 0.1f;

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
    // 최근 speedWindow 동안의 (시간, 자리 기준 손 위치, 손 회전) 기록
    readonly System.Collections.Generic.Queue<(float time, Vector3 rel, Quaternion rot)> samples =
        new System.Collections.Generic.Queue<(float, Vector3, Quaternion)>();
    Color originalColor = Color.white;
    float nextHapticTime;
    float nextDamageTime;
    int leftSlotFrame = -10;
    bool judging;

    public int DamageLevel => DamageLog.GetLevel(itemId);
    public MemoryState Memory => DamageLog.GetMemoryState(itemId);
    public bool IsJudging => judging;

    Vector3 SlotPoint => slot != null ? slot.GetAttachTransform(grab).position : transform.position;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (damageTarget == null) damageTarget = GetComponentInChildren<Renderer>();
        if (damageTarget != null) originalColor = damageTarget.material.color;

        if (slot == null && autoCreateSlot) CreateSlot();

        // 다른 Rigidbody 아래 자식으로 있으면 물리가 꼬이므로 떼어낸다 (위치는 자리가 잡아 줌)
        if (transform.parent != null && transform.parent.GetComponentInParent<Rigidbody>() != null)
            transform.SetParent(null, true);
    }

    void CreateSlot()
    {
        Vector3 size = Vector3.one;
        var box = GetComponent<BoxCollider>();
        if (box != null) size = box.size;
        size *= 1.5f; // 살짝 넉넉하게 감지

        var part = PartSocket.Create($"자리_{name}", transform.parent, transform.localPosition, transform.localRotation, transform.localScale, size);
        slot = part.Socket != null ? part.Socket : part.GetComponent<XRSocketInteractor>();
        slot.startingSelectedInteractable = grab;
        part.gameObject.SetActive(true);
        Debug.Log($"[억지] {itemId}: '{name}' 자리 자동 생성");
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            judging = false;
            return;
        }

        hand = args.interactorObject.transform;
        haptic = hand.GetComponentInParent<HapticImpulsePlayer>();
        if (haptic == null) Debug.LogWarning("HapticImpulsePlayer를 찾지 못함: 진동이 나지 않음");
        samples.Clear();
        samples.Enqueue((Time.time, hand.position - SlotPoint, hand.rotation));

        // 자리에서 막 뽑힌 경우(같은 프레임 또는 직후)만 판정
        judging = slot == null || Time.frameCount - leftSlotFrame <= 1;
        if (judging) Debug.Log($"[억지] {itemId}: 자리에서 뽑기 시작 (현재 손상 {DamageLevel}단계)");
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            leftSlotFrame = Time.frameCount;
            return;
        }

        hand = null;
        haptic = null;
        judging = false;
    }

    void Update()
    {
        if (!judging || hand == null) return;
        if (DamageLevel >= DamageLog.MaxLevel) { judging = false; return; }

        Vector3 slotPoint = SlotPoint;

        // 최근 speedWindow 구간의 평균 속도로 판정 (한 프레임 값은 트래킹·프레임 시간 흔들림으로 튐)
        float now = Time.time;
        Vector3 rel = hand.position - slotPoint;
        samples.Enqueue((now, rel, hand.rotation));
        while (samples.Count > 2 && now - samples.Peek().time > speedWindow) samples.Dequeue();

        var oldest = samples.Peek();
        float elapsed = now - oldest.time;
        if (elapsed >= 0.03f) // 최소 30ms 이상 쌓였을 때만 계산
        {
            float speed = (rel - oldest.rel).magnitude / elapsed;
            float twist = Quaternion.Angle(oldest.rot, hand.rotation) / elapsed;

            float speedRatio = speed / Mathf.Max(0.0001f, speedLimit);
            float twistRatio = twist / Mathf.Max(0.0001f, twistLimit);
            float ratio = Mathf.Max(speedRatio, twistRatio);

            if (ratio >= 1f)
            {
                if (Time.time >= nextDamageTime)
                    Damage(speedRatio >= twistRatio ? $"급하게 당김 {speed:0.00}m/s" : $"거칠게 비틂 {twist:0}도/s");
            }
            else if (ratio >= warnStartRatio && Time.time >= nextHapticTime)
            {
                float t = Mathf.InverseLerp(warnStartRatio, 1f, ratio);
                if (haptic != null) haptic.SendHapticImpulse(Mathf.Lerp(0.1f, 0.8f, t), 0.05f);
                nextHapticTime = Time.time + 0.05f;
            }
        }

        // 자리에서 충분히 멀어지면 꺼냄 (확 잡아챈 경우도 위에서 먼저 판정한 뒤 꺼냄 처리)
        if (Vector3.Distance(transform.position, slotPoint) > pullZone)
        {
            judging = false;
            Debug.Log($"[꺼냄] {itemId}: 손상 {DamageLevel}단계 상태로 꺼냄");
            onPulledOut?.Invoke();
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
        judging = false;
        nextDamageTime = 0f;
        DamageLog.Clear(itemId);
        if (damageTarget != null) damageTarget.material.color = originalColor;
    }

    [ContextMenu("테스트용 손상 1단계 추가")]
    void DebugAddDamage() { Damage("테스트"); }
}
