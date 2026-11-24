using UnityEngine;

namespace Dokkaebi
{
    // 힌트 (HNT-01 힌트 타이머, HNT-02 1단계 움직임, HNT-03 2단계 간접 대사, HNT-04 3단계 빛 점). 기획서 12장, 21-2, 21-4, 21-5.
    //  - 타이머는 S2 수선에서만 흐르고, 기억 장면(S3)부터 멈춘다 (21-5)
    //  - 물건 상태가 바뀔 때(부품 결합·단계 완료)만 처음부터 센다. 만지기만 하거나 틀린 조작·억지 경고가 나도 계속 흐른다 (12장·21-4)
    //  - 반복 실패 횟수에 따른 별도 힌트는 없다 (12장)
    //  - 시작값: 일반 단계 10·20·30초, 발견 단계(곰 1·2단계) 20·30·40초 (기능명세서 HNT-01)
    //  - 1단계: 물건이 살펴볼 쪽으로 움찔한다. 곰 1~3단계는 배 쪽 움찔, 4·5단계는 눈 자리·다음 바늘구멍 쪽으로 기움 (12장 표)
    //  - 2단계: 그 단계 데이터의 힌트 대사(H01 등)를 물건 옆에. 대사가 없는 단계는 건너뛴다.
    //    단계가 바뀌면 대기열에 남은 이전 단계 힌트 대사는 지운다. 연출 대사가 먼저 나온다(대기열 순서) (21-4)
    //  - 3단계: 정답 위치에 작은 빛 점이 깜박인다. 처리 자리 빛 윤곽(물건 모양)과는 모양으로 구분 (12장)
    // 시작값은 5주차 플레이테스트와 12월 힌트 타이밍 테스트(n=6, 기획서 7장)에서 조정한다.
    public class HintDirector : MonoBehaviour
    {
        public BearRepair repair;
        public AntiqueData data;
        public DialogueDisplay dialogue;
        [Tooltip("힌트 대사가 뜨는 자리 (물건 옆)")]
        public Transform lineAnchor;
        [Tooltip("단계별 정답 위치 (1단계부터). 5단계는 바늘구멍이 있으면 다음 구멍을 따라간다")]
        public Transform[] stepTargets = new Transform[0];
        [Tooltip("3단계 빛 점 (작은 구). 처리 자리 빛 윤곽과 모양으로 구분")]
        public GameObject lightDot;

        [Header("시간 (초, 기능명세서 HNT-01 시작값)")]
        public float[] normalTimes = { 10f, 20f, 30f };
        [Tooltip("발견 단계 (단계 데이터의 discovery)")]
        public float[] discoveryTimes = { 20f, 30f, 40f };

        [Header("빛 점")]
        [Tooltip("깜박임 한 번의 길이 (초)")]
        public float blinkPeriod = 0.7f;
        [Tooltip("켜져 있는 비율 (0~1)")]
        [Range(0.1f, 0.9f)] public float blinkOn = 0.55f;

        public int Level { get; private set; }   // 지금 도달한 힌트 단계 (0 = 없음)
        public float Elapsed { get; private set; }

        bool running;
        int timedStep;
        Renderer dotRenderer;
        Vector3 dotScale = Vector3.one;

        string ItemId => repair != null ? repair.itemId : "bear";

        void Awake()
        {
            if (lightDot != null)
            {
                dotRenderer = lightDot.GetComponent<Renderer>();
                dotScale = lightDot.transform.localScale;
                lightDot.SetActive(false);
            }
        }

        void OnEnable()
        {
            GameEvents.StateChanged += OnState;
            GameEvents.PartFitted += OnPartFitted;
            GameEvents.StepCompleted += OnStepCompleted;
            GameEvents.MemoryTriggered += OnMemory;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnState;
            GameEvents.PartFitted -= OnPartFitted;
            GameEvents.StepCompleted -= OnStepCompleted;
            GameEvents.MemoryTriggered -= OnMemory;
        }

        // ---- 타이머 켜고 끄기 (21-5: S2 켬 / S3 멈춤 / 그 외 끔) ----

        void OnState(NightState s)
        {
            bool on = s == NightState.S2_Repair;
            if (on && !running) { running = true; ResetTimer("수선 시작"); }
            else if (!on && running) { running = false; ClearHints(); Debug.Log($"[힌트] {ItemId}: 타이머 정지 ({s})"); }
        }

        void OnMemory(string item)
        {
            if (item != ItemId) return;
            running = false;
            ClearHints();
        }

        // ---- 물건 상태가 바뀔 때만 처음부터 (부품 결합·단계 완료) ----

        void OnPartFitted(string item, string key) { if (item == ItemId && running) ResetTimer($"부품 결합 {key}"); }

