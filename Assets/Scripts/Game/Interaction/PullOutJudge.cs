using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Dokkaebi
{
    // 꺼내기 (INP-03) + 세게 당기기 억지 (REP-03·REP-04).
    //  - 부품은 물건 안 자리(origin)에 묶여 있다. 잡으면 손을 따라 천천히 빠져나오고(최대 slideSpeed),
    //    자리에서 10cm 이상 나오면 꺼냄 (13장 판정 시작값, Q-046·Q-047 부품 중심 기준)
    //  - 17장 '꺼내기는 부품을 일정 거리까지 제자리에 묶어 두고 손이 멀어지는 속도를 본다'
    //  - 물건 기준 손 속도 1.0m/s부터 경고 진동, 1.5m/s 이상이 0.3초 이어지면 억지 (11장, Q-288)
    //  - 꺼내기 전에 놓으면 제자리로 돌아간다. 꺼낸 뒤에는 보통 부품처럼 움직인다
    [RequireComponent(typeof(Grabbable))]
    public class PullOutJudge : MonoBehaviour
    {
        public string itemId = "bear";
        public int step = 2;
        [Tooltip("부품이 들어 있는 자리 (물건의 자식)")]
        public Transform origin;
        public float extractDistance = 0.10f;
        [Tooltip("묶여 있을 때 부품이 빠져나오는 최대 속도 (m/s)")]
        public float slideSpeed = 0.3f;
        public float warnSpeed = 1.0f;
        public float damageSpeed = 1.5f;
        public float damageHold = 0.3f;
        public float speedWindow = 0.1f;
        public bool active;
        [Tooltip("묶여 있는 동안 충돌을 끌 물건 콜라이더 (몸통 안에 들어 있으므로)")]
        public Collider[] ignoreWhileInside;
        [Tooltip("부품이 들어 있는 물건 본체. 손이 부품 가까이에서 쥐면 본체가 아니라 부품이 잡히게 한다")]
        public XRGrabInteractable container;
        [Tooltip("손이 부품에서 이 거리 안이면 부품을 먼저 잡는다 (m)")]
        public float grabFirstRadius = 0.06f;

        public bool Extracted { get; private set; }
        public event Action<PullOutJudge> OnExtracted;
        public event Action<PullOutJudge, string> OnForceDamaged;

        Grabbable grab;
        XRGrabInteractable xr;
        Vector3 localOffset;      // origin 기준 부품 위치
        Vector3 grabOffsetLocal;  // 잡은 순간 손과 부품의 차이 (origin 기준)
        float overTime;
        float nextHaptic;
        float nextDamage;
        readonly Queue<(float t, Vector3 rel)> samples = new Queue<(float, Vector3)>();
        Collider[] myColliders;
        XRSelectFilterDelegate partFirst;

        void Awake()
        {
            grab = GetComponent<Grabbable>();
            xr = GetComponent<XRGrabInteractable>();
            myColliders = GetComponentsInChildren<Collider>();
        }

        void Start() { Park(); }

        void OnEnable()
        {
            grab.Grabbed += OnGrabbed;
            grab.Released += OnReleased;
            if (container != null)
            {
                partFirst ??= new XRSelectFilterDelegate(ContainerMayBeGrabbed);
                container.selectFilters.Add(partFirst);
            }
        }

        void OnDisable()
        {
            grab.Grabbed -= OnGrabbed;
            grab.Released -= OnReleased;
            if (container != null && partFirst != null) container.selectFilters.Remove(partFirst);
        }

        // 부품은 본체 콜라이더 안에 묻혀 있어 손이 둘에 함께 닿는다. 부품 바로 위에서 쥐면 본체 잡기를 막아 부품이 잡히게 한다.
        // 이미 본체를 쥔 손은 그대로 두고, 머리·다리처럼 부품에서 먼 곳을 쥐면 본체가 잡힌다
        bool ContainerMayBeGrabbed(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (Extracted || !xr.enabled || grab.IsHeld) return true;
            if (interactable.interactorsSelecting.Contains(interactor)) return true;
            var attach = interactor.GetAttachTransform(interactable);
            Vector3 hand = attach != null ? attach.position : interactor.transform.position;
            return Vector3.Distance(hand, transform.position) > grabFirstRadius;
        }

        void SetIgnore(bool ignore)
        {
            if (ignoreWhileInside == null) return;
            foreach (var other in ignoreWhileInside)
                foreach (var mine in myColliders)
                    if (other != null && mine != null) Physics.IgnoreCollision(other, mine, ignore);
        }

        // 제자리에 묶어 둔다
        public void Park()
        {
            if (Extracted || origin == null) return;
            SetIgnore(true);
            grab.SetBaseKinematic(true);
            xr.trackPosition = false;
            xr.trackRotation = false;
            localOffset = Vector3.zero;
            transform.SetPositionAndRotation(origin.position, origin.rotation);
            grab.returnWhenLost = false;
        }

        void OnGrabbed(Grabbable g)
        {
            if (Extracted || origin == null) return;
            grabOffsetLocal = origin.InverseTransformVector(transform.position - g.Hand.position);
            samples.Clear();
            overTime = 0f;
        }

        void OnReleased(Grabbable g)
        {
            if (!Extracted) Park();
            // 잡을 때의 고정 상태를 XRI가 되돌리므로, 꺼낸 뒤에는 다시 물리로 둔다
            else if (g.Body != null && !g.Locked) g.Body.isKinematic = false;
        }

        void Update()
        {
            if (Extracted || origin == null) return;

            if (!grab.IsHeld)
            {
                transform.SetPositionAndRotation(origin.TransformPoint(localOffset), origin.rotation);
                return;
            }

            // XRI는 Velocity Tracking으로 잡을 때 Rigidbody를 물리 모드로 바꾸므로, 묶여 있는 동안은 다시 고정한다
            if (grab.Body != null && !grab.Body.isKinematic) grab.Body.isKinematic = true;

            // 손을 따라가되 빠져나오는 속도에 상한을 둔다
            Vector3 desired = origin.InverseTransformPoint(grab.Hand.position) + grabOffsetLocal;
            localOffset = Vector3.MoveTowards(localOffset, desired, slideSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(origin.TransformPoint(localOffset), origin.rotation);

            if (active) JudgeForce();

            if (localOffset.magnitude >= extractDistance) Extract();
        }

        void JudgeForce()
        {
            float now = Time.time;
            Vector3 rel = grab.Hand.position - origin.position; // 물건 기준 (다른 손으로 몸통을 같이 움직여도 억지로 치지 않음)
            samples.Enqueue((now, rel));
            while (samples.Count > 2 && now - samples.Peek().t > speedWindow) samples.Dequeue();
            var s0 = samples.Peek();
            if (now - s0.t < 0.03f) return;
            float speed = (rel - s0.rel).magnitude / (now - s0.t);

            if (speed >= warnSpeed && now >= nextHaptic)
            {
                float t = Mathf.InverseLerp(warnSpeed, damageSpeed, speed);
                Haptics.Send(grab.Hand, Haptics.WarnAmplitude(t), Haptics.WarnDur);
                nextHaptic = now + 0.05f;
                GameEvents.RaiseForceWarning(itemId, step, t);
            }

            overTime = speed >= damageSpeed ? overTime + Time.deltaTime : 0f;
            if (overTime >= damageHold && now >= nextDamage)
            {
                nextDamage = now + 1f;
                overTime = 0f;
                string reason = $"세게 당김 {speed:0.00}m/s";
                Haptics.Play(grab.Hand, Haptics.Damage);
                RepairRecord.MarkDamage(itemId, step, reason);
                GameEvents.RaiseForceDamaged(itemId, step, reason);
                OnForceDamaged?.Invoke(this, reason);
                grab.ForceRelease();
                Park();
            }
        }

        public void Extract()
        {
            if (Extracted) return;
            Extracted = true;
            SetIgnore(false);
            xr.trackPosition = true;
            xr.trackRotation = true;
            grab.SetBaseKinematic(false);
            grab.returnWhenLost = true;
            grab.SetHome(origin.position + Vector3.up * 0.12f, transform.rotation);
            Haptics.Play(grab.Hand, Haptics.Fit);
            Sfx.Play(SfxKey.Extract, transform.position);
            Debug.Log($"[꺼내기] {itemId}: {name} 꺼냄");
            OnExtracted?.Invoke(this);
        }
    }
}
