using System;
using UnityEngine;

namespace Dokkaebi
{
    // 한 밤의 진행 상태 (기획서 21-1). Prologue와 Done은 MVP 흐름용으로 추가했다.
    // Boot(높이 측정·스탠드 점등 전 검은 화면)은 3주차에 추가. 값 순서를 바꾸지 않도록 맨 뒤에 둔다.
    public enum NightState
    {
        Prologue,
        S1_Request,   // 의뢰
        S2_Repair,    // 수선
        S3_Memory,    // 기억 장면
        S4_Ask,       // 처리 요청
        S5_Process,   // 처리
        S6_Transition,// 공간 변화·밤 전환
        Done,
        Boot          // 앱 시작: 검은 화면, 작업대 높이 측정(SYS-01), 스탠드 점등(EP0-01)
    }

    // 시스템끼리 주고받는 이벤트 (기획서 21-2) = SYS-05 이벤트 버스.
    // 보내는 쪽은 Raise...만 부르고, 받는 쪽은 itemId로 자기 물건 이벤트만 골라 쓴다.
    // 21-2 표의 이벤트를 모두 둔다. 힌트 단계 상승은 4주차(HNT), 엔딩 선택은 4월, 진행자 초기화는 5주차(SAV-06)에 보낸다.
    // 모든 이벤트는 EventLog가 [이벤트]로 기록한다 (알파 확인용).
    public static class GameEvents
    {
        public static event Action<NightState> StateChanged;
        public static event Action<string, string> PartFitted;          // itemId, 부품 키
        public static event Action<string, int> StepCompleted;          // itemId, 단계 번호
        public static event Action<string, int> WrongOperation;         // itemId, 단계 번호
        public static event Action<string, int, float> ForceWarning;    // itemId, 단계 번호, 0~1 위험도
        public static event Action<string, int, string> ForceDamaged;   // itemId, 단계 번호, 이유
        public static event Action<string, int, int> HintRaised;        // itemId, 단계 번호, 힌트 단계(1~3)
        public static event Action<string> MemoryTriggered;
        public static event Action<string> MemoryEnded;
        public static event Action<string, string> ProcessCompleted;   // itemId, 처리 방식
        public static event Action<string, string> WrongProcessPlace;  // itemId, 놓인 자리 이름 (PRC-04)
        public static event Action<string> EndingChosen;               // 처리 방식 (넷째 밤)
        public static event Action OperatorReset;                      // 진행자 초기화 (SAV-06)
        public static event Action<int, string> NightStarted;          // 몇째 밤, 글자 ("둘째 밤")
        public static event Action<float> TableHeightSet;              // 작업대 높이 H (SYS-01)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            StateChanged = null; PartFitted = null; StepCompleted = null; WrongOperation = null;
            ForceWarning = null; ForceDamaged = null; HintRaised = null; MemoryTriggered = null; MemoryEnded = null;
            ProcessCompleted = null; WrongProcessPlace = null; EndingChosen = null; OperatorReset = null;
            NightStarted = null; TableHeightSet = null;
        }

        public static void RaiseStateChanged(NightState s) { StateChanged?.Invoke(s); }
        public static void RaisePartFitted(string item, string key) { PartFitted?.Invoke(item, key); }
        public static void RaiseStepCompleted(string item, int step) { StepCompleted?.Invoke(item, step); }
        public static void RaiseWrong(string item, int step) { WrongOperation?.Invoke(item, step); }
        public static void RaiseForceWarning(string item, int step, float ratio) { ForceWarning?.Invoke(item, step, ratio); }
        public static void RaiseForceDamaged(string item, int step, string reason) { ForceDamaged?.Invoke(item, step, reason); }
        public static void RaiseHint(string item, int step, int level) { HintRaised?.Invoke(item, step, level); }
        public static void RaiseMemoryTriggered(string item) { MemoryTriggered?.Invoke(item); }
        public static void RaiseMemoryEnded(string item) { MemoryEnded?.Invoke(item); }
        public static void RaiseProcessCompleted(string item, string kind) { ProcessCompleted?.Invoke(item, kind); }
        public static void RaiseWrongProcessPlace(string item, string zone) { WrongProcessPlace?.Invoke(item, zone); }
        public static void RaiseEndingChosen(string kind) { EndingChosen?.Invoke(kind); }
        public static void RaiseOperatorReset() { OperatorReset?.Invoke(); }
        public static void RaiseNightStarted(int night, string label) { NightStarted?.Invoke(night, label); }
        public static void RaiseTableHeightSet(float h) { TableHeightSet?.Invoke(h); }
    }
}
