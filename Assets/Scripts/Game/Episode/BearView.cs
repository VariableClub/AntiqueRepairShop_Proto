using UnityEngine;

namespace Dokkaebi
{
    // 곰 인형 상태 표현 (REP-06 연결부). 상태 이름은 MVP 정의서 6장.
    //  S0 시작 / S1 배 벌림(단추·쪽지 꺼냄) / S2 솜 채움 / S3 눈 채움 / S4 꿰맴 1~4 / S5 완성
    //  손상 A(2단계 억지): 배의 찢어진 곳이 더 벌어짐 / 손상 B(4단계 억지): 단추에 금
    //  손상 C(3단계 억지, 회색 박스 임시): 솜이 뭉쳐 눈 자리 표시가 틀어짐 (판정 위치는 그대로, Q-010)
    // 회색 박스에서는 오브젝트 켜고 끄기와 크기·색 변화로 보여 준다 (상태 표현 S0~S5·손상 A·B 구현은 4주차 REP-06, 강민우).
    // 신주연 모델(11/17): S0~S4는 수선 전 모델(SM_Bear_Torn), S5 완성에서 수선 후 모델(SM_Bear_Repaired)로 바꾼다.
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
        [Tooltip("솜을 다 채웠을 때 몸(bellyPatch)이 커지는 비율. 13장 3단계 '배가 둥글게 부풂'을 영상에서도 보이게 (교수님 피드백 3)")]
        public float stuffedGrow = 0.04f;
        [Tooltip("들썩일 때 커지는 비율. 회색 박스 배 천은 0.25, 모델 몸 전체를 들썩이면 작게")]
        public float pulseAmount = 0.25f;
        public GameObject[] threads;      // 실 4개
        public GameObject seamClosed;     // 완성 봉합선

        [Header("모델 (신주연, 비우면 회색 박스)")]
        public GameObject modelBefore;    // 수선 전 (S0~S4)
        public GameObject modelAfter;     // 수선 후 (S5)

        [Header("눈")]
        public GameObject emptyEyeMark;
        public Transform eyeMarkRoot;     // 손상 C 때 틀어짐
        public GameObject buttonCrack;    // 손상 B

        [Header("힌트 1단계 움찔 (HNT-02, 11/25 변경)")]
        [Tooltip("움찔할 때 위로 튀는 속도 (m/s). 0.35면 약 6mm 뜬다")]
        public float twitchHop = 0.35f;
        [Tooltip("정답 쪽으로 기우는 회전 속도 (rad/s)")]
        public float twitchTilt = 1.2f;
        [Tooltip("두 번 움찔하는 사이 간격 (초)")]
        public float twitchGap = 0.35f;

        [Header("(사용 안 함) 이전 기울기 — 모델만 기울어 배·단추가 따로 놀아 11/25 움찔로 바꿈")]
        [Tooltip("정답 쪽으로 기우는 각도 (도)")]
        public float leanAngle = 7f;
        [Tooltip("기울었다 돌아오는 시간 (초). 두 번 반복")]
        public float leanTime = 0.45f;

        public string StateName { get; private set; } = "S0";

        float pulse;
        Vector3 patchBaseScale = Vector3.one;
        Vector3 patchOriginal;

        void Awake()
        {
            if (bellyPatch != null) { patchBaseScale = bellyPatch.localScale; patchOriginal = patchBaseScale; }
            SetStuffing(0f);
            SetThreads(0);
            if (tearLarge != null) tearLarge.SetActive(false);
            if (seamClosed != null) seamClosed.SetActive(false);
            if (buttonCrack != null) buttonCrack.SetActive(false);
            if (modelAfter != null) modelAfter.SetActive(false);
        }

        void OnEnable() { GameEvents.ForceDamaged += OnForceDamaged; }
        void OnDisable() { GameEvents.ForceDamaged -= OnForceDamaged; }

        void Update()
        {
            if (bellyPatch == null || pulse <= 0f) return;
            pulse = Mathf.Max(0f, pulse - Time.deltaTime * 4f);
            bellyPatch.localScale = patchBaseScale * (1f + pulseAmount * Mathf.Sin(pulse * Mathf.PI));
        }

        public void PulseBelly() { pulse = 1f; }

