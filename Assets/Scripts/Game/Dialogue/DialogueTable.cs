using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    public enum Speaker
    {
        Dokkaebi,     // 도깨비: 그 도깨비나 물건 옆
        Protagonist,  // 주인공: 작업대 위 시선 높이
        MemoryPerson, // 넷째 밤 기억 속 인물: 실루엣 옆 (3월)
        System        // 밤 전환 글자 등
    }

    [Serializable]
    public class DialogueLine
    {
        [Tooltip("대본의 대사 ID (D01, H01, N01, P01 …)")]
        public string id;
        public Speaker speaker;
        [Tooltip("글자. 구절을 나누고 싶은 곳에 / 를 넣는다. 없으면 20자 안에서 어절 단위로 자동으로 나눈다")]
        [TextArea(2, 5)] public string text;
        [Tooltip("다음으로 넘어가는 조건 (메모). 예: 변신 끝, 단계 완료")]
        public string next;
    }

    // 대사 데이터 (UI-03). 대본(신주연)이 기준이고, 이 표는 대본을 옮겨 적는다 (Q-094).
    [CreateAssetMenu(fileName = "Dialogue", menuName = "도깨비/대사 표")]
    public class DialogueTable : ScriptableObject
    {
        public List<DialogueLine> lines = new List<DialogueLine>();

        public DialogueLine Find(string id)
        {
            foreach (var l in lines) if (l.id == id) return l;
            return null;
        }
    }
}
