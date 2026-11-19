using System;
using UnityEngine;

namespace Dokkaebi
{
    public enum AngleMode
    {
        None,     // 각도 무관
        Tilt,     // 부품의 위(Y)축이 자리의 위축과 이루는 각 (단추 면의 기울기)
        AxisLine, // 부품의 앞(Z)축이 자리의 앞축과 나란한지 (앞뒤 뒤집힘은 같게 봄, 도구 홈)
        Full      // 회전 전체
    }

    // 맞추기 자리 (INP-04). 놓는 순간 위치·각도 허용 범위를 둘 다 만족하면 결합, 아니면 튕김.
    //  - 비활성(현재 단계가 아님)이면 반응하지 않는다 (11장 '순서보다 먼저 한 조작', Q-007)
    //  - 결합되면 잠기고 같은 성공 이벤트를 다시 보내지 않는다 (Q-026, Q-049)
    //  - 튕김: 자리 바깥쪽으로 약 0.5m/s (Q-063), 오조작 +1 (REP-02)
    //  - forceJudge를 켜면 '각도가 틀린 채 밀어 넣기' 억지 판정 (REP-03·REP-04)
    public class FitSocket : MonoBehaviour, IReleaseReceiver
    {
        [Header("받는 부품")]
        public string acceptKey;
        [Tooltip("현재 단계일 때만 켠다")]
        public bool active = true;

        [Header("허용 범위 (레벨 명세 5장)")]
        public float positionTolerance = 0.02f;
        public AngleMode angleMode = AngleMode.None;
        public float angleTolerance = 20f;
        [Tooltip("이 거리 안에서 놓으면 이 자리에 놓으려던 것으로 본다")]
        public float detectRadius = 0.06f;
        [Tooltip("틀리면 튕기고 오조작으로 센다")]
        public bool bounceWrong = true;

        [Header("기록")]
        public string itemId;
        public int step;

        [Header("억지 밀어 넣기")]
        public bool forceJudge;
        public PushForceJudge push = new PushForceJudge();
        public float damageCooldown = 1f;

        [Header("결합 후")]
        [Tooltip("결합한 부품을 붙일 부모 (비우면 이 자리)")]
        public Transform lockParent;

        public bool IsFilled => Filled != null;
        public Grabbable Filled { get; private set; }

        public event Action<FitSocket, Grabbable> Fitted;
        public event Action<FitSocket, Grabbable> Rejected;
        public event Action<FitSocket, Grabbable, string> ForceDamaged;

        public int Priority => 10;

        float nextDamageTime;
        float nextWarnHaptic;
        Grabbable pushTarget;

        static readonly System.Collections.Generic.List<FitSocket> sockets = new System.Collections.Generic.List<FitSocket>();

        void OnEnable() { ReleaseRouter.Register(this); sockets.Add(this); }
        void OnDisable() { ReleaseRouter.Unregister(this); sockets.Remove(this); }

        // 자리가 여러 개 가까이 있으면(트레이 홈) 가장 가까운 켜진 자리 하나만 판정한다
        bool IsNearestFor(Grabbable g, float myDist)
        {
            foreach (var s in sockets)
            {
                if (s == this || !s.active || s.IsFilled) continue;
                float d = Vector3.Distance(g.transform.position, s.transform.position);
                if (d <= s.detectRadius && d < myDist) return false;
            }
            return true;
        }

        public bool AngleOk(Transform part)
        {
            switch (angleMode)
            {
                case AngleMode.Tilt:
                    return Vector3.Angle(part.up, transform.up) <= angleTolerance;
                case AngleMode.AxisLine:
                    float a = Vector3.Angle(part.forward, transform.forward);
                    return Mathf.Min(a, 180f - a) <= angleTolerance;
                case AngleMode.Full:
                    return Quaternion.Angle(part.rotation, transform.rotation) <= angleTolerance;
                default:
                    return true;
            }
        }

