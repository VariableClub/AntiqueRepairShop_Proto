using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Dokkaebi
{
    // 상태별로 잡기를 켜고 끄는 묶음 (기획서 21-5).
    //  Tool = 도구·소품(도구 4개, 바늘, 솜), Item = 골동품 본체, Part = 골동품 부품(단추, 쪽지)
    public enum GrabCategory { Tool, Item, Part }

    // 잡을 수 있는 모든 물건에 붙인다 (INP-01 잡기·놓기).
    //  - XRI 잡기를 Velocity Tracking + 잡은 위치 그대로(Dynamic Attach)로 맞춘다
    //  - 놓으면 다음 프레임에 ReleaseRouter로 "어디에 놓였는지" 판정을 보낸다
    //  - 바닥이나 손이 닿지 않는 곳에 떨어지면 2초 뒤 시작 위치로 돌아온다 (질의서 Q-008, Q-289)
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Grabbable : MonoBehaviour
    {
        public static readonly List<Grabbable> All = new List<Grabbable>();
        // 이 높이보다 낮게 떨어지면 '손이 닿지 않는 곳'으로 본다. GameFlow가 작업대 높이로 정한다.
        public static float LostBelowY = 0.6f;
        // 플레이 공간 중심에서 이만큼 벗어나도 손이 닿지 않는 곳
        public static float LostOutsideRadius = 1.4f;

        // 지금 상태가 허용하는 잡기·물리 (GameFlow가 상태를 바꿀 때 적어 둔다). 나중에 새로 생긴 물건(솜 덩어리 복제 등)이 따라 맞춘다.
        static bool gateKnown, gateTools, gateItems, gateParts, gateFreeze;
        public static void SetGate(bool tools, bool items, bool parts, bool freeze)
        {
            gateKnown = true; gateTools = tools; gateItems = items; gateParts = parts; gateFreeze = freeze;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { gateKnown = false; }

        // 새로 생긴 물건에 지금 상태의 잡기·물리 규칙을 적용한다
        public void ApplyGate()
        {
            if (!gateKnown) return;
            SetFrozen(gateFreeze);
            SetAllowed(category == GrabCategory.Tool ? gateTools : category == GrabCategory.Item ? gateItems : gateParts);
        }

        [Tooltip("놓인 자리 판정에 쓰는 이름 (예: button, cotton, tool_tweezers, bear)")]
        public string key;
        public GrabCategory category = GrabCategory.Part;
        [Tooltip("떨어지면 2초 뒤 시작 위치로 돌아오기")]
        public bool returnWhenLost = true;

        public XRGrabInteractable Grab { get; private set; }
        public Rigidbody Body { get; private set; }
        public IXRSelectInteractor Interactor { get; private set; }
        public Transform Hand => Interactor != null ? Interactor.transform : null;
        public Transform LastHand { get; private set; }
        public bool IsHeld => Interactor != null;
        public bool Locked { get; private set; }

        public event Action<Grabbable> Grabbed;
        public event Action<Grabbable> Released;

        Vector3 homePos;
        Quaternion homeRot;
        bool allowed = true;
        bool frozen;
        bool baseKinematic;
        bool releasePending;
        float lostTime;

        void Awake()
        {
            Grab = GetComponent<XRGrabInteractable>();
            Body = GetComponent<Rigidbody>();
            Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            Grab.useDynamicAttach = true;
            baseKinematic = Body != null && Body.isKinematic;
            SetHome();
        }

        void OnEnable()
        {
            All.Add(this);
            Grab.selectEntered.AddListener(OnSelectEntered);
            Grab.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            All.Remove(this);
            Grab.selectEntered.RemoveListener(OnSelectEntered);
            Grab.selectExited.RemoveListener(OnSelectExited);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            Interactor = args.interactorObject;
            LastHand = Interactor.transform;
            lostTime = 0f;
            Grabbed?.Invoke(this);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            Interactor = null;
            releasePending = true; // XRI 이벤트 처리 중에 컴포넌트를 끄지 않도록 다음 프레임에 판정
        }

        void Update()
        {
            if (releasePending)
            {
                releasePending = false;
                if (!Locked)
                {
                    Released?.Invoke(this);
                    ReleaseRouter.Route(this);
                }
            }

            if (returnWhenLost && !IsHeld && !Locked && !frozen && Body != null && !Body.isKinematic)
            {
                Vector3 p = transform.position;
                bool lost = p.y < LostBelowY || new Vector2(p.x, p.z).magnitude > LostOutsideRadius;
                lostTime = lost ? lostTime + Time.deltaTime : 0f;
                if (lostTime >= 2f) ReturnHome();
            }
        }

        public void SetHome() { homePos = transform.position; homeRot = transform.rotation; }
        public void SetHome(Vector3 pos, Quaternion rot) { homePos = pos; homeRot = rot; }

        public void ReturnHome()
        {
            lostTime = 0f;
            StopMotion();
            transform.SetPositionAndRotation(homePos, homeRot);
            Debug.Log($"[복귀] {name}: 손이 닿지 않는 곳에 떨어져 시작 위치로 돌아옴");
        }

        public void StopMotion()
        {
            if (Body == null || Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        // 쥔 손에서 강제로 놓게 한다. 속도를 0으로 해 튕기지 않게 한다 (기획서 21-4 '강제로 놓을 때').
        public void ForceRelease()
        {
            if (Grab.isSelected && Grab.interactionManager != null)
            {
                var interactor = Grab.firstInteractorSelecting;
                if (interactor != null) Grab.interactionManager.SelectExit(interactor, Grab);
            }
            releasePending = false;
            Interactor = null;
            StopMotion();
        }

        // 결합·처리가 끝나 자리에 고정한다. 다시 잡을 수 없다.
        public void Lock(Transform parent, Vector3 worldPos, Quaternion worldRot)
        {
            ForceRelease();
            Locked = true;
            if (Body != null)
            {
                StopMotion();
                Body.isKinematic = true;
                // 보간이 켜져 있으면 부모(곰 인형)가 순간이동해도 이전 위치에 남아 허공에 뜬다
                Body.interpolation = RigidbodyInterpolation.None;
                // 다른 Rigidbody(곰 인형 등)의 자식으로 붙으면 겹친 충돌체가 부모를 계속 밀어 떨리고 날아다닌다.
                // 결합한 부품은 더 이상 부딪힐 필요가 없으므로 충돌을 끈다
                if (parent != null && parent.GetComponentInParent<Rigidbody>() != null) Body.detectCollisions = false;
            }
            transform.SetPositionAndRotation(worldPos, worldRot);
            if (parent != null) transform.SetParent(parent, true);
            Physics.SyncTransforms();
            ApplyEnabled();
        }

        // 상태별 잡기 허용 (21-5)
        public void SetAllowed(bool value)
        {
            if (allowed == value) return;
            allowed = value;
            if (!allowed && IsHeld) ForceRelease();
            ApplyEnabled();
        }

        // 기억 장면·밤 전환 중 물리 정지 (21-4 '기억 장면 중 물리')
        public void SetFrozen(bool value)
        {
            if (frozen == value) return;
            frozen = value;
            if (frozen && IsHeld) ForceRelease();
            if (Body != null && !Locked)
            {
                if (frozen) StopMotion();
                Body.isKinematic = frozen || baseKinematic;
            }
            ApplyEnabled();
        }

        // 다른 스크립트가 잠시 물리를 직접 다룰 때 (꺼내기 전 묶어 두기 등)
        public void SetBaseKinematic(bool value)
        {
            baseKinematic = value;
            if (Body != null && !Locked && !frozen) Body.isKinematic = value;
        }

        void ApplyEnabled() { Grab.enabled = allowed && !Locked && !frozen; }

        // 손(컨트롤러) 쪽 잡는 점과 물건 쪽 잡힌 점 사이 거리. 억지 '밀어 넣기' 깊이 판정에 쓴다 (17장 억지 판정 구현 방법).
        public float HandToPartDistance()
        {
            if (Interactor == null) return 0f;
            var handAttach = Interactor.GetAttachTransform(Grab);
            var partAttach = Grab.GetAttachTransform(Interactor);
            if (handAttach == null || partAttach == null) return 0f;
            return Vector3.Distance(handAttach.position, partAttach.position);
        }

        public static void ForEach(Action<Grabbable> action)
        {
            for (int i = All.Count - 1; i >= 0; i--) action(All[i]);
        }
    }

    // 물건을 놓았을 때 받아 줄 자리 (처리 자리, 넣는 자리, 맞추는 자리)
    public interface IReleaseReceiver
    {
        int Priority { get; }        // 큰 값부터 먼저 판정
        bool TryReceive(Grabbable g); // 처리했으면 true (그 뒤 자리는 판정하지 않음)
    }

    public static class ReleaseRouter
    {
        static readonly List<IReleaseReceiver> receivers = new List<IReleaseReceiver>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { receivers.Clear(); }

        public static void Register(IReleaseReceiver r)
        {
            if (receivers.Contains(r)) return;
            receivers.Add(r);
            receivers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static void Unregister(IReleaseReceiver r) { receivers.Remove(r); }

        public static void Route(Grabbable g)
        {
            for (int i = 0; i < receivers.Count; i++)
                if (receivers[i].TryReceive(g)) return;
        }
    }
}
