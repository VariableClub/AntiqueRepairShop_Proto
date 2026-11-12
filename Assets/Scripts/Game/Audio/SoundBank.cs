using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    // 기능 효과음 이름 (SND-01). 18장 조작 피드백 표·도깨비·밤 전환.
    public enum SfxKey
    {
        Fit,            // 맞는 조작: 맑은 소리 (프롤로그 도구 정리 소리 = 이후 맞는 조작 소리, 10장)
        WrongCloth,     // 틀린 조작: 둔탁한 클릭 (곰 인형)
        WrongGlass,     // 틀린 조작: 낮은 유리음 (거울, 1월)
        Creak,          // 억지 경고: 삐걱·당겨지는 소리 (반복 재생, 위험도에 따라 크기)
        Tear,           // 억지 손상: 찢김 (곰 2단계)
        Thud,           // 억지 손상: 뭉침 (곰 3단계)
        Crack,          // 억지 손상: 금 (곰 4단계)
        Extract,        // 꺼냄
        StepDone,       // 단계 완료 (기억 소리 한 겹이 함께 쌓임)
        RepairDone,     // 마지막 실: 따뜻한 효과음 (13장)
        DoorBell,       // 출입문 종
        Appear,         // 도깨비 등장 (빛·연기)
        Transform,      // 도깨비 ↔ 물건 변신
        LampOn,         // 스탠드 점등 (EP0-01)
        Pen,            // 밤 전환: 펜으로 긋는 사각사각 (작업대 왼쪽 뒤)
        Place,          // 처리 자리에 올려 둠
        WrongPlace,     // 잘못된 처리 자리 (튕김)
        LatencyClick    // 에어팟 지연 확인용 짧은 클릭 (SND-04)
    }

    // 효과음 묶음. MVP는 임시음 (D173: 정식 효과음은 12월 이후 강민우).
    // 임시음은 Assets/Audio/Placeholder/에 있고, 소리를 바꿀 때는 이 에셋의 칸만 바꾼다.
    [CreateAssetMenu(menuName = "도깨비/효과음 묶음", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxKey key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("재생할 때마다 음높이를 조금씩 바꿔 반복이 덜 들리게 (0이면 그대로)")]
            [Range(0f, 0.2f)] public float pitchJitter = 0.03f;
            [Tooltip("0 = 머리에서 들림(2D), 1 = 위치에서 들림(3D)")]
            [Range(0f, 1f)] public float spatial = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(SfxKey key)
        {
            foreach (var e in entries) if (e != null && e.key == key) return e;
            return null;
        }
    }
}