        public bool TryReceive(Grabbable g)
        {
            if (!active || IsFilled || g == null) return false;
            float dist = Vector3.Distance(g.transform.position, transform.position);
            if (dist > detectRadius || !IsNearestFor(g, dist)) return false;

            bool keyOk = g.key == acceptKey;
            if (keyOk && dist <= positionTolerance && AngleOk(g.transform))
            {
                Fit(g);
                return true;
            }

            if (!bounceWrong) return false;
            string why = !keyOk ? $"다른 부품({g.key})" : dist > positionTolerance ? $"위치 {dist * 100f:0.0}cm" : "각도";
            Debug.Log($"[맞추기] {name}: {g.name} 튕김 ({why})");
            Bounce(g);
            if (!string.IsNullOrEmpty(itemId))
            {
                RepairRecord.AddWrong(itemId, step);
                GameEvents.RaiseWrong(itemId, step);
            }
            Rejected?.Invoke(this, g);
            return true;
        }

        public void Fit(Grabbable g)
        {
            Filled = g;
            g.Lock(lockParent != null ? lockParent : transform, transform.position, FitRotation(g.transform));
            Haptics.Play(g.LastHand, Haptics.Fit);
            Debug.Log($"[맞추기] {name}: {g.name} 결합");
            if (!string.IsNullOrEmpty(itemId)) GameEvents.RaisePartFitted(itemId, g.key);
            Fitted?.Invoke(this, g);
        }

        Quaternion FitRotation(Transform part)
        {
            switch (angleMode)
            {
                case AngleMode.Full:
                    return transform.rotation;
                case AngleMode.AxisLine:
                    bool flipped = Vector3.Angle(part.forward, transform.forward) > 90f;
                    return flipped ? transform.rotation * Quaternion.Euler(0f, 180f, 0f) : transform.rotation;
                default:
                    // 위축만 자리에 맞추고 돌림은 유지 (둥근 부품)
                    return Quaternion.FromToRotation(part.up, transform.up) * part.rotation;
            }
        }

        void Bounce(Grabbable g)
        {
            if (g.Body == null || g.Body.isKinematic) return;
            Vector3 dir = g.transform.position - transform.position;
            if (dir.sqrMagnitude < 1e-6f) dir = transform.up;
            dir = (dir.normalized + Vector3.up * 0.5f).normalized;
            g.Body.linearVelocity = dir * 0.5f;
            Haptics.Play(g.LastHand, Haptics.Wrong);
        }

        void Update()
        {
            if (!forceJudge || !active || IsFilled) { push.Reset(); pushTarget = null; return; }

            // 이 자리 근처에서 잡혀 있는 맞는 부품 하나만 본다
            if (pushTarget == null || !pushTarget.IsHeld || Vector3.Distance(pushTarget.transform.position, transform.position) > detectRadius)
            {
                pushTarget = null;
                push.Reset();
                foreach (var g in Grabbable.All)
                {
                    if (g.IsHeld && g.key == acceptKey && Vector3.Distance(g.transform.position, transform.position) <= detectRadius)
                    { pushTarget = g; break; }
                }
                if (pushTarget == null) return;
            }

            bool wrongAngle = !AngleOk(pushTarget.transform);
            var result = push.Tick(pushTarget, true, wrongAngle, Time.deltaTime);

            if (result == PushForceJudge.Result.Warn && Time.time >= nextWarnHaptic)
            {
                Haptics.Send(pushTarget.Hand, Haptics.WarnAmplitude(push.Ratio), Haptics.WarnDur);
                nextWarnHaptic = Time.time + 0.05f;
                if (!string.IsNullOrEmpty(itemId)) GameEvents.RaiseForceWarning(itemId, step, push.Ratio);
            }
            else if (result == PushForceJudge.Result.Damage && Time.time >= nextDamageTime)
            {
                nextDamageTime = Time.time + damageCooldown;
                string reason = $"맞지 않는 각도로 밀어 넣음 ({push.Describe()})";
                var target = pushTarget;
                Haptics.Play(target.Hand, Haptics.Damage);
                target.ForceRelease(); // 손상 순간 쥔 부품을 놓게 한다 (Q-074)
                if (!string.IsNullOrEmpty(itemId))
                {
                    RepairRecord.MarkDamage(itemId, step, reason);
                    GameEvents.RaiseForceDamaged(itemId, step, reason);
                }
                ForceDamaged?.Invoke(this, target, reason);
                push.Reset();
                pushTarget = null;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, positionTolerance);
            Gizmos.color = new Color(1f, 1f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.05f);
        }
    }
}
