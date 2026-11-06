using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Dokkaebi
{
    // 물건별 수선 기록 (REP-04 손상 기록, REP-02 오조작 횟수).
    // 기획서 11장: 한 물건에서 억지가 여러 번 일어나도 손상은 한 번으로 기록하고,
    // 손상된 단계는 단계마다 남긴다(21-4). 저장(23장)은 12월 SAV 작업에서 이 값을 쓴다.
    public static class RepairRecord
    {
        class Entry
        {
            public bool damaged;
            public readonly List<int> damagedSteps = new List<int>();
            public readonly List<string> reasons = new List<string>();
            public int wrongCount;
        }

        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { entries.Clear(); }

        static Entry Get(string item)
        {
            if (!entries.TryGetValue(item, out var e)) { e = new Entry(); entries.Add(item, e); }
            return e;
        }

        // 억지 손상 기록. 이 단계가 처음 손상되면 true.
        public static bool MarkDamage(string item, int step, string reason)
        {
            var e = Get(item);
            bool firstForItem = !e.damaged;
            e.damaged = true;
            e.reasons.Add($"{step}단계: {reason}");
            bool newStep = !e.damagedSteps.Contains(step);
            if (newStep) e.damagedSteps.Add(step);
            Debug.Log($"[손상 기록] {item}: {step}단계 {reason} → 물건 손상 {(firstForItem ? "기록(1회)" : "이미 기록됨")}, 손상 단계 [{string.Join(", ", e.damagedSteps)}]");
            return newStep;
        }

        public static void AddWrong(string item, int step)
        {
            var e = Get(item);
            e.wrongCount++;
            Debug.Log($"[오조작] {item}: {step}단계 틀린 조작 (누적 {e.wrongCount}회, 기억에는 영향 없음)");
        }

        public static bool IsDamaged(string item) { return entries.TryGetValue(item, out var e) && e.damaged; }
        public static bool IsStepDamaged(string item, int step) { return entries.TryGetValue(item, out var e) && e.damagedSteps.Contains(step); }
        public static IReadOnlyList<int> DamagedSteps(string item)
        {
            return entries.TryGetValue(item, out var e) ? e.damagedSteps : (IReadOnlyList<int>)System.Array.Empty<int>();
        }
        public static int WrongCount(string item) { return entries.TryGetValue(item, out var e) ? e.wrongCount : 0; }

        public static string Summary(string item)
        {
            if (!entries.TryGetValue(item, out var e)) return $"{item}: 손상 없음, 오조작 0회";
            var sb = new StringBuilder();
            sb.Append($"{item}: 손상 {(e.damaged ? "있음" : "없음")}");
            if (e.damaged) sb.Append($" (단계 [{string.Join(", ", e.damagedSteps)}] / {string.Join(" · ", e.reasons)})");
            sb.Append($", 오조작 {e.wrongCount}회");
            return sb.ToString();
        }

        public static void Clear() { entries.Clear(); }
    }
}
