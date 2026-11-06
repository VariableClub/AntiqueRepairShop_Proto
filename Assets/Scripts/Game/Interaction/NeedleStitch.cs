using System;
using UnityEngine;

namespace Dokkaebi
{
    // 맞추기(순서) - 바늘구멍 꿰매기 (곰 인형 5단계, 13장).
    //  - 바늘을 쥔 채 바늘 끝이 구멍 중심 1.5cm 안에 들어오면 통과 (Q-054 예외: 놓지 않고 판정)
    //  - 순서가 틀린 구멍이면 틀린 조작 (오조작 +1, 둔탁한 진동). 쥔 바늘은 튕길 수 없어 진동으로 알린다
    //  - 4번째 구멍을 통과한 뒤 바늘을 15cm 이상 당기면 완료 (마지막 실 당기기)
    //  - 놓아도 진행도는 유지 (Q-183). 실은 미리 만든 모델을 켠다 (Q-181)
    public class NeedleStitch : MonoBehaviour
    {
        public string itemId = "bear";
        public int step = 5;
        public Grabbable needle;
        public Transform tip;
        public Transform[] holes;
        public float tolerance = 0.015f;
        public float pullDistance = 0.15f;
        public float wrongCooldown = 0.6f;
        public bool active;

        public int Passed { get; private set; }
        public bool Completed { get; private set; }
        public event Action<int> HolePassed;
        public event Action OnCompleted;

        float nextWrong;

        void Update()
        {
            if (!active || Completed || needle == null || !needle.IsHeld || tip == null || holes == null || holes.Length == 0) return;

            if (Passed < holes.Length)
            {
                for (int i = 0; i < holes.Length; i++)
                {
                    if (i < Passed) continue; // 이미 통과한 구멍은 무시
                    if (Vector3.Distance(tip.position, holes[i].position) > tolerance) continue;

                    if (i == Passed)
                    {
                        Passed++;
                        Haptics.Send(needle.Hand, Haptics.FitAmp, Haptics.FitDur);
                        Debug.Log($"[꿰매기] {itemId}: 바늘구멍 {Passed}/{holes.Length} 통과");
                        HolePassed?.Invoke(Passed);
                    }
                    else if (Time.time >= nextWrong)
                    {
                        nextWrong = Time.time + wrongCooldown;
                        Haptics.Send(needle.Hand, Haptics.WrongAmp, Haptics.WrongDur);
                        Debug.Log($"[꿰매기] {itemId}: {i + 1}번 구멍은 순서가 아님 (다음은 {Passed + 1}번)");
                        RepairRecord.AddWrong(itemId, step);
                        GameEvents.RaiseWrong(itemId, step);
                    }
                    break;
                }
            }
            else
            {
                float pulled = Vector3.Distance(tip.position, holes[holes.Length - 1].position);
                if (pulled >= pullDistance)
                {
                    Completed = true;
                    Haptics.Send(needle.Hand, 0.6f, 0.2f);
                    Debug.Log($"[꿰매기] {itemId}: 마지막 실을 {pulled * 100f:0}cm 당김 → 완료");
                    OnCompleted?.Invoke();
                }
            }
        }

        // 에디터 확인용
        public void DebugPassAll()
        {
            while (Passed < holes.Length) { Passed++; HolePassed?.Invoke(Passed); }
            Completed = true;
            OnCompleted?.Invoke();
        }
    }
}
