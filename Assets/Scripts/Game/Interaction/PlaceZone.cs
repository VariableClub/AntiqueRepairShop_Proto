using System;
using UnityEngine;

namespace Dokkaebi
{
    // 처리 자리 (PRC-01 반환 선반). 윤곽에 맞춰 놓으면 처리 완료.
    //  - 중심 10cm 안, 각도 무관 (레벨 명세 5장). 놓는 순간 판정 (Q-135), 판정 순간 그 자리에 고정 (Q-139)
    //  - 잘못된 처리 자리(PRC-04)는 3주차
    public class PlaceZone : MonoBehaviour, IReleaseReceiver
    {
        public string acceptKey = "bear";
        public string itemId = "bear";
        public string processKind = "반환";
        public float tolerance = 0.10f;
        public bool active;
        [Tooltip("물건 모양 빛 윤곽 (UI-05 정식 모양은 4주차)")]
        public GameObject outline;
        [Tooltip("놓인 물건의 기준점이 갈 자리 (비우면 이 자리). 판정은 물건 무게 중심과 이 자리 사이 거리")]
        public Transform seat;

        public bool Done { get; private set; }
        public event Action<PlaceZone, Grabbable> Placed;

        public int Priority => 30;

        void OnEnable() { ReleaseRouter.Register(this); }
        void OnDisable() { ReleaseRouter.Unregister(this); }

        public void SetActive(bool value)
        {
            active = value && !Done;
            if (outline != null) outline.SetActive(active);
        }

        public bool TryReceive(Grabbable g)
        {
            if (!active || Done || g == null || g.key != acceptKey) return false;
            Vector3 center = g.Body != null ? g.Body.worldCenterOfMass : g.transform.position;
            if (Vector3.Distance(center, transform.position) > tolerance) return false;
            Place(g);
            return true;
        }

        public void Place(Grabbable g)
        {
            Done = true;
            active = false;
            // 수평만 맞추고 앉혀 둔다
            Vector3 fwd = Vector3.ProjectOnPlane(g.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            var s = seat != null ? seat : transform;
            g.Lock(transform, s.position, Quaternion.LookRotation(fwd, Vector3.up));
            if (outline != null) outline.SetActive(false);
            Haptics.Send(g.LastHand, 0.5f, 0.15f);
            Debug.Log($"[처리] {itemId}: {processKind} 완료");
            GameEvents.RaiseProcessCompleted(itemId, processKind);
            Placed?.Invoke(this, g);
        }
    }
}
