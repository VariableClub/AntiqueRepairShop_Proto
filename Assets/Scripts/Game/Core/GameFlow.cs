using System;
using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 골동품 진행 상태 머신 (SYS-03). 기획서 21-1(상태와 넘어가는 조건), 21-4(겹칠 때 규칙), 21-5(상태별로 켜고 끄는 것).
    // 3주차 알파 흐름: 앱 시작 → 프롤로그 → 첫째 밤(곰 인형) S1~S6 → 둘째 밤 S1 자리 (거울은 1월).
    //  Boot  : 검은 화면에서 작업대 높이 측정(SYS-01) → 스탠드 점등(EP0-01)
    //  S1 의뢰: 출입문 종 → 도깨비 등장(NPC-01) → D01 + 고유음 → 물건으로 변신(NPC-02)
    //  S2 수선: 단계마다 기억 소리 한 겹 (MEM-01·02, MemorySoundLayers가 단계 완료 이벤트를 듣는다)
    //  S3 기억: 수선 완료(숨 쉬듯 움직임 + 따뜻한 소리) → 소리 교차 페이드(MEM-03) → 감정 조명(MEM-04) → 기억 영상(MEM-05·06·07)
    //  S4 처리 요청: P01 → 도깨비로 돌아옴 → D02 → 다시 물건으로
    //  S5 처리: 반환 선반 윤곽. 다른 처리 자리면 튕김 + 윤곽 깜박임(PRC-04)
    //  S6 공간 변화·밤 전환 (SYS-06·PRC-05)
    // 도깨비 등장·변신, 기억 영상, 밤 전환은 정해진 길이 + 5초 안에 끝나지 않으면 강제로 넘긴다 (21-4).
    public class GameFlow : MonoBehaviour
    {
        [Header("작업대")]
        [Tooltip("씬을 만든 기준 높이 H (m). 실제 H는 앱 시작 때 잰다(SYS-01)")]
        public float tableHeight = 0.9f;
        public TableHeightCalibrator calibrator;

        [Header("구간")]
        public PrologueController prologue;
        public BearRepair bear;
        public AntiqueData bearData;
        [Tooltip("곰 인형과 배 속 부품을 묶은 부모. 도깨비가 변하기 전까지 꺼 둔다")]
        public GameObject bearSet;
        public PlaceZone returnShelf;
        public WrongPlaceZone[] wrongPlaces;
        public DialogueDisplay dialogue;

        [Header("연출")]
        public ScreenFader fader;
        public LightingDirector lighting;
        public DokkaebiStandIn dokkaebi;
        public Transform dokkaebiTalkAnchor;
        public MemorySoundLayers memorySound;
        public MemoryVideoPlayer memoryVideo;
        public NightTransition transition;
        public Transform doorBell;
        [Tooltip("곰 인형이 놓이는 수선 매트 위 자리")]
        public Transform bearSpawn;

        [Header("시간 (초)")]
        [Tooltip("수선 완료 뒤 인형이 숨 쉬듯 움직이는 시간 (기억 ①)")]
        public float breatheTime = 2f;
        [Tooltip("기억 트리거 뒤 감정 조명이 시작되는 시각 (기억 ②)")]
        public float lightingDelay = 2f;
        public float lightingTime = 3f;
        [Tooltip("영상이 끝나기 몇 초 전부터 조명이 돌아오는지 (기억 ⑥)")]
        public float restoreLead = 3f;
        public float forceExtra = 5f;

        [Header("에디터 확인")]
        [Tooltip("헤드셋 없이 볼 때 시작 검은 화면을 짧게")]
        public bool quickBootInEditor = true;

        public NightState State { get; private set; } = NightState.Boot;
        public int Night { get; private set; }

        void Awake()
        {
            Grabbable.LostBelowY = tableHeight - 0.3f; // 작업대보다 30cm 아래로 떨어지면 손이 닿지 않는 곳 (SYS-01이 다시 정함)
        }

        void OnEnable()
        {
            if (prologue != null) prologue.OnFinished += OnPrologueFinished;
            if (bear != null) bear.Repaired += OnBearRepaired;
            if (returnShelf != null) returnShelf.Placed += OnReturned;
        }

        void OnDisable()
        {
            if (prologue != null) prologue.OnFinished -= OnPrologueFinished;
            if (bear != null) bear.Repaired -= OnBearRepaired;
            if (returnShelf != null) returnShelf.Placed -= OnReturned;
        }

        void Start()
        {
            RepairRecord.Clear();
            if (bearSet != null) bearSet.SetActive(false);
            if (dokkaebi != null) dokkaebi.gameObject.SetActive(false);
            if (returnShelf != null) returnShelf.SetActive(false);
            SetWrongPlaces(false);
            StartCoroutine(Boot());
        }

        // ================= 상태 (21-5) =================

        void SetState(NightState s)
        {
            State = s;
            Debug.Log($"[흐름] 상태 → {s}");

            // 잡기: 프롤로그=도구만, S1·S4=도구·소품, S2·S5=모두, S3·S6·Boot=끔 (21-5)
            bool tools = s == NightState.Prologue || s == NightState.S1_Request || s == NightState.S2_Repair
                         || s == NightState.S4_Ask || s == NightState.S5_Process || s == NightState.Done;
            bool items = s == NightState.S2_Repair || s == NightState.S5_Process || s == NightState.Done;
            bool parts = s == NightState.S2_Repair || s == NightState.S5_Process || s == NightState.Done;
            // 물리: S3·S6·Boot는 모두 그 자리에 고정
            bool freeze = s == NightState.S3_Memory || s == NightState.S6_Transition || s == NightState.Boot;

            Grabbable.SetGate(tools, items, parts, freeze);
            Grabbable.ForEach(g =>
            {
                g.SetFrozen(freeze);
                bool allow = g.category == GrabCategory.Tool ? tools : g.category == GrabCategory.Item ? items : parts;
                g.SetAllowed(allow);
            });
            if (s == NightState.S2_Repair && bear != null) bear.ReapplyGrabRules();

            // 처리 판정: S5에서만
            bool process = s == NightState.S5_Process;
            if (!process && returnShelf != null && !returnShelf.Done) returnShelf.SetActive(false);
            SetWrongPlaces(process);

            GameEvents.RaiseStateChanged(s);
        }

        void SetWrongPlaces(bool on)
        {
            if (wrongPlaces == null) return;
            foreach (var w in wrongPlaces) if (w != null) w.active = on;
        }

        // 연출이 끝나지 않을 때 (21-4): 정해진 길이 + 5초면 강제로 넘긴다
        IEnumerator WithTimeout(IEnumerator routine, float length, string what)
        {
            bool done = false;
            var co = StartCoroutine(Wrap(routine, () => done = true));
            float limit = length + forceExtra;
            for (float t = 0f; t < limit && !done; t += Time.deltaTime) yield return null;
            if (!done)
            {
                StopCoroutine(co);
                Debug.LogWarning($"[흐름] {what}: {limit:0.#}초 안에 끝나지 않아 강제로 다음으로 (21-4)");
            }
        }

        static IEnumerator Wrap(IEnumerator routine, Action onDone)
        {
            yield return routine;
            onDone();
        }

        // ================= 앱 시작 (SYS-01, EP0-01) =================

        IEnumerator Boot()
        {
            SetState(NightState.Boot);
            if (fader != null) fader.Set(1f);
            if (lighting != null) lighting.SetDark();

            if (calibrator != null)
            {
                if (Application.isEditor && quickBootInEditor && !UnityEngine.XR.XRSettings.isDeviceActive) calibrator.measureTime = 0.6f;
                yield return calibrator.Measure();
            }

            // EP0-01: 검은 화면에서 작업대 왼쪽 스탠드가 켜지며 수선소가 드러난다 (0~5초)
            if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.2f));
            if (lighting != null) yield return lighting.LampOn();
            else yield return new WaitForSeconds(1.2f);
            Debug.Log("[프롤로그] 스탠드 점등 끝 (EP0-01). 이때의 스탠드 색이 기억 연출 뒤 돌아올 기본색");

            SetState(NightState.Prologue);
            prologue.Begin();
        }

        // ================= 첫째 밤 =================

        void OnPrologueFinished() { StartCoroutine(FirstNight()); }

        IEnumerator FirstNight()
        {
            Night = 1;
            GameEvents.RaiseNightStarted(Night, bearData != null ? bearData.nightLabel : "첫째 밤");
            SetState(NightState.S1_Request);

            // 출입문 종 → 도깨비 등장 (NPC-01)
            Sfx.Play(SfxKey.DoorBell, doorBell != null ? doorBell.position : Vector3.forward);
            yield return new WaitForSeconds(0.6f);
            if (dokkaebi != null) yield return WithTimeout(dokkaebi.Appear(), dokkaebi.appearTime, "도깨비 등장");

            // D01 + 고유음 (NPC-04)
            if (dokkaebi != null) dokkaebi.StartVoice();
            if (dialogue != null) yield return dialogue.SayAndWait(bearData != null ? bearData.requestLine : "D01", dokkaebiTalkAnchor);
            if (dokkaebi != null) dokkaebi.StopVoice();

            // 도깨비 → 곰 인형 (NPC-02). 곰 인형은 매트 위에 나타난다
            // 곰 인형 세트(인형 + 배 속 단추·쪽지)는 꺼 둔 채 자리를 잡고, 연기가 가장 짙을 때 한꺼번에 켠다
            var b = bear.bear;
            b.gameObject.SetActive(true);
            if (bearSpawn != null) b.transform.SetPositionAndRotation(bearSpawn.position, bearSpawn.rotation);
            var set = bearSet != null ? bearSet : b.gameObject;
            if (dokkaebi != null) yield return WithTimeout(dokkaebi.BecomeItem(set), dokkaebi.transformTime, "도깨비 변신");
            set.SetActive(true);
            b.SetHome();
            Debug.Log("[흐름] 도깨비가 곰 인형으로 변함 → 매트 위");

            if (memorySound != null) memorySound.ResetLayers();
            SetState(NightState.S2_Repair);
            bear.Begin();
        }

        void OnBearRepaired() { StartCoroutine(AfterRepair()); }

        IEnumerator AfterRepair()
        {
            var data = bearData;
            var b = bear.bear;

            // 기억 ① 0~2초: 마지막 실을 당긴 인형이 숨 쉬듯 움직이고 따뜻한 효과음. 쌓인 기억 소리가 이어진다
            SetState(NightState.S3_Memory);
            Sfx.Play(SfxKey.RepairDone, b.transform.position);
            GameEvents.RaiseMemoryTriggered(bear.itemId); // 기억 트리거: 소리 교차 페이드 시작(MEM-03), 힌트 타이머 정지
            StartCoroutine(Breathe(b.transform, breatheTime));

            // 기억 ② 2~5초: 감정 조명 (MEM-04)
            StartCoroutine(DelayedLighting(data));

            // 기억 영상 (MEM-05·06·07). 끝나기 3초 전부터 조명이 돌아온다 (기억 ⑥)
            float length = data != null ? data.memoryLength : 20f;
            if (memoryVideo != null)
                yield return WithTimeout(memoryVideo.Play(data, restoreLead, () => { if (lighting != null) lighting.ExitMemory(restoreLead); }),
                                         length + memoryVideo.fadeTime * 2f + memoryVideo.prepareTimeout * 2f, "기억 영상");
            else { yield return new WaitForSeconds(length); if (lighting != null) lighting.ExitMemory(restoreLead); }
            if (lighting != null && lighting.InMemory) lighting.ExitMemory(1f);

            GameEvents.RaiseMemoryEnded(bear.itemId);
            Debug.Log($"[기억] 끝. 손상 단계 [{string.Join(", ", RepairRecord.DamagedSteps(bear.itemId))}], 재생 방식: {(memoryVideo != null ? memoryVideo.LastResult : "없음")}");

            // S4 처리 요청: P01 → 도깨비 재등장 → D02 → 다시 곰 인형
            SetState(NightState.S4_Ask);
            if (dialogue != null) yield return dialogue.SayAndWait(data != null ? data.reactionLine : "P01");

            if (dokkaebi != null) yield return WithTimeout(dokkaebi.BecomeDokkaebi(b.gameObject), dokkaebi.transformTime, "도깨비 재등장");
            else b.gameObject.SetActive(false);
            if (dokkaebi != null) dokkaebi.StartVoice();
            if (dialogue != null) yield return dialogue.SayAndWait(data != null ? data.processLine : "D02", dokkaebiTalkAnchor);
            if (dokkaebi != null) dokkaebi.StopVoice();
            if (dokkaebi != null) yield return WithTimeout(dokkaebi.BecomeItem(b.gameObject), dokkaebi.transformTime, "도깨비 변신");
            b.gameObject.SetActive(true);

            // S5 처리
            SetState(NightState.S5_Process);
            returnShelf.SetActive(true);
            Debug.Log($"[흐름] 처리: {AntiqueData.ProcessName(data != null ? data.process : ProcessKind.Return)} — 반환 선반(뒤쪽)에 곰 인형 윤곽");
        }

        IEnumerator DelayedLighting(AntiqueData data)
        {
            yield return new WaitForSeconds(lightingDelay);
            if (State == NightState.S3_Memory && lighting != null)
                lighting.EnterMemory(data != null ? data.emotionColor : new Color(1f, 0.68f, 0.28f), lightingTime);
        }

        // 인형이 숨 쉬듯 두 번 부풀었다 돌아온다 (13장)
        static IEnumerator Breathe(Transform t, float time)
        {
            Vector3 s0 = t.localScale;
            for (float e = 0f; e < time; e += Time.deltaTime)
            {
                float k = Mathf.Sin(e / time * Mathf.PI * 2f);
                t.localScale = s0 * (1f + 0.04f * Mathf.Max(0f, k));
                yield return null;
            }
            t.localScale = s0;
        }

        void OnReturned(PlaceZone zone, Grabbable g) { StartCoroutine(EndNight(g)); }

        IEnumerator EndNight(Grabbable returned)
        {
            SetState(NightState.S6_Transition);
            Debug.Log($"[흐름] 첫째 밤 끝. {RepairRecord.Summary(bear.itemId)}");
            var data = bearData;
            string next = "둘째 밤";
            if (transition != null)
            {
                yield return WithTimeout(transition.Run(data != null ? data.process : ProcessKind.Return, Night, next, () =>
                {
                    // 반환한 곰 인형은 주인에게 돌아갔으므로 암전 때 사라진다 (D172)
                    if (returned != null) returned.gameObject.SetActive(false);
                }), transition.TotalLength, "밤 전환");
            }

            // 둘째 밤 S1 자리: 출입문 종까지. 거울 구간은 1월 (7장)
            Night = 2;
            GameEvents.RaiseNightStarted(Night, next);
            Sfx.Play(SfxKey.DoorBell, doorBell != null ? doorBell.position : Vector3.forward);
            Debug.Log("[흐름] 둘째 밤 S1 자리 (출입문 종). 거울 구간은 1월에 조립 → 알파 흐름 끝");
            SetState(NightState.Done);
        }

        // ---- 에디터 확인용 ----
        public void DebugAdvance()
        {
            switch (State)
            {
                case NightState.Prologue:
                    if (!AllToolsPlaced()) prologue.DebugPlaceAllTools();
                    else prologue.DebugConfirmMemo();
                    break;
                case NightState.S2_Repair:
                    bear.DebugCompleteCurrent();
                    break;
                case NightState.S5_Process:
                    returnShelf.Place(bear.bear);
                    break;
                default:
                    Debug.Log($"[확인용] {State}에서는 기다리면 다음으로 넘어감");
                    break;
            }
        }

        // 에디터 확인용: S5에서 곰 인형을 잘못된 처리 자리(첫 번째)에 놓은 것처럼
        public void DebugWrongPlace()
        {
            if (State != NightState.S5_Process || wrongPlaces == null || wrongPlaces.Length == 0 || wrongPlaces[0] == null) return;
            var w = wrongPlaces[0];
            bear.bear.transform.position = w.transform.position;
            Physics.SyncTransforms();
            w.TryReceive(bear.bear);
        }

        bool AllToolsPlaced()
        {
            foreach (var s in prologue.toolSlots) if (s != null && !s.IsFilled) return false;
            return true;
        }
    }
}
