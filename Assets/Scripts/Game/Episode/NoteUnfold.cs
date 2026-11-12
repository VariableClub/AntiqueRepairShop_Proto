using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 쪽지 자동 펼침 (EP1-02). 기획서 13장: 쪽지를 잡으면 손에서 저절로 펼쳐지고 손글씨(N01)가 보인다. (D147)
    //  - 배 안에서 펼치면 몸통을 뚫으므로, 배 밖으로 꺼낸 뒤(PullOutJudge 꺼냄) 손에 쥐고 있을 때 펼친다
    //  - 접힌 모양을 끄고, 두 쪽짜리 종이의 한 쪽이 경첩처럼 180° 열린다 (0.4초)
    //  - 한 번 펼치면 다시 접지 않는다. 손글씨는 UI-04 텍스처 (1m 가독성은 Quest 3 실기에서 확인)
    [RequireComponent(typeof(Grabbable))]
    public class NoteUnfold : MonoBehaviour
    {
        public GameObject folded;
        public GameObject unfoldedRoot;
        [Tooltip("열리는 쪽 경첩")]
        public Transform flap;
        public Vector3 flapClosedEuler = new Vector3(0f, 0f, 180f);
        public float time = 0.4f;
        [Tooltip("펼친 뒤 콜라이더 크기 (m)")]
        public Vector3 unfoldedCollider = new Vector3(0.09f, 0.006f, 0.07f);

        public bool Unfolded { get; private set; }

        Grabbable grab;
        PullOutJudge pull;

        void Awake()
        {
            grab = GetComponent<Grabbable>();
            pull = GetComponent<PullOutJudge>();
            if (unfoldedRoot != null) unfoldedRoot.SetActive(false);
        }

        void OnEnable() { grab.Grabbed += OnGrabbed; if (pull != null) pull.OnExtracted += OnExtracted; }
        void OnDisable() { grab.Grabbed -= OnGrabbed; if (pull != null) pull.OnExtracted -= OnExtracted; }

        void OnGrabbed(Grabbable g) { if (pull == null || pull.Extracted) Unfold(); }
        void OnExtracted(PullOutJudge p) { if (grab.IsHeld) Unfold(); }

        public void Unfold()
        {
            if (Unfolded) return;
            Unfolded = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (folded != null) folded.SetActive(false);
            if (unfoldedRoot != null) unfoldedRoot.SetActive(true);
            var box = GetComponent<BoxCollider>();
            if (box != null) box.size = unfoldedCollider;
            if (flap != null)
            {
                for (float t = 0f; t < time; t += Time.deltaTime)
                {
                    // 180°는 Slerp 방향이 정해지지 않으므로 각도를 직접 줄인다
                    flap.localRotation = Quaternion.Euler(flapClosedEuler * (1f - Mathf.SmoothStep(0f, 1f, t / time)));
                    yield return null;
                }
                flap.localRotation = Quaternion.identity;
            }
            Debug.Log("[쪽지] 손에서 저절로 펼쳐짐 → N01 \"조금 늦어도 꼭 데리러 갈게.\" (EP1-02)");
        }
    }
}
