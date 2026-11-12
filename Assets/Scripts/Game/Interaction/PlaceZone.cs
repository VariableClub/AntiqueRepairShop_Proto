using System;
using UnityEngine;

namespace Dokkaebi
{
    // 처리 자리 (PRC-01 반환 선반). 윤곽에 맞춰 놓으면 처리 완료.
    //  - 중심 10cm 안, 각도 무관 (레벨 명세 5장). 놓는 순간 판정 (Q-135), 판정 순간 그 자리에 고정 (Q-139)
    //  - 잘못된 처리 자리(PRC-04)는 WrongPlaceZone이 튕기고 이 자리의 윤곽을 한 번 깜박인다 (FlashOutline)
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

        [Tooltip("잘못된 자리에 놓았을 때 윤곽이 깜박이는 시간 (초)")]
        public float flashTime = 0.6f;

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

        // PRC-04: 정답 자리 윤곽이 한 번 깜박인다 (꺼졌다 켜짐)
        public void FlashOutline()
        {
            if (outline == null || !active) return;
            StopAllCoroutines();
            StartCoroutine(Flash());
        }

        System.Collections.IEnumerator Flash()
        {
            var r = outline.GetComponent<Renderer>();
            Vector3 baseScale = outline.transform.localScale;
            for (float t = 0f; t < flashTime; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / flashTime * Mathf.PI); // 0 → 1 → 0 한 번
                outline.transform.localScale = baseScale * (1f + 0.15f * k);
                if (r != null) r.enabled = k < 0.85f; // 가장 커질 때 한 번 꺼졌다 켜짐
                yield return null;
            }
            outline.transform.localScale = baseScale;
            if (r != null) r.enabled = true;
            outline.SetActive(active);
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
            Haptics.Play(g.LastHand, Haptics.Place);
            Debug.Log($"[처리] {itemId}: {processKind} 완료");
            GameEvents.RaiseProcessCompleted(itemId, processKind);
            Placed?.Invoke(this, g);
        }
    }
}
