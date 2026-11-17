using System;
using UnityEngine;

namespace Dokkaebi
{
    // 맞추기(순서) - 바늘구멍 꿰매기 (곰 인형 5단계, 13장).
    //  - 바늘을 쥔 채 바늘 끝이 구멍 중심 1.5cm 안에 들어오면 통과 (Q-054 예외: 놓지 않고 판정)
    //  - 순서가 틀린 구멍이면 틀린 조작 (오조작 +1, 둔탁한 진동). 쥔 바늘은 튕길 수 없어 진동으로 알린다
    //  - 4번째 구멍을 통과한 뒤 바늘을 15cm 이상 당기면 완료 (마지막 실 당기기)
    //  - 놓아도 진행도는 유지 (Q-183). 실은 미리 만든 모델을 켠다 (Q-181)
    //  - 바늘은 양 끝이 똑같이 생겨서 어느 끝으로 찔러도 통과로 본다
    //  - 다음에 꿸 구멍은 밝게 커졌다 작아지며 알려 준다 (순서를 몰라 틀린 구멍만 찌르는 일을 막는다)
    public class NeedleStitch : MonoBehaviour
    {
        public string itemId = "bear";
        public int step = 5;
        public Grabbable needle;
        public Transform tip;
        public Transform[] holes;
        [Tooltip("바늘 끝이 구멍 중심에서 이 거리 안이면 통과 (m). 13장 시작값 1.5cm에서 실기 확인 뒤 넓힘")]
        public float tolerance = 0.02f;
        public float pullDistance = 0.15f;
        public float wrongCooldown = 0.6f;
        public bool active;
        [Tooltip("다음에 꿸 구멍 색")]
        public Color nextHoleColor = new Color(1f, 0.85f, 0.3f);
        [Tooltip("다음에 꿸 구멍이 커지는 배율")]
        public float nextHoleScale = 1.8f;

        public int Passed { get; private set; }
        public bool Completed { get; private set; }
        public event Action<int> HolePassed;
        public event Action OnCompleted;

        float nextWrong;
        Vector3[] holeScales;
        Renderer[] holeRenderers;
        MaterialPropertyBlock block;
        int shown = -1;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            int n = holes != null ? holes.Length : 0;
            holeScales = new Vector3[n];
            holeRenderers = new Renderer[n];
            for (int i = 0; i < n; i++)
            {
                if (holes[i] == null) continue;
                holeScales[i] = holes[i].localScale;
                holeRenderers[i] = holes[i].GetComponent<Renderer>();
            }
        }

        // 다음 구멍 하나만 밝게 키운다. next가 범위 밖이면 모두 원래대로
        void ShowNext(int next)
        {
            if (holeScales == null) return;
            if (next != shown)
            {
                for (int i = 0; i < holeScales.Length; i++)
                {
                    if (holes[i] == null) continue;
                    holes[i].localScale = holeScales[i];
                    if (holeRenderers[i] == null) continue;
                    if (i == next) { block.SetColor(BaseColorId, nextHoleColor); holeRenderers[i].SetPropertyBlock(block); }
                    else holeRenderers[i].SetPropertyBlock(null);
                }
                shown = next;
            }
            if (next >= 0 && next < holeScales.Length && holes[next] != null)
                holes[next].localScale = holeScales[next] * Mathf.Lerp(1f, nextHoleScale, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
        }

        // 구멍에 더 가까운 쪽 바늘 끝까지의 거리 (양 끝이 똑같이 생겼다)
        float EndDistance(Vector3 point)
        {
            Vector3 other = needle.transform.TransformPoint(-needle.transform.InverseTransformPoint(tip.position));
            return Mathf.Min(Vector3.Distance(tip.position, point), Vector3.Distance(other, point));
        }

        void Update()
        {
            bool ready = active && !Completed && needle != null && tip != null && holes != null && holes.Length > 0;
            ShowNext(ready && Passed < holes.Length ? Passed : -1);
            if (!ready || !needle.IsHeld) return;

            if (Passed < holes.Length)
            {
                for (int i = 0; i < holes.Length; i++)
                {
                    if (i < Passed) continue; // 이미 통과한 구멍은 무시
                    if (EndDistance(holes[i].position) > tolerance) continue;

                    if (i == Passed)
                    {
                        Passed++;
                        Haptics.Play(needle.Hand, Haptics.Fit);
                        Debug.Log($"[꿰매기] {itemId}: 바늘구멍 {Passed}/{holes.Length} 통과");
                        GameEvents.RaisePartFitted(itemId, $"hole{Passed}"); // 순서 맞추기 성공 = 부품 결합 (21-2)
                        HolePassed?.Invoke(Passed);
                    }
                    else if (Time.time >= nextWrong)
                    {
                        nextWrong = Time.time + wrongCooldown;
                        Haptics.Play(needle.Hand, Haptics.Wrong);
                        Debug.Log($"[꿰매기] {itemId}: {i + 1}번 구멍은 순서가 아님 (다음은 {Passed + 1}번)");
                        RepairRecord.AddWrong(itemId, step);
                        GameEvents.RaiseWrong(itemId, step);
                    }
                    break;
                }
            }
            else
            {
                float pulled = EndDistance(holes[holes.Length - 1].position);
                if (pulled >= pullDistance)
                {
                    Completed = true;
                    Haptics.Play(needle.Hand, Haptics.RepairDone);
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
