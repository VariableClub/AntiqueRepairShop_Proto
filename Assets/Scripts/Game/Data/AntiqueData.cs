using System;
using UnityEngine;
using UnityEngine.Video;

namespace Dokkaebi
{
    public enum RepairAction { Inspect, ExtractInsert, Fit, Rotate, LookInto } // 살피기, 꺼내고 넣기, 맞추기, 돌리기, 들여다보기 (1장)
    public enum ForceKind { None, Pull, Push, Overturn }                       // 억지 없음, 세게 당기기, 밀어 넣기, 지나치게 돌리기 (11장)
    public enum ProcessKind { Return, Seal, Preserve }                         // 반환, 봉인, 보존 (15장)

    // 기억 영상 위에 덮는 손상 마스크 한 칸 (MEM-07). 부위는 신주연 기억 연출 정의서에서 정한다(14장).
    [Serializable]
    public class DamageMask
    {
        public bool enabled;
        [Tooltip("영상 화면 안의 영역 (0~1, 왼쪽 아래가 0,0)")]
        public Rect uv = new Rect(0.35f, 0.25f, 0.3f, 0.4f);
        [Tooltip("영상에서 마스크가 켜지는 구간 (초)")]
        public float start = 0f;
        public float end = 20f;
        [Tooltip("흐림·찢김 정도")]
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("검토용 설명 (어느 장면의 어디를 덮는지)")]
        public string note;
    }

    // 수선 단계 하나 (21-3 '단계 목록·억지 지점·발견 단계·힌트')
    [Serializable]
    public class RepairStepData
    {
        public string name;
        public RepairAction action;
        [Tooltip("완료 조건 (사람이 읽는 설명. 판정은 단계별 판정 컴포넌트가 한다)")]
        public string doneCondition;
        [Tooltip("스스로 알아내는 단계: 힌트를 늦게 준다 (20·30·40초)")]
        public bool discovery;
        [Tooltip("힌트 2단계 대사 키 (H01 등, 4주차 HNT)")]
        public string hintLine;
        public ForceKind force;
        [Tooltip("억지가 나면 바로 보이는 손상 모양 (11장)")]
        public string damageShape;
        public SfxKey damageSound = SfxKey.Tear;
        [Tooltip("손상 시 기억 영상에 덮을 부위")]
        public DamageMask mask = new DamageMask();
    }

    // 골동품 데이터 (SYS-04, 기획서 21-3). 새 골동품은 이 항목만 채워 만든다(1월 거울에서 검증, 17장).
    [CreateAssetMenu(menuName = "도깨비/골동품 데이터", fileName = "Antique_")]
    public class AntiqueData : ScriptableObject
    {
        [Header("기본")]
        public string itemId = "bear";
        public string displayName = "곰 인형";
        [Tooltip("몇째 밤 (1~4)")]
        public int night = 1;
        [Tooltip("밤 전환 때 암전 화면에 뜨는 글자 (이 물건이 오는 밤)")]
        public string nightLabel = "첫째 밤";

        [Header("모델 (비우면 회색 박스. 상태 S0~S5·손상 A·B는 MVP 정의서 6장)")]
        public GameObject brokenModel;
        public GameObject repairedModel;
        public GameObject[] damagedModels;

        [Header("도깨비")]
        [Tooltip("공통 몸체에 붙일 특징")]
        public string dokkaebiFeature = "단추 눈";
        [Tooltip("의뢰 첫 대사 키")]
        public string requestLine = "D01";
        [Tooltip("대사가 뜨는 동안 나는 물건 고유음 (NPC-04, 신주연)")]
        public AudioClip dokkaebiVoice;

        [Header("단계")]
        public RepairStepData[] steps = new RepairStepData[0];

        [Header("기억 단서")]
        [Tooltip("단계 중에 나오는 단서 (예: 2단계 쪽지)")]
        public string clueNote;

        [Header("기억 소리 (MEM-01·02)")]
        [Tooltip("단계마다 한 겹. 영상 소리에서 자른 것")]
        public AudioClip[] memoryLayers = new AudioClip[0];
        [Tooltip("손상 단계용 갈라진 소리. 비어 있으면 같은 소리를 갈라지게 바꿔 재생한다")]
        public AudioClip[] memoryLayersDamaged = new AudioClip[0];

        [Header("기억 영상 (MEM-05·06·07)")]
        [Tooltip("검은 배경 영상 (H.264 mp4)")]
        public VideoClip memoryVideo;
        [Tooltip("투명 배경 영상 (VP8 webm, 알파). Quest 3 재생 확인용 (MEM-06)")]
        public VideoClip memoryVideoAlpha;
        [Tooltip("영상 소리 (영상과 따로 둔다: 교차 페이드를 직접 하기 위해)")]
        public AudioClip memoryAudio;
        [Tooltip("영상 길이 (초). 영상이 없을 때 콘티 이미지 시간과 강제 종료 시간에 쓴다")]
        public float memoryLength = 20f;
        [Tooltip("영상을 불러오지 못하면 같은 길이 동안 차례로 보여줄 콘티 이미지")]
        public Texture2D[] storyboard = new Texture2D[0];
        [Tooltip("감정 조명 색 (스탠드). 세부 색값은 Unity 테스트에서 정한다")]
        public Color emotionColor = new Color(1f, 0.68f, 0.28f);
        [Tooltip("기억 뒤 주인공 반응 대사 키")]
        public string reactionLine = "P01";

        [Header("처리")]
        public string processLine = "D02";
        public ProcessKind process = ProcessKind.Return;
        [Tooltip("공간 변화 (다음 밤부터 남는 것)")]
        public string spaceChange = "출입문 옆 사진·꽃, 밝아진 조명";

        [Header("넷째 밤 연결 (3월)")]
        public string fourthNightReplacement = "잠드는 장면 → 시계를 벗어둔 장면";
        public string fourthNightTrace = "찢어진 천의 결";

        [Header("수선 일지 문장 틀 (24장, LLM 실패 시)")]
        [TextArea] public string journalNormal = "배 속의 단추를 찾아 다시 달아 주었다.";
        [TextArea] public string journalDamaged = "";

        public static string ProcessName(ProcessKind k)
        {
            switch (k) { case ProcessKind.Seal: return "봉인"; case ProcessKind.Preserve: return "보존"; default: return "반환"; }
        }

        public RepairStepData Step(int step) { return steps != null && step >= 1 && step <= steps.Length ? steps[step - 1] : null; }

        public SfxKey DamageSoundFor(int step)
        {
            var s = Step(step);
            return s != null ? s.damageSound : SfxKey.Tear;
        }

        public AudioClip LayerFor(int step) { return memoryLayers != null && step >= 1 && step <= memoryLayers.Length ? memoryLayers[step - 1] : null; }
        public AudioClip DamagedLayerFor(int step) { return memoryLayersDamaged != null && step >= 1 && step <= memoryLayersDamaged.Length ? memoryLayersDamaged[step - 1] : null; }
    }
}