        // HNT-02 힌트 1단계 (11/25 변경): 곰 인형 전체가 정답 쪽으로 두 번 움찔한다 (12장 '물건이 스스로 움찔하거나 기운다').
        //  - 이전에는 모델 몸통만 키우거나(PulseBelly) 모델만 기울여(Lean) 찢어진 배·단추 자리·실은 그대로 있어 부자연스러웠다 (11/25 실행 확인)
        //  - 바닥에 놓여 있으면 물리로 살짝 튀며 기운다 → 몸에 붙은 것이 모두 같이 움직인다
        //  - 손에 들고 있으면 물리가 손을 따라가므로 대신 그 손에 약한 진동 두 번 (Haptics.HintTwitch)
        public void HintTwitch(Vector3 worldTarget)
        {
            if (twitching != null) StopCoroutine(twitching);
            twitching = StartCoroutine(TwitchRoutine(worldTarget));
        }

        Coroutine twitching;

        System.Collections.IEnumerator TwitchRoutine(Vector3 worldTarget)
        {
            var g = GetComponent<Grabbable>();
            for (int rep = 0; rep < 2; rep++)
            {
                var rb = g != null ? g.Body : null;
                if (g != null && g.IsHeld) Haptics.Play(g.Hand, Haptics.HintTwitch);
                else if (rb != null && !rb.isKinematic && (g == null || !g.Locked))
                {
                    Vector3 dir = Vector3.ProjectOnPlane(worldTarget - rb.worldCenterOfMass, Vector3.up);
                    rb.WakeUp();
                    rb.AddForce(Vector3.up * twitchHop, ForceMode.VelocityChange);
                    if (dir.sqrMagnitude > 1e-6f) rb.AddTorque(Vector3.Cross(Vector3.up, dir.normalized) * twitchTilt, ForceMode.VelocityChange);
                }
                else break; // 고정된 상태(잠김·키네마틱)면 움직이지 않는다. 대사·빛 점 힌트는 그대로 나온다
                yield return new WaitForSeconds(twitchGap);
            }
            twitching = null;
        }

        // HNT-02: 수선 전 모델이 정답 쪽(눈 자리·다음 바늘구멍)으로 살짝 두 번 기울었다 돌아온다 (12장 '움찔하거나 기운다').
        // 판정 자리(소켓·구멍)는 몸체에 붙어 있으므로 보이는 모델만 기울인다
        public void Lean(Vector3 worldTarget)
        {
            var m = modelBefore != null && modelBefore.activeInHierarchy ? modelBefore.transform : null;
            if (m == null) { PulseBelly(); return; }
            if (leaning != null) { StopCoroutine(leaning); m.localRotation = leanBase; }
            leaning = StartCoroutine(LeanRoutine(m, worldTarget));
        }

        Coroutine leaning;
        Quaternion leanBase;

        System.Collections.IEnumerator LeanRoutine(Transform m, Vector3 worldTarget)
        {
            leanBase = m.localRotation;
            Vector3 dir = Vector3.ProjectOnPlane(worldTarget - m.position, Vector3.up);
            if (dir.sqrMagnitude < 1e-6f) dir = m.forward;
            // 위쪽 축을 정답 방향으로 기울이는 회전 축 (월드 → 부모 기준)
            Vector3 axisWorld = Vector3.Cross(Vector3.up, dir.normalized);
            Vector3 axis = m.parent != null ? m.parent.InverseTransformDirection(axisWorld) : axisWorld;
            for (int rep = 0; rep < 2; rep++)
            {
                for (float t = 0f; t < leanTime; t += Time.deltaTime)
                {
                    float k = Mathf.Sin(t / leanTime * Mathf.PI);
                    m.localRotation = Quaternion.AngleAxis(leanAngle * k, axis) * leanBase;
                    yield return null;
                }
            }
            m.localRotation = leanBase;
            leaning = null;
        }

        public void SetStuffing(float t)
        {
            t = Mathf.Clamp01(t);
            if (bellyFill != null) bellyFill.localScale = Vector3.Lerp(fillFlatScale, fillFullScale, t);
            // 몸 전체가 솜만큼 조금 부푼다 (흔들 때 들썩임도 이 크기를 기준으로)
            if (bellyPatch != null && patchOriginal != Vector3.zero)
            {
                patchBaseScale = patchOriginal * (1f + stuffedGrow * t);
                if (pulse <= 0f) bellyPatch.localScale = patchBaseScale;
            }
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
                    if (modelAfter != null)
                    {
                        // 수선 후 모델은 배가 닫혀 있다. 배 속 솜은 감추고, 손상 A(더 벌어진 자국)는 남긴다
                        if (modelBefore != null) modelBefore.SetActive(false);
                        modelAfter.SetActive(true);
                        if (bellyFill != null) bellyFill.gameObject.SetActive(false);
                    }
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
