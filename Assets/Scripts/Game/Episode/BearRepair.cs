using System;
using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 곰 인형 5단계 (EP1-01) + 단계 완료 판정 (REP-05). 기획서 13장, 11장 공통 규칙.
    //  1 살피기: 흔들기 2회
    //  2 꺼내기: 단추와 쪽지를 둘 다 배 밖으로 10cm
    //  3 넣기: 솜 3번
    //  4 맞추기: 단추를 눈 자리에 (1.5cm, 기울기 ±20°)
    //  5 맞추기(순서): 바늘구멍 4개 → 실 15cm 당기기
    // 판정은 현재 단계에서만 켠다. 예외: 살피기 단계에서는 다음 단계(꺼내기)도 함께 켜 두고,
    // 꺼내기가 먼저 끝나면 1·2단계를 차례로 완료한다 (11장, 21-4, 결정 1 재검토 11/6).
    public class BearRepair : MonoBehaviour
    {
        public string itemId = "bear";
        public Grabbable bear;
        public ShakeDetector shake;
        public PullOutJudge buttonPull;
        public PullOutJudge notePull;
        public InsertZone belly;
        public FitSocket eye;
        public NeedleStitch stitch;
        public BearView view;
        public int shakesRequired = 2;
        [Tooltip("자동 완료된 단계 사이의 간격 (기억 소리 1·2겹을 차례로 더함, Q-281)")]
        public float autoCompleteGap = 0.5f;

        public int CurrentStep { get; private set; } // 0 = 시작 전, 1~5, 6 = 수선 완료
        public bool IsRepaired => CurrentStep > 5;
        public event Action<int> StepCompleted;
        public event Action Repaired;

        void OnEnable()
        {
            if (shake != null) shake.Shaken += OnShaken;
            if (buttonPull != null) buttonPull.OnExtracted += OnExtracted;
            if (notePull != null) notePull.OnExtracted += OnExtracted;
            if (belly != null) belly.Inserted += OnInserted;
            if (eye != null) eye.Fitted += OnEyeFitted;
            if (stitch != null) { stitch.HolePassed += OnHolePassed; stitch.OnCompleted += OnStitchDone; }
        }

        void OnDisable()
        {
            if (shake != null) shake.Shaken -= OnShaken;
            if (buttonPull != null) buttonPull.OnExtracted -= OnExtracted;
            if (notePull != null) notePull.OnExtracted -= OnExtracted;
            if (belly != null) belly.Inserted -= OnInserted;
            if (eye != null) eye.Fitted -= OnEyeFitted;
            if (stitch != null) { stitch.HolePassed -= OnHolePassed; stitch.OnCompleted -= OnStitchDone; }
        }

        public void Begin()
        {
            CurrentStep = 1;
            Debug.Log($"[수선] {itemId}: 수선 시작 (1단계 살피기)");
            ApplyStep();
        }

        void ApplyStep()
        {
            int s = CurrentStep;
            if (shake != null) shake.active = s >= 1 && s <= 5; // 흔들기 자체는 계속 가능 (Q-045, Q-179 '흔들기는 소리만')
            if (buttonPull != null) buttonPull.active = s == 1 || s == 2;
            if (notePull != null) notePull.active = s == 1 || s == 2;
            if (belly != null) belly.active = s == 3;
            if (eye != null) eye.active = s == 4;
            if (stitch != null) stitch.active = s == 5;

            // 꺼내기 단계가 아닐 때는 배 안의 부품을 잡을 수 없다 (현재 단계만 판정)
            SetPartGrab(buttonPull, s == 1 || s == 2 || (buttonPull != null && buttonPull.Extracted));
            SetPartGrab(notePull, s == 1 || s == 2 || (notePull != null && notePull.Extracted));
        }

        void SetPartGrab(PullOutJudge p, bool allowed)
        {
            if (p == null) return;
            var g = p.GetComponent<Grabbable>();
            if (g != null) g.SetAllowed(allowed && !g.Locked);
        }

        // 상태 게이트(GameFlow)가 잡기를 다시 켰을 때 단계 조건을 다시 적용한다
        public void ReapplyGrabRules() { if (CurrentStep >= 1) ApplyStep(); }

        void OnShaken(int count)
        {
            // 단추가 배 안에 있으면 달그락 (진동 + 배 천이 들썩임, Q-254)
            bool rattle = buttonPull != null && !buttonPull.Extracted;
            if (rattle)
            {
                Haptics.Play(bear.Hand, Haptics.Rattle);
                if (view != null) view.PulseBelly();
            }
            Debug.Log($"[살피기] {itemId}: 흔들기 {count}회{(rattle ? " (배 쪽에서 달그락)" : "")}");
            if (CurrentStep == 1 && count >= shakesRequired) Complete(1);
        }

        void OnExtracted(PullOutJudge p)
        {
            bool both = buttonPull.Extracted && notePull.Extracted;
            if (!both) return;
            if (CurrentStep == 1) StartCoroutine(CompleteOneAndTwo());
            else if (CurrentStep == 2) Complete(2);
        }

        IEnumerator CompleteOneAndTwo()
        {
            Debug.Log($"[수선] {itemId}: 흔들기 전에 꺼내기를 마쳐 1·2단계를 함께 완료");
            Complete(1);
            yield return new WaitForSeconds(autoCompleteGap);
            Complete(2);
        }

        void OnInserted(int count)
        {
            if (view != null) view.SetStuffing(count / (float)belly.required);
            if (CurrentStep == 3 && count >= belly.required) Complete(3);
        }

        void OnEyeFitted(FitSocket s, Grabbable g)
        {
            if (CurrentStep == 4) Complete(4);
        }

        void OnHolePassed(int n) { if (view != null) view.SetThreads(n); }

        void OnStitchDone() { if (CurrentStep == 5) Complete(5); }

        public void Complete(int step)
        {
            if (step != CurrentStep) return;
            Debug.Log($"[단계 완료] {itemId}: {step}단계{(RepairRecord.IsStepDamaged(itemId, step) ? " (손상된 단계 → 기억 소리는 갈라진 소리로)" : "")}");
            if (view != null) view.OnStepCompleted(step);
            GameEvents.RaiseStepCompleted(itemId, step);
            StepCompleted?.Invoke(step);
            CurrentStep = step + 1;
            ApplyStep();
            if (CurrentStep > 5)
            {
                Debug.Log($"[수선] {itemId}: 수선 완료 → {RepairRecord.Summary(itemId)}");
                Repaired?.Invoke();
            }
        }

        // ---- 에디터 확인용 (헤드셋 없이 흐름 확인) ----
        public void DebugCompleteCurrent()
        {
            switch (CurrentStep)
            {
                case 1: Complete(1); break;
                case 2: buttonPull.Extract(); notePull.Extract(); break;
                case 3: StartCoroutine(DebugInsertAll()); break;
                case 4:
                    var g = buttonPull.GetComponent<Grabbable>();
                    eye.Fit(g);
                    break;
                case 5: stitch.DebugPassAll(); break;
            }
        }

        // 솜 상자는 한 번에 한 덩어리만 내놓으므로, 새 덩어리가 생기기를 기다리며 채운다
        IEnumerator DebugInsertAll()
        {
            for (int guard = 0; guard < 120 && CurrentStep == 3 && belly.Count < belly.required; guard++)
            {
                DebugInsertCotton();
                yield return null;
            }
        }

        void DebugInsertCotton()
        {
            foreach (var g in Grabbable.All)
            {
                if (g.key != belly.acceptKey || !g.gameObject.activeInHierarchy) continue;
                g.transform.position = belly.transform.position;
                belly.TryReceive(g);
                return;
            }
        }

        public void DebugDamageCurrent()
        {
            int step = CurrentStep;
            if (step < 2 || step > 4) { Debug.Log("[확인용] 억지 지점은 2·3·4단계"); return; }
            string reason = "확인용 억지";
            RepairRecord.MarkDamage(itemId, step, reason);
            GameEvents.RaiseForceDamaged(itemId, step, reason);
        }
    }
}
