using System;
using UnityEngine;

namespace Dokkaebi
{
    // 처리 자리 (PRC-01 반환 선반). 윤곽에 맞춰 놓으면 처리 완료.
    //  - 중심 10cm 안, 각도 무관 (레벨 명세 5장). 놓는 순간 판정 (Q-135), 판정 순간 그 자리에 고정 (Q-139)
    //  - 잘못된 처리 자리(PRC-04)는 WrongPlaceZone이 튕기고 이 자리의 윤곽을 한 번 깜박인다 (FlashOutline)
    //  - 빛 윤곽 (UI-05, 기획서 15장): 물건 모양의 희미한 빛이 켜지고, 물건을 가까이 가져오면 진해진다. 색이 아니라 모양으로 구분 (11장)
    //    진하기는 윤곽 재질을 바꾸지 않고 MaterialPropertyBlock으로 알파만 바꾼다
    public class PlaceZone : MonoBehaviour, IReleaseReceiver
    {
        public string acceptKey = "bear";
        public string itemId = "bear";
        public string processKind = "반환";
        public float tolerance = 0.10f;
        public bool active;
        [Tooltip("물건 모양 빛 윤곽 (UI-05)")]
        public GameObject outline;
        [Tooltip("놓인 물건의 기준점이 갈 자리 (비우면 이 자리). 판정은 물건 무게 중심과 이 자리 사이 거리")]
        public Transform seat;

        [Tooltip("잘못된 자리에 놓았을 때 윤곽이 깜박이는 시간 (초)")]
        public float flashTime = 0.6f;

        [Header("빛 윤곽 진하기 (UI-05, 시작값)")]
        [Tooltip("물건이 이 거리(m)보다 멀면 가장 희미하게")]
        public float farDistance = 0.8f;
        [Tooltip("물건이 이 거리(m) 안이면 가장 진하게")]
        public float nearDistance = 0.15f;
        [Range(0f, 1f)] public float farAlpha = 0.15f;
        [Range(0f, 1f)] public float nearAlpha = 0.6f;

        Renderer[] outlineRenderers;
        MaterialPropertyBlock block;
        Color outlineBase = new Color(1f, 0.92f, 0.6f, 0.35f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool Done { get; private set; }
        public event Action<PlaceZone, Grabbable> Placed;

        public int Priority => 30;

        void Awake()
        {
            if (outline == null) return;
            outlineRenderers = outline.GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            foreach (var r in outlineRenderers)
            {
                var m = r != null ? r.sharedMaterial : null;
                if (m != null && m.HasProperty(BaseColorId)) { outlineBase = m.GetColor(BaseColorId); break; }
            }
        }

        // UI-05: 받는 물건(acceptKey)이 가까울수록 윤곽이 진해진다
        void Update()
        {
            if (!active || outline == null || !outline.activeSelf || outlineRenderers == null) return;
            float d = float.MaxValue;
            foreach (var g in Grabbable.All)
            {
                if (g == null || g.key != acceptKey || !g.gameObject.activeInHierarchy) continue;
                Vector3 c = g.Body != null ? g.Body.worldCenterOfMass : g.transform.position;
                d = Mathf.Min(d, Vector3.Distance(c, transform.position));
            }
            float k = d == float.MaxValue ? 0f : Mathf.InverseLerp(farDistance, nearDistance, d);
            var col = outlineBase;
            col.a = Mathf.Lerp(farAlpha, nearAlpha, k);
            block.SetColor(BaseColorId, col);
            foreach (var r in outlineRenderers) if (r != null) r.SetPropertyBlock(block);
        }

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
