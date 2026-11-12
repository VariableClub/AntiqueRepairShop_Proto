using UnityEngine;
using UnityEngine.InputSystem;

namespace Dokkaebi
{
    // 헤드셋 없이 에디터에서 흐름을 확인하는 키. 빌드에는 들어가지 않는다.
    //  N : 다음으로 (도구 정리 → 메모 확인 → 현재 수선 단계 완료 → 반환)
    //  B : 현재 수선 단계에서 억지 손상 (2·3·4단계) → 갈라진 소리·영상 마스크 확인
    //  W : 처리 단계에서 곰 인형을 잘못된 처리 자리(봉인함)에 놓기 (PRC-04)
    //  V : 기억 영상 재생 방식 바꾸기 (자동 → 알파만 → 검은 배경만 → 콘티만, MEM-06 확인)
    //  R : 수선 기록 출력
    //  L : 에어팟 지연 확인 (기기에서는 왼손 메뉴 버튼, SND-04)
    public class DebugKeys : MonoBehaviour
    {
        public GameFlow flow;

#if UNITY_EDITOR
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || flow == null) return;
            if (kb.nKey.wasPressedThisFrame) flow.DebugAdvance();
            if (kb.bKey.wasPressedThisFrame && flow.bear != null) flow.bear.DebugDamageCurrent();
            if (kb.wKey.wasPressedThisFrame) flow.DebugWrongPlace();
            if (kb.vKey.wasPressedThisFrame && flow.memoryVideo != null) flow.memoryVideo.CycleMode();
            if (kb.rKey.wasPressedThisFrame && flow.bear != null) Debug.Log($"[확인용] {RepairRecord.Summary(flow.bear.itemId)}");
        }
#endif
    }
}
