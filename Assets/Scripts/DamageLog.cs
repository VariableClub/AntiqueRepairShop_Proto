using UnityEngine;
using System.Collections.Generic;

// 억지 조작으로 생긴 손상과, 그에 따른 "기억 손상"을 아이템별로 기록한다.
// 기억 연출(4주차 연동) 쪽에서는 GetMemoryState / GetMemoryIntegrity 만 읽으면 된다.
public enum MemoryState
{
    Intact,     // 0단계: 온전한 기억
    Blurred,    // 1단계(흠집): 기억 일부가 흐려짐 (노이즈·색 바램)
    Fragmented, // 2단계(손상): 기억 조각 하나가 빠짐 (장면 일부 생략)
    Broken      // 3단계(파손): 기억이 왜곡되거나 끊김 (결말 분기용)
}

public class DamageRecord
{
    public int level;
    public readonly List<string> reasons = new List<string>();
}

public static class DamageLog
{
    public const int MaxLevel = 3;

    static readonly Dictionary<string, DamageRecord> records = new Dictionary<string, DamageRecord>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Init() { records.Clear(); }

    // 손상 1단계 누적. 이미 최대 단계면 기록만 하고 단계는 그대로.
    public static int AddDamage(string itemId, string reason)
    {
        if (!records.TryGetValue(itemId, out var rec))
        {
            rec = new DamageRecord();
            records.Add(itemId, rec);
        }

        rec.level = Mathf.Min(rec.level + 1, MaxLevel);
        rec.reasons.Add(reason);
        Debug.Log($"[손상 기록] {itemId}: {rec.level}단계 ({reason}) → 기억 상태 {GetMemoryState(itemId)}");
        return rec.level;
    }

    // 이전 코드 호환용
    public static void Record(string itemId) { AddDamage(itemId, "기록"); }

    public static int GetLevel(string itemId)
    {
        return records.TryGetValue(itemId, out var rec) ? rec.level : 0;
    }

    public static bool IsDamaged(string itemId) { return GetLevel(itemId) > 0; }

    public static MemoryState GetMemoryState(string itemId) { return (MemoryState)GetLevel(itemId); }

    // 1 = 온전, 0 = 완전 파손. 기억 연출의 노이즈 강도 등에 그대로 쓰면 된다.
    public static float GetMemoryIntegrity(string itemId)
    {
        return 1f - (float)GetLevel(itemId) / MaxLevel;
    }

    public static IReadOnlyList<string> GetReasons(string itemId)
    {
        return records.TryGetValue(itemId, out var rec) ? rec.reasons : (IReadOnlyList<string>)System.Array.Empty<string>();
    }

    public static void Clear(string itemId) { records.Remove(itemId); }
    public static void Clear() { records.Clear(); }
}
