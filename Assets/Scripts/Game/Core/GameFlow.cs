using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 2주차 회색 박스 흐름: 프롤로그 → 첫째 밤(곰 인형) → 반환.
    // 상태(21-1)마다 잡기·물리를 켜고 끈다(21-5). 3주차 기능이 들어갈 자리는 빈 자리로 두고 로그만 남긴다.
    //  S1 의뢰: 도깨비 자리(임시 기둥)에 D01 → 곰 인형이 매트에 나타남 (NPC-01·02 등장·변신은 3주차)
    //  S3 기억: 빈 트리거 2초 (MEM-05 임시 영상은 3주차)
    //  S4 처리 요청: 도깨비 자리에 D02
    //  S5 처리: 반환 선반 윤곽 → 올려두면 반환 (PRC-01)
    //  S6 밤 전환: 로그만 (SYS-06은 3주차)
    public class GameFlow : MonoBehaviour
    {
        [Header("작업대")]
        [Tooltip("작업대 윗면 높이 H (m). 2주차는 고정값, 자동 측정(SYS-01)은 3주차")]
        public float tableHeight = 0.9f;

        [Header("구간")]
        public PrologueController prologue;
        public BearRepair bear;
        [Tooltip("곰 인형과 배 속 부품을 묶은 부모. 도깨비가 변하기 전까지 꺼 둔다")]
        public GameObject bearSet;
        public PlaceZone returnShelf;
        public DialogueDisplay dialogue;

        [Header("도깨비 자리 (임시)")]
        public GameObject dokkaebiStandIn;
        public Transform dokkaebiTalkAnchor;
        [Tooltip("곰 인형이 놓이는 수선 매트 위 자리")]
        public Transform bearSpawn;
        public float memoryPlaceholderSeconds = 2f;

        public NightState State { get; private set; }

        void Awake()
        {
            Grabbable.LostBelowY = tableHeight - 0.3f; // 작업대보다 30cm 아래로 떨어지면 손이 닿지 않는 곳
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
            if (dokkaebiStandIn != null) dokkaebiStandIn.SetActive(false);
            if (returnShelf != null) returnShelf.SetActive(false);
            SetState(NightState.Prologue);
            prologue.Begin();
        }

        void SetState(NightState s)
        {
            State = s;
            Debug.Log($"[흐름] 상태 → {s}");

            bool tools = s == NightState.Prologue || s == NightState.S1_Request || s == NightState.S2_Repair
                         || s == NightState.S4_Ask || s == NightState.S5_Process || s == NightState.Done;
            bool items = s == NightState.S2_Repair || s == NightState.S5_Process || s == NightState.Done;
            bool parts = s == NightState.S2_Repair || s == NightState.S5_Process || s == NightState.Done;
            bool freeze = s == NightState.S3_Memory || s == NightState.S6_Transition;

            Grabbable.ForEach(g =>
            {
                g.SetFrozen(freeze);
                bool allow = g.category == GrabCategory.Tool ? tools : g.category == GrabCategory.Item ? items : parts;
                g.SetAllowed(allow);
            });
            if (s == NightState.S2_Repair && bear != null) bear.ReapplyGrabRules();

            GameEvents.RaiseStateChanged(s);
        }

        void OnPrologueFinished() { StartCoroutine(FirstNight()); }

        IEnumerator FirstNight()
        {
            SetState(NightState.S1_Request);
            if (dokkaebiStandIn != null) dokkaebiStandIn.SetActive(true);
            Debug.Log("[흐름] 곰 인형 도깨비 등장 (임시 기둥, 등장 연출은 3주차)");
            if (dialogue != null) yield return dialogue.SayAndWait("D01", dokkaebiTalkAnchor);
            if (dokkaebiStandIn != null) dokkaebiStandIn.SetActive(false);

            var b = bear.bear;
            if (bearSet != null) bearSet.SetActive(true);
            b.gameObject.SetActive(true);
            if (bearSpawn != null) b.transform.SetPositionAndRotation(bearSpawn.position, bearSpawn.rotation);
            b.SetHome();
            Debug.Log("[흐름] 도깨비가 곰 인형으로 변함 → 매트 위 (변신 연출은 3주차)");

            SetState(NightState.S2_Repair);
            bear.Begin();
        }

        void OnBearRepaired() { StartCoroutine(AfterRepair()); }

        IEnumerator AfterRepair()
        {
            SetState(NightState.S3_Memory);
            GameEvents.RaiseMemoryTriggered(bear.itemId);
            Debug.Log($"[기억] 빈 트리거 {memoryPlaceholderSeconds}초 (손상 단계 [{string.Join(", ", RepairRecord.DamagedSteps(bear.itemId))}] → 3주차에 마스크·갈라진 소리 연결)");
            yield return new WaitForSeconds(memoryPlaceholderSeconds);
            GameEvents.RaiseMemoryEnded(bear.itemId);
            SetState(NightState.S4_Ask);

            if (dialogue != null) yield return dialogue.SayAndWait("P01");

            bear.bear.gameObject.SetActive(false);
            if (dokkaebiStandIn != null) dokkaebiStandIn.SetActive(true);
            if (dialogue != null) yield return dialogue.SayAndWait("D02", dokkaebiTalkAnchor);
            if (dokkaebiStandIn != null) dokkaebiStandIn.SetActive(false);
            bear.bear.gameObject.SetActive(true);

            SetState(NightState.S5_Process);
            returnShelf.SetActive(true);
            Debug.Log("[흐름] 반환 선반(뒤쪽)에 곰 인형 윤곽 표시");
        }

        void OnReturned(PlaceZone zone, Grabbable g)
        {
            StartCoroutine(EndNight());
        }

        IEnumerator EndNight()
        {
            SetState(NightState.S6_Transition);
            Debug.Log($"[흐름] 첫째 밤 끝. {RepairRecord.Summary(bear.itemId)}");
            Debug.Log("[흐름] 공간 변화·암전·'둘째 밤' 글자는 3주차(SYS-06·PRC-05)");
            yield return new WaitForSeconds(1f);
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

        bool AllToolsPlaced()
        {
            foreach (var s in prologue.toolSlots) if (s != null && !s.IsFilled) return false;
            return true;
        }
    }
}
