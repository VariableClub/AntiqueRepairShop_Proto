using System;
using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 프롤로그 (기획서 10장, 프롤로그 연출 정의서 v0.1)
    //  EP0-02 도구 정리: 도구 4개를 트레이의 같은 모양 홈에 넣는다. 순서 자유(Q-169), 틀린 홈은 튕김(Q-170), 맞으면 고정(Q-171)
    //  EP0-03 할머니 메모 확인: 마지막 도구를 놓으면 도구함 뚜껑이 저절로 열리고, 메모에 시선 2초 → 종소리
    //  EP0-01 스탠드 점등(0~5초)은 GameFlow.Boot에서 한 뒤 Begin을 부른다
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

        public bool Finished { get; private set; }
        public event Action OnFinished;

        int placed;

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
            Debug.Log("[프롤로그] 시작: 도구 4개를 트레이 홈에 정리");
        }

        void OnToolFitted(FitSocket slot, Grabbable tool)
        {
            placed++;
            Debug.Log($"[프롤로그] 도구 정리 {placed}/{toolSlots.Length}");
            if (placed >= toolSlots.Length) StartCoroutine(OpenLid());
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
