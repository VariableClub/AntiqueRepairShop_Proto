using System;
using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 프롤로그 (기획서 10장, 프롤로그 연출 정의서 v0.1)
    //  EP0-02 도구 정리: 도구 4개를 트레이의 같은 모양 홈에 넣는다. 순서 자유(Q-169), 틀린 홈은 튕김(Q-170), 맞으면 고정(Q-171)
    //  EP0-03 할머니 메모 확인: 마지막 도구를 놓으면 도구함 뚜껑이 저절로 열리고, 메모에 시선 2초 → 종소리
    //  EP0-01 스탠드 점등(0~5초)은 GameFlow.Boot에서 한 뒤 Begin을 부른다
    //  홈 빛 힌트 (11/25, 실행 확인 '도구 정리에 힌트가 없어 막힘'): 도구를 하나도 더 넣지 못한 채 slotHintDelay초가 지나면
    //    아직 빈 홈에 홈 모양 빛이 희미하게 켜진다. 그 홈에 맞는 도구를 들고 가까이 가면 진해진다 (처리 자리 빛 윤곽 UI-05와 같은 방식 →
    //    '모양 빛 = 놓는 자리'를 여기서 먼저 익힌다). 도구를 하나 넣으면 다시 센다. 만지기만 해서는 다시 세지 않는다 (곰 인형 힌트와 같은 규칙)
    public class PrologueController : MonoBehaviour
    {
        public FitSocket[] toolSlots;
        [Tooltip("도구함 뚜껑 (경첩이 원점)")]
        public Transform lid;
        public Vector3 lidOpenEuler = new Vector3(100f, 0f, 0f);
        public float lidOpenTime = 1f;
        public GazeTarget memo;
        [Tooltip("10초 동안 메모를 보지 않으면 은은하게 빛나게 할 오브젝트 (Q-172)")]
        public GameObject memoGlow;

        [Header("홈 빛 힌트 (11/25)")]
        [Tooltip("toolSlots와 같은 순서. 홈 모양의 반투명 판")]
        public Renderer[] slotGlows = new Renderer[0];
        [Tooltip("진행 없이 이 시간(초)이 지나면 빈 홈이 빛난다 (곰 인형 힌트 1단계 시작값과 같게)")]
        public float slotHintDelay = 10f;
        [Range(0f, 1f)] public float glowFarAlpha = 0.18f;
        [Range(0f, 1f)] public float glowNearAlpha = 0.6f;
        [Tooltip("맞는 도구가 이 거리(m)보다 멀면 가장 희미하게, nearDistance 안이면 가장 진하게")]
        public float glowFarDistance = 0.5f;
        public float glowNearDistance = 0.1f;

        public bool Finished { get; private set; }
        public event Action OnFinished;

        int placed;
        bool tidying;
        float idle;
        bool hintShown;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            SetGlows(false);
        }

        void OnEnable()
        {
            foreach (var s in toolSlots) if (s != null) s.Fitted += OnToolFitted;
            if (memo != null) memo.OnConfirmed += OnMemoConfirmed;
        }

        void OnDisable()
        {
            foreach (var s in toolSlots) if (s != null) s.Fitted -= OnToolFitted;
            if (memo != null) memo.OnConfirmed -= OnMemoConfirmed;
        }

        public void Begin()
        {
            placed = 0;
            foreach (var s in toolSlots) if (s != null) s.active = true;
            if (memoGlow != null) memoGlow.SetActive(false);
            tidying = true; idle = 0f; hintShown = false; SetGlows(false);
            Debug.Log("[프롤로그] 시작: 도구 4개를 트레이 홈에 정리");
        }

        void OnToolFitted(FitSocket slot, Grabbable tool)
        {
            placed++;
            Debug.Log($"[프롤로그] 도구 정리 {placed}/{toolSlots.Length}");
            // 진행했으니 처음부터 다시 센다. 이미 켜진 빛은 끈다 (넣은 홈은 다시 켜지지 않는다)
            idle = 0f; hintShown = false; SetGlows(false);
            if (placed >= toolSlots.Length) { tidying = false; StartCoroutine(OpenLid()); }
        }

        IEnumerator OpenLid()
        {
            if (lid != null)
            {
                Quaternion from = lid.localRotation;
                Quaternion to = Quaternion.Euler(lidOpenEuler);
                for (float t = 0f; t < lidOpenTime; t += Time.deltaTime)
                {
                    lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / lidOpenTime));
                    yield return null;
                }
                lid.localRotation = to;
            }
            Debug.Log("[프롤로그] 도구함 뚜껑이 열림 → 메모에 시선 2초");
            if (memo != null) memo.Activate();

            for (float t = 0f; t < 10f && memo != null && !memo.Confirmed; t += Time.deltaTime) yield return null;
            if (memo != null && !memo.Confirmed && memoGlow != null) memoGlow.SetActive(true);
        }

        void Update()
        {
            if (!tidying || slotGlows == null || slotGlows.Length == 0) return;
            idle += Time.deltaTime;
            if (!hintShown && idle >= slotHintDelay)
            {
                hintShown = true;
                Debug.Log($"[프롤로그] {idle:0.#}초 무진행 → 빈 홈 빛 힌트");
            }
            if (!hintShown) return;
            for (int i = 0; i < slotGlows.Length && i < toolSlots.Length; i++)
            {
                var r = slotGlows[i];
                var s = toolSlots[i];
                if (r == null || s == null) continue;
                bool on = !s.IsFilled;
                if (r.gameObject.activeSelf != on) r.gameObject.SetActive(on);
                if (!on) continue;
                // 맞는 도구를 들고 가까이 올수록 진하게 (UI-05와 같은 방식)
                float d = float.MaxValue;
                foreach (var g in Grabbable.All)
                    if (g.key == s.acceptKey && g.IsHeld) d = Mathf.Min(d, Vector3.Distance(g.transform.position, s.transform.position));
                float k = d == float.MaxValue ? 0f : Mathf.InverseLerp(glowFarDistance, glowNearDistance, d);
                var baseCol = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
                baseCol.a = Mathf.Lerp(glowFarAlpha, glowNearAlpha, k);
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, baseCol);
                r.SetPropertyBlock(block);
            }
        }

        void SetGlows(bool on)
        {
            if (slotGlows == null) return;
            foreach (var r in slotGlows) if (r != null) r.gameObject.SetActive(on);
        }

        void OnMemoConfirmed()
        {
            if (Finished) return;
            Finished = true;
            if (memoGlow != null) memoGlow.SetActive(false);
            Debug.Log("[프롤로그] 메모 확인 → 출입문 종소리");
            OnFinished?.Invoke();
        }

        // ---- 에디터 확인용 ----
        public void DebugPlaceAllTools()
        {
            foreach (var s in toolSlots)
            {
                if (s == null || s.IsFilled) continue;
                foreach (var g in Grabbable.All)
                    if (g.key == s.acceptKey && !g.Locked) { s.Fit(g); break; }
            }
        }

        public void DebugConfirmMemo() { if (memo != null) memo.ForceConfirm(); }
    }
}
