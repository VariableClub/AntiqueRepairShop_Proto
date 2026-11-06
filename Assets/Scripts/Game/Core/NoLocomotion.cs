using UnityEngine;

namespace Dokkaebi
{
    // 로코모션 없음 (기획서 1장·11장: 이동·회전에 썸스틱을 쓰지 않는다).
    // XR Origin 프리팹의 Locomotion(이동·회전·텔레포트·등반·중력)과 텔레포트 레이를 실행할 때 끈다.
    [DefaultExecutionOrder(-60)]
    public class NoLocomotion : MonoBehaviour
    {
        public string[] childNames = { "Locomotion", "Teleport Interactor" };

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                foreach (var n in childNames)
                {
                    if (t.name != n) continue;
                    t.gameObject.SetActive(false);
                    Debug.Log($"[리그] {t.name} 끔 (로코모션 없음)");
                }
            }
        }
    }
}
