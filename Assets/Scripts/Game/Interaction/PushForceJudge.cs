using System;
using UnityEngine;

namespace Dokkaebi
{
    // 억지 '밀어 넣기' 판정 (REP-03 경고, REP-04 판정). 기획서 11장·13장, 17장 억지 판정 구현 방법.
    //  - 깊이: 손(컨트롤러)과 잡힌 부품 기준점 사이 거리가 자리에 들어온 순간보다 늘어난 만큼 (Q-070, Q-287)
    //    부품은 자리의 충돌체에 막혀 멈추므로, 손이 더 들어간 거리가 밀어 넣은 깊이다
    //  - 시간: 조건(예: 각도가 틀림)을 만족한 채 미는 시간. 0.3초 이상 멈추면 처음부터 (Q-071)
    //  - 경고 구간에 0.3초 이상 머문 뒤에만 손상 (Q-069, Q-288)
    [Serializable]
    public class PushForceJudge
    {
        [Tooltip("경고 시작 깊이 (m)")] public float warnDepth = 0.01f;
        [Tooltip("억지 판정 깊이 (m)")] public float damageDepth = 0.02f;
        [Tooltip("경고 시작 시간 (초). 시간 조건이 없으면 0")] public float warnTime = 0.5f;
        [Tooltip("억지 판정 시간 (초). 시간 조건이 없으면 0")] public float damageTime = 1f;
        [Tooltip("손상 전에 경고 구간에 머물러야 하는 시간 (초)")] public float minWarnHold = 0.3f;
        [Tooltip("이 깊이 이상이면 '밀고 있다'로 본다 (m)")] public float pushingDepth = 0.003f;
        [Tooltip("이만큼 밀기를 멈추면 시간을 처음부터 (초)")] public float resetGap = 0.3f;

        public enum Result { None, Warn, Damage }

        float baseline = -1f;
        float pushTime, idleTime, warnHeld;

        public float Ratio { get; private set; }
        public float Depth { get; private set; }
        public float PushTime => pushTime;

        public void Reset()
        {
            baseline = -1f;
            pushTime = idleTime = warnHeld = 0f;
            Ratio = Depth = 0f;
        }

        // inZone: 부품이 자리 판정 범위 안. condition: 억지로 볼 조건(예: 각도가 틀림)
        public Result Tick(Grabbable g, bool inZone, bool condition, float dt)
        {
            if (g == null || !g.IsHeld || !inZone) { Reset(); return Result.None; }

            float sep = g.HandToPartDistance();
            if (baseline < 0f || sep < baseline) baseline = sep; // 들어온 순간(가장 가까운 값)이 기준
            Depth = Mathf.Max(0f, sep - baseline);

            if (!condition) { pushTime = 0f; warnHeld = 0f; Ratio = 0f; return Result.None; }

            bool pushing = Depth >= pushingDepth;
            if (pushing) { pushTime += dt; idleTime = 0f; }
            else
            {
                idleTime += dt;
                if (idleTime >= resetGap) { pushTime = 0f; warnHeld = 0f; }
            }

            float depthRatio = Depth / Mathf.Max(0.0001f, damageDepth);
            float timeRatio = damageTime > 0f ? pushTime / damageTime : 0f;
            Ratio = Mathf.Clamp01(Mathf.Max(depthRatio, timeRatio));

            bool warn = Depth >= warnDepth || (warnTime > 0f && pushTime >= warnTime);
            bool over = Depth >= damageDepth || (damageTime > 0f && pushTime >= damageTime);

            if (warn || over) warnHeld += dt; else warnHeld = 0f;
            if (over && warnHeld >= minWarnHold) return Result.Damage;
            return warn ? Result.Warn : Result.None;
        }

        public string Describe() { return $"깊이 {Depth * 100f:0.0}cm, 민 시간 {pushTime:0.00}초"; }
    }
}
