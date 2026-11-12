using UnityEngine;

namespace Dokkaebi
{
    // 바닥(y=0)에서 작업대 높이 기준 어느 높이까지 올라오는 가구 몸체 (작업대 다리, 선반 받침, 봉인함 몸체 등).
    // SYS-01이 H를 정하면 아래는 바닥에 붙인 채 위끝을 H + topOffset에 맞춘다. 기본 도형(가운데 기준, 높이 1) 기준이다.
    public class FloorToHeight : MonoBehaviour
    {
        [Tooltip("위끝 = H + topOffset (m)")]
        public float topOffset;
        public float bottom;

        public void Fit(float h)
        {
            float top = h + topOffset;
            float height = Mathf.Max(0.01f, top - bottom);
            var s = transform.localScale;
            var parentScaleY = transform.parent != null ? transform.parent.lossyScale.y : 1f;
            transform.localScale = new Vector3(s.x, height / Mathf.Max(0.0001f, parentScaleY), s.z);
            var p = transform.position;
            transform.position = new Vector3(p.x, bottom + height * 0.5f, p.z);
        }
    }
}
