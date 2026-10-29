using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// 물건(눈, 편지 등)을 끼우는 자리. XRSocketInteractor와 같이 붙인다.
//  - 시작할 때 콜라이더를 트리거로 맞춘다 (소켓은 트리거여야 물건을 감지함)
//  - 물건이 끼워지면 그 물건과 몸통(소켓의 부모 Rigidbody) 사이 충돌을 끈다
//    → 끼운 물건이 몸통 콜라이더와 겹쳐서 서로 밀어내는 문제 방지
[RequireComponent(typeof(XRSocketInteractor))]
public class PartSocket : MonoBehaviour
{
    [Tooltip("확인용 이름 (로그에 표시)")]
    public string slotName;

    public UnityEvent<Transform> onFilled;
    public UnityEvent<Transform> onEmptied;

    public bool IsFilled => Socket != null && Socket.hasSelection;
    public XRSocketInteractor Socket { get; private set; }

    protected virtual void Awake()
    {
        Socket = GetComponent<XRSocketInteractor>();
        if (string.IsNullOrEmpty(slotName)) slotName = name;

        bool hasTrigger = false;
        foreach (var col in GetComponents<Collider>())
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.Log($"[자리] {slotName}: 콜라이더를 트리거로 변경");
            }
            hasTrigger = true;
        }
        if (!hasTrigger) Debug.LogWarning($"[자리] {slotName}: 콜라이더가 없어 물건을 감지할 수 없음");
    }

    protected virtual void OnEnable()
    {
        Socket.selectEntered.AddListener(OnSelectEntered);
        Socket.selectExited.AddListener(OnSelectExited);
    }

    protected virtual void OnDisable()
    {
        Socket.selectEntered.RemoveListener(OnSelectEntered);
        Socket.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        var item = args.interactableObject.transform;
        IgnoreBodyCollision(item);
        Debug.Log($"[자리] {slotName}: {item.name} 끼움");
        OnFilled(item);
        onFilled?.Invoke(item);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        var item = args.interactableObject.transform;
        Debug.Log($"[자리] {slotName}: {item.name} 빠짐");
        OnEmptied(item);
        onEmptied?.Invoke(item);
    }

    protected virtual void OnFilled(Transform item) { }
    protected virtual void OnEmptied(Transform item) { }

    // 소켓이 붙어 있는 몸통의 콜라이더들과 끼운 물건의 콜라이더 충돌을 끈다
    void IgnoreBodyCollision(Transform item)
    {
        var body = GetComponentInParent<Rigidbody>();
        Transform owner = body != null ? body.transform : transform.parent;
        if (owner == null) return;

        var itemCols = item.GetComponentsInChildren<Collider>(true);
        foreach (var bodyCol in owner.GetComponentsInChildren<Collider>(true))
        {
            if (bodyCol.isTrigger || bodyCol.transform.IsChildOf(item)) continue;
            foreach (var itemCol in itemCols)
                Physics.IgnoreCollision(bodyCol, itemCol, true);
        }
    }

    // 실행 중에 자리를 만든다 (ForceJudge가 자기 자리를 자동으로 만들 때 사용)
    public static PartSocket Create(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, Vector3 triggerSize)
    {
        var go = new GameObject(name);
        go.SetActive(false); // 설정을 끝낸 뒤 켜서 Awake/OnEnable 순서 보장
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = triggerSize;

        var socket = go.AddComponent<XRSocketInteractor>();
        socket.showInteractableHoverMeshes = false;
        var part = go.AddComponent<PartSocket>();
        part.slotName = name;
        return part;
    }
}
