using UnityEngine;

namespace Dokkaebi
{
    // SYS-05 이벤트 버스 확인용: 21-2 표의 모든 이벤트를 [이벤트] 한 줄로 남긴다.
    // AlphaLog가 켜져 있으면 이 줄들이 기기 안 파일에도 남아, 알파 완주 녹화와 함께 '이벤트가 어떤 순서로 오갔는지'를 증빙한다.
    public class EventLog : MonoBehaviour
    {
        void OnEnable()
        {
            GameEvents.StateChanged += s => Log($"상태 변경 → {s}");
            GameEvents.PartFitted += (i, k) => Log($"부품 결합 {i} / {k}");
            GameEvents.StepCompleted += (i, s) => Log($"단계 완료 {i} / {s}단계");
            GameEvents.WrongOperation += (i, s) => Log($"틀린 조작 {i} / {s}단계");
            GameEvents.ForceDamaged += (i, s, r) => Log($"억지 발생 {i} / {s}단계 / {r}");
            GameEvents.HintRaised += (i, s, l) => Log($"힌트 단계 상승 {i} / {s}단계 / {l}단계 힌트");
            GameEvents.MemoryTriggered += i => Log($"기억 트리거 {i}");
            GameEvents.MemoryEnded += i => Log($"기억 종료 {i}");
            GameEvents.ProcessCompleted += (i, k) => Log($"처리 완료 {i} / {k}");
            GameEvents.WrongProcessPlace += (i, z) => Log($"잘못된 처리 자리 {i} / {z}");
            GameEvents.EndingChosen += k => Log($"엔딩 선택 {k}");
            GameEvents.OperatorReset += () => Log("진행자 초기화");
            GameEvents.NightStarted += (n, l) => Log($"밤 시작 {n} ({l})");
            GameEvents.TableHeightSet += h => Log($"작업대 높이 H = {h:0.000}m");
            // 억지 경고는 매 프레임 올 수 있어 따로 줄인다
            GameEvents.ForceWarning += OnWarning;
        }

        // 정적 이벤트라 씬이 끝날 때 ResetStatics(SubsystemRegistration)가 비운다. 같은 씬 안에서는 한 번만 구독한다.

        float lastWarn = -10f;
        void OnWarning(string item, int step, float ratio)
        {
            if (Time.time - lastWarn < 0.5f) return;
            lastWarn = Time.time;
            Log($"억지 경고 {item} / {step}단계 / 위험도 {ratio:0.00}");
        }

        static void Log(string msg) { Debug.Log($"[이벤트] {Time.time:0.00}s {msg}"); }
    }
}
