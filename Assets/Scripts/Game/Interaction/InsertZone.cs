using System;
using UnityEngine;

namespace Dokkaebi
{
    // 넣기 (INP-03). 받는 부품을 판정 영역 안에 놓으면 1회 (곰 인형 솜: 3회, 13장).
    //  - 넣은 덩어리는 물건 안으로 들어가 사라진다 (덩어리 단위 판정, Q-050·Q-178)
    //  - 억지 '쑤셔 넣기': 솜이 들어간 자리에서 2cm 이상 더 밀어 넣음, 1cm부터 경고 (13장)
    public class InsertZone : MonoBehaviour, IReleaseReceiver
    {
        public string acceptKey = "cotton";
        public Vector3 halfExtents = new Vector3(0.06f, 0.06f, 0.04f);
        public int required = 3;
        public bool active;

        [Header("기록")]
        public string itemId = "bear";
        public int step = 3;

        [Header("억지 쑤셔 넣기 (시간 조건 없음)")]
        public bool forceJudge = true;
        public PushForceJudge push = new PushForceJudge { warnTime = 0f, damageTime = 0f };
        public float damageCooldown = 1f;

        public int Count { get; private set; }
        public event Action<int> Inserted;
        public event Action<string> ForceDamaged;

        public int Priority => 20;

        Grabbable pushTarget;
        float nextHaptic, nextDamage;

        void OnEnable() { ReleaseRouter.Register(this); }
        void OnDisable() { ReleaseRouter.Unregister(this); }

        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            Vector3 s = transform.lossyScale;
            p = Vector3.Scale(p, s);
            return Mathf.Abs(p.x) <= halfExtents.x && Mathf.Abs(p.y) <= halfExtents.y && Mathf.Abs(p.z) <= halfExtents.z;
        }

        public bool TryReceive(Grabbable g)
        {
            if (!active || g == null || g.key != acceptKey || Count >= required) return false;
            if (!Contains(g.transform.position)) return false;

            Count++;
            Haptics.Play(g.LastHand, Haptics.Fit);
            Debug.Log($"[넣기] {itemId}: {acceptKey} {Count}/{required}");
            GameEvents.RaisePartFitted(itemId, acceptKey);
            g.Lock(transform, g.transform.position, g.transform.rotation);
            g.gameObject.SetActive(false); // 물건 안으로 들어감
            Inserted?.Invoke(Count);
            return true;
        }

        void Update()
        {
            if (!forceJudge || !active || Count >= required) { push.Reset(); pushTarget = null; return; }

            if (pushTarget == null || !pushTarget.IsHeld || !Contains(pushTarget.transform.position))
            {
                pushTarget = null;
                push.Reset();
                foreach (var g in Grabbable.All)
                    if (g.IsHeld && g.key == acceptKey && Contains(g.transform.position)) { pushTarget = g; break; }
                if (pushTarget == null) return;
            }

            var result = push.Tick(pushTarget, true, true, Time.deltaTime);
            if (result == PushForceJudge.Result.Warn && Time.time >= nextHaptic)
            {
                Haptics.Send(pushTarget.Hand, Haptics.WarnAmplitude(push.Ratio), Haptics.WarnDur);
                nextHaptic = Time.time + 0.05f;
                GameEvents.RaiseForceWarning(itemId, step, push.Ratio);
            }
            else if (result == PushForceJudge.Result.Damage && Time.time >= nextDamage)
            {
                nextDamage = Time.time + damageCooldown;
                string reason = $"솜을 쑤셔 넣음 ({push.Describe()})";
                Haptics.Play(pushTarget.Hand, Haptics.Damage);
                pushTarget.ForceRelease();
                RepairRecord.MarkDamage(itemId, step, reason);
                GameEvents.RaiseForceDamaged(itemId, step, reason);
                ForceDamaged?.Invoke(reason);
                push.Reset();
                pushTarget = null;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}
