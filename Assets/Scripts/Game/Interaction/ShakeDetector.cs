using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    // 살피기 - 흔들기 (INP-02). 잡은 손(컨트롤러)의 가속도가 임계값을 넘는 봉우리 하나를 1회로 센다.
    //  - 임계값 15m/s² (13장 판정 시작값), 다음 1회는 0.25초 뒤부터 (Q-041, Q-042)
    //  - 한 프레임 값은 트래킹 흔들림으로 튀므로 짧은 구간 평균 속도의 변화로 가속도를 잰다
    [RequireComponent(typeof(Grabbable))]
    public class ShakeDetector : MonoBehaviour
    {
        public float threshold = 15f;
        public float refractory = 0.25f;
        [Tooltip("속도를 평균 내는 구간 (초)")]
        public float window = 0.04f;
        public bool active = true;

        public int Count { get; private set; }
        public float LastAcceleration { get; private set; }
        public event Action<int> Shaken;

        Grabbable grab;
        readonly Queue<(float t, Vector3 p)> positions = new Queue<(float, Vector3)>();
        readonly Queue<(float t, Vector3 v)> velocities = new Queue<(float, Vector3)>();
        float lastPeak = -10f;
        bool above;

        void Awake() { grab = GetComponent<Grabbable>(); }

        public void ResetCount() { Count = 0; }

        void Update()
        {
            if (!active || !grab.IsHeld) { positions.Clear(); velocities.Clear(); above = false; return; }

            float now = Time.time;
            positions.Enqueue((now, grab.Hand.position));
            while (positions.Count > 2 && now - positions.Peek().t > window) positions.Dequeue();
            var p0 = positions.Peek();
            if (now - p0.t < window * 0.5f) return;
            Vector3 v = (grab.Hand.position - p0.p) / (now - p0.t);

            velocities.Enqueue((now, v));
            while (velocities.Count > 2 && now - velocities.Peek().t > window) velocities.Dequeue();
            var v0 = velocities.Peek();
            if (now - v0.t < window * 0.5f) return;
            LastAcceleration = (v - v0.v).magnitude / (now - v0.t);

            if (!above && LastAcceleration >= threshold && now - lastPeak >= refractory)
            {
                above = true;
                lastPeak = now;
                Count++;
                Shaken?.Invoke(Count);
            }
            else if (above && LastAcceleration < threshold * 0.6f)
            {
                above = false;
            }
        }
    }
}
