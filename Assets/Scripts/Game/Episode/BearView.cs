using UnityEngine;

namespace Dokkaebi
{
    // 곰 인형 상태 표현 (REP-06 연결부). 상태 이름은 MVP 정의서 6장.
    //  S0 시작 / S1 배 벌림(단추·쪽지 꺼냄) / S2 솜 채움 / S3 눈 채움 / S4 꿰맴 1~4 / S5 완성
    //  손상 A(2단계 억지): 배의 찢어진 곳이 더 벌어짐 / 손상 B(4단계 억지): 단추에 금
    //  손상 C(3단계 억지, 회색 박스 임시): 솜이 뭉쳐 눈 자리 표시가 틀어짐 (판정 위치는 그대로, Q-010)
    // 회색 박스에서는 오브젝트 켜고 끄기와 크기·색 변화로 보여 주고, 11/20 신채연 모델로 바꾼다.
    public class BearView : MonoBehaviour
    {
        public string itemId = "bear";

        [Header("배")]
        public GameObject tearSmall;
        public GameObject tearLarge;      // 손상 A
        public Transform bellyFill;       // 솜 채움 (납작 → 둥글게)
        public Vector3 fillFlatScale = new Vector3(0.12f, 0.12f, 0.015f);
        public Vector3 fillFullScale = new Vector3(0.13f, 0.13f, 0.07f);
        public Transform bellyPatch;      // 흔들 때 들썩이는 천 (Q-254 소리 없이도 보이는 단서)
        public GameObject[] threads;      // 실 4개
        public GameObject seamClosed;     // 완성 봉합선

        [Header("눈")]
        public GameObject emptyEyeMark;
        public Transform eyeMarkRoot;     // 손상 C 때 틀어짐
        public GameObject buttonCrack;    // 손상 B

        public string StateName { get; private set; } = "S0";

        float pulse;
        Vector3 patchBaseScale = Vector3.one;

        void Awake()
        {
            if (bellyPatch != null) patchBaseScale = bellyPatch.localScale;
            SetStuffing(0f);
            SetThreads(0);
            if (tearLarge != null) tearLarge.SetActive(false);
            if (seamClosed != null) seamClosed.SetActive(false);
            if (buttonCrack != null) buttonCrack.SetActive(false);
        }

        void OnEnable() { GameEvents.ForceDamaged += OnForceDamaged; }
        void OnDisable() { GameEvents.ForceDamaged -= OnForceDamaged; }

        void Update()
        {
            if (bellyPatch == null || pulse <= 0f) return;
            pulse = Mathf.Max(0f, pulse - Time.deltaTime * 4f);
            bellyPatch.localScale = patchBaseScale * (1f + 0.25f * Mathf.Sin(pulse * Mathf.PI));
        }

        public void PulseBelly() { pulse = 1f; }

        public void SetStuffing(float t)
        {
            if (bellyFill == null) return;
            bellyFill.localScale = Vector3.Lerp(fillFlatScale, fillFullScale, Mathf.Clamp01(t));
        }

        public void SetThreads(int n)
        {
            if (threads == null) return;
            for (int i = 0; i < threads.Length; i++) if (threads[i] != null) threads[i].SetActive(i < n);
            if (n > 0) SetState($"S4 (꿰맴 {n}/{threads.Length})");
        }

        public void OnStepCompleted(int step)
        {
            switch (step)
            {
                case 2: SetState("S1 배 벌림"); break;
                case 3: SetState("S2 솜 채움"); break;
                case 4:
                    if (emptyEyeMark != null) emptyEyeMark.SetActive(false);
                    SetState("S3 눈 채움");
                    break;
                case 5:
                    if (tearSmall != null) tearSmall.SetActive(false);
                    if (tearLarge != null && !RepairRecord.IsStepDamaged(itemId, 2)) tearLarge.SetActive(false);
                    if (seamClosed != null) seamClosed.SetActive(true);
                    SetState("S5 완성");
                    break;
            }
        }

        void OnForceDamaged(string item, int step, string reason) { if (item == itemId) ApplyDamage(step); }

        public void ApplyDamage(int step)
        {
            switch (step)
            {
                case 2:
                    if (tearSmall != null) tearSmall.SetActive(false);
                    if (tearLarge != null) tearLarge.SetActive(true);
                    break;
                case 3:
                    if (eyeMarkRoot != null)
                    {
                        eyeMarkRoot.localRotation = Quaternion.Euler(0f, 0f, 25f) * eyeMarkRoot.localRotation;
                        eyeMarkRoot.localPosition += new Vector3(0.008f, -0.006f, 0f);
                    }
                    break;
                case 4:
                    if (buttonCrack != null) buttonCrack.SetActive(true);
                    break;
            }
            Debug.Log($"[상태] {itemId}: {StateName}, 손상 [{DamageLetters()}]");
        }

        void SetState(string s)
        {
            StateName = s;
            Debug.Log($"[상태] {itemId}: {StateName}{(RepairRecord.IsDamaged(itemId) ? $", 손상 [{DamageLetters()}]" : "")}");
        }

        string DamageLetters()
        {
            var steps = RepairRecord.DamagedSteps(itemId);
            var parts = new System.Collections.Generic.List<string>();
            foreach (int s in steps) parts.Add(s == 2 ? "A(2단계)" : s == 4 ? "B(4단계)" : s == 3 ? "C(3단계)" : $"{s}단계");
            return string.Join(", ", parts);
        }
    }
}
