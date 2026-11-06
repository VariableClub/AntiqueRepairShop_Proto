using UnityEngine;

namespace Dokkaebi
{
    // 솜 상자. 상자 안에 솜 덩어리가 늘 하나 놓여 있고, 꺼내 가면 새 덩어리가 생긴다 (Q-050·Q-178 '꺼낼 때마다 새 덩어리').
    public class CottonBox : MonoBehaviour
    {
        [Tooltip("복제할 솜 덩어리 (꺼진 상태로 둔다)")]
        public Grabbable template;
        public Transform spawnPoint;
        [Tooltip("상자에서 이만큼 멀어지면 새 덩어리를 만든다")]
        public float leaveDistance = 0.08f;
        public int maxPieces = 12;

        Grabbable current;
        int made;

        void Start()
        {
            if (template != null) template.gameObject.SetActive(false);
            Spawn();
        }

        void Update()
        {
            if (current == null || !current.gameObject.activeInHierarchy)
            {
                Spawn();
                return;
            }
            if (Vector3.Distance(current.transform.position, spawnPoint.position) > leaveDistance) Spawn();
        }

        void Spawn()
        {
            if (template == null || spawnPoint == null || made >= maxPieces) { current = null; return; }
            var go = Instantiate(template.gameObject, spawnPoint.position, spawnPoint.rotation, transform.parent);
            go.name = $"솜 덩어리 {++made}";
            go.SetActive(true);
            current = go.GetComponent<Grabbable>();
            current.SetHome(spawnPoint.position, spawnPoint.rotation);
        }
    }
}