        void OnStepCompleted(string item, int step)
        {
            if (item != ItemId || !running) return;
            // 이전 단계 힌트 대사가 대기열에 남아 있으면 지운다 (21-4)
            if (dialogue != null) dialogue.DropQueued(IsHintLine);
            ResetTimer($"{step}단계 완료");
        }

        void ResetTimer(string why)
        {
            Elapsed = 0f;
            if (Level > 0) Debug.Log($"[힌트] {ItemId}: {why} → 힌트 {Level}단계에서 처음부터");
            ClearHints();
        }

        void ClearHints()
        {
            Level = 0;
            if (lightDot != null) lightDot.SetActive(false);
        }

        static bool IsHintLine(DialogueLine l) { return l != null && !string.IsNullOrEmpty(l.id) && l.id.StartsWith("H"); }

        // ---- 시간이 차면 한 단계씩 ----

        void Update()
        {
            if (!running || repair == null) return;
            int step = repair.CurrentStep;
            if (step < 1 || step > 5) return;
            if (step != timedStep) { timedStep = step; Elapsed = 0f; ClearHints(); }

            Elapsed += Time.deltaTime;
            var times = IsDiscovery(step) ? discoveryTimes : normalTimes;
            while (Level < 3 && Level < times.Length && Elapsed >= times[Level]) Raise(step, Level + 1);
        }

        bool IsDiscovery(int step)
        {
            var s = data != null ? data.Step(step) : null;
            return s != null && s.discovery;
        }

        void Raise(int step, int level)
        {
            Level = level;
            Debug.Log($"[힌트] {ItemId}: {step}단계 {Elapsed:0.#}초 무진행 → 힌트 {level}단계{(IsDiscovery(step) ? " (발견 단계)" : "")}");
            GameEvents.RaiseHint(ItemId, step, level);
            RepairRecord.MarkHint(ItemId, step, level);

            var target = TargetFor(step);
            switch (level)
            {
                case 1: Nudge(step, target); break;
                case 2: SayHint(step); break;
                case 3: ShowDot(target); break;
            }
        }

        // HNT-02: 곰 1~3단계는 배 쪽 움찔(흔들 때 들썩임과 같은 움직임), 4·5단계는 정답 쪽으로 기움
        void Nudge(int step, Transform target)
        {
            var view = repair.view;
            if (view == null) return;
            if (step <= 3 || target == null) view.PulseBelly();
            else view.Lean(target.position);
        }

        // HNT-03: 대본의 고정 대사를 물건 옆에. 셋째 밤처럼 대사가 없는 단계는 건너뛴다
        void SayHint(int step)
        {
            var s = data != null ? data.Step(step) : null;
            if (s == null || string.IsNullOrEmpty(s.hintLine) || dialogue == null)
            {
                Debug.Log($"[힌트] {ItemId}: {step}단계는 힌트 대사가 없어 2단계 대사를 건너뜀");
                return;
            }
            dialogue.Say(s.hintLine, lineAnchor != null ? lineAnchor : repair.bear != null ? repair.bear.transform : null);
        }

        // HNT-04: 정답 위치 빛 점
        void ShowDot(Transform target)
        {
            if (lightDot == null || target == null) return;
            lightDot.transform.position = target.position;
            lightDot.SetActive(true);
        }

        Transform TargetFor(int step)
        {
            // 5단계: 다음 바늘구멍
            var st = repair.stitch;
            if (step == 5 && st != null && st.holes != null && st.Passed < st.holes.Length && st.holes[st.Passed] != null)
                return st.holes[st.Passed];
            return stepTargets != null && step >= 1 && step <= stepTargets.Length ? stepTargets[step - 1] : null;
        }

        // 빛 점은 정답 위치를 따라가며 깜박인다 (물건을 들고 돌려도 그 자리에)
        void LateUpdate()
        {
            if (lightDot == null || !lightDot.activeSelf || repair == null) return;
            var target = TargetFor(repair.CurrentStep);
            if (target != null) lightDot.transform.position = target.position;
            float phase = Mathf.Repeat(Time.time, blinkPeriod) / blinkPeriod;
            bool on = phase < blinkOn;
            if (dotRenderer != null) dotRenderer.enabled = on;
            lightDot.transform.localScale = dotScale * (on ? 1f + 0.25f * Mathf.Sin(phase / blinkOn * Mathf.PI) : 1f);
        }

        // ---- 에디터 확인용: 지금 단계의 힌트를 한 단계 올린다 ----
        public void DebugRaise()
        {
            if (!running || repair == null || repair.CurrentStep < 1 || repair.CurrentStep > 5) { Debug.Log("[확인용] 힌트는 S2 수선 중에만"); return; }
            if (Level >= 3) { Debug.Log("[확인용] 이미 힌트 3단계"); return; }
            var times = IsDiscovery(repair.CurrentStep) ? discoveryTimes : normalTimes;
            Elapsed = Mathf.Max(Elapsed, times[Mathf.Min(Level, times.Length - 1)]);
        }
    }
}
