using UnityEngine;
using UnityEngine.InputSystem;

namespace Dokkaebi
{
    // 헤드셋 없이 에디터에서 흐름을 확인하는 키. 빌드에는 들어가지 않는다.
    //  N : 다음으로 (도구 정리 → 메모 확인 → 현재 수선 단계 완료 → 반환)
    //  B : 현재 수선 단계에서 억지 손상 (2·3·4단계)
    //  R : 수선 기록 출력
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
            if (kb.rKey.wasPressedThisFrame && flow.bear != null) Debug.Log($"[확인용] {RepairRecord.Summary(flow.bear.itemId)}");
        }
#endif
    }
}
