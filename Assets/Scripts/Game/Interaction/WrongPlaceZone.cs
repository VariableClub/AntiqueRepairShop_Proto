using UnityEngine;

namespace Dokkaebi
{
    // 잘못된 처리 자리 (PRC-04). 기획서 15장, 21-2, 21-4.
    //  - 앞 세 물건은 정해진 자리만 받는다. 다른 처리 자리(봉인함·기억 선반)에 놓으면 튕겨 나오고 그 자리에 놓이지 않는다
    //  - 정답 자리 윤곽이 한 번 깜박인다. 별도 힌트 대사는 없다
    //  - 처리 중에는 힌트 타이머 없이 윤곽과 깜박임으로만 안내한다
    //  - 판정 영역은 상자 모양 (이 오브젝트 기준). S5 처리 상태에서만 켠다
    public class WrongPlaceZone : MonoBehaviour, IReleaseReceiver
    {
        [Tooltip("이 자리 이름 (로그·이벤트용)")]
        public string zoneName = "봉인함";
        [Tooltip("이 자리가 받지 않는 물건 키들 (예: bear)")]
        public string[] rejectKeys = { "bear" };
        public Vector3 halfExtents = new Vector3(0.22f, 0.2f, 0.17f);
        public bool active;
        [Tooltip("이 물건의 정답 처리 자리 (윤곽을 깜박인다)")]
        public PlaceZone correct;
        public float bounceSpeed = 0.6f;

        public int Priority => 25; // 정답 자리(30)보다 늦게, 넣기·맞추기보다 먼저

        void OnEnable() { ReleaseRouter.Register(this); }
        void OnDisable() { ReleaseRouter.Unregister(this); }

        bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            p = Vector3.Scale(p, transform.lossyScale);
            return Mathf.Abs(p.x) <= halfExtents.x && Mathf.Abs(p.y) <= halfExtents.y && Mathf.Abs(p.z) <= halfExtents.z;
        }

        public bool TryReceive(Grabbable g)
        {
            if (!active || g == null || g.Locked) return false;
            bool rejected = false;
            foreach (var k in rejectKeys) if (g.key == k) { rejected = true; break; }
            if (!rejected) return false;
            Vector3 center = g.Body != null ? g.Body.worldCenterOfMass : g.transform.position;
            if (!Contains(center)) return false;

            // 튕김: 자리 바깥·위쪽으로, 작업대 쪽(공간 가운데)을 향해
            if (g.Body != null && !g.Body.isKinematic)
            {
                Vector3 toCenter = new Vector3(-transform.position.x, 0f, -transform.position.z).normalized;
                g.Body.linearVelocity = (toCenter + Vector3.up * 0.8f).normalized * bounceSpeed;
            }
            Haptics.Play(g.LastHand, Haptics.WrongPlace);
            if (correct != null) correct.FlashOutline();
            string item = correct != null ? correct.itemId : g.key;
            Debug.Log($"[처리] {item}: {zoneName}에 놓음 → 튕김, 정답 자리 윤곽 깜박임 (PRC-04)");
            GameEvents.RaiseWrongProcessPlace(item, zoneName);
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}
