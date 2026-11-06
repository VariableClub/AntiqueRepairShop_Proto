using System;
using UnityEngine;

namespace Dokkaebi
{
    // 한 밤의 진행 상태 (기획서 21-1). Prologue와 Done은 MVP 흐름용으로 추가했다.
    public enum NightState
    {
        Prologue,
        S1_Request,   // 의뢰
        S2_Repair,    // 수선
        S3_Memory,    // 기억 장면
        S4_Ask,       // 처리 요청
        S5_Process,   // 처리
        S6_Transition,// 공간 변화·밤 전환
        Done
    }

    // 시스템끼리 주고받는 이벤트 (기획서 21-2). 3주차 SYS-05 이벤트 버스의 시작점.
    // 받는 쪽은 itemId로 자기 물건 이벤트만 골라 쓴다.
    public static class GameEvents
    {
        public static event Action<NightState> StateChanged;
        public static event Action<string, string> PartFitted;          // itemId, 부품 키
        public static event Action<string, int> StepCompleted;          // itemId, 단계 번호
        public static event Action<string, int> WrongOperation;         // itemId, 단계 번호
        public static event Action<string, int, float> ForceWarning;    // itemId, 단계 번호, 0~1 위험도
        public static event Action<string, int, string> ForceDamaged;   // itemId, 단계 번호, 이유
        public static event Action<string> MemoryTriggered;
        public static event Action<string> MemoryEnded;
        public static event Action<string, string> ProcessCompleted;   // itemId, 처리 방식

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            StateChanged = null; PartFitted = null; StepCompleted = null; WrongOperation = null;
            ForceWarning = null; ForceDamaged = null; MemoryTriggered = null; MemoryEnded = null; ProcessCompleted = null;
        }

        public static void RaiseStateChanged(NightState s) { StateChanged?.Invoke(s); }
        public static void RaisePartFitted(string item, string key) { PartFitted?.Invoke(item, key); }
        public static void RaiseStepCompleted(string item, int step) { StepCompleted?.Invoke(item, step); }
        public static void RaiseWrong(string item, int step) { WrongOperation?.Invoke(item, step); }
        public static void RaiseForceWarning(string item, int step, float ratio) { ForceWarning?.Invoke(item, step, ratio); }
        public static void RaiseForceDamaged(string item, int step, string reason) { ForceDamaged?.Invoke(item, step, reason); }
        public static void RaiseMemoryTriggered(string item) { MemoryTriggered?.Invoke(item); }
        public static void RaiseMemoryEnded(string item) { MemoryEnded?.Invoke(item); }
        public static void RaiseProcessCompleted(string item, string kind) { ProcessCompleted?.Invoke(item, kind); }
    }
}
