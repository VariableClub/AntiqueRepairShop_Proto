using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    // 조작 피드백 소리 (REP-01 부품 결합, REP-02 틀린 조작, REP-03 억지 경고, REP-04 억지 손상, SND-01).
    // 이벤트 버스(GameEvents)만 듣고, 판정 스크립트는 소리를 모른다 (21-2 '받는 시스템: 효과음·햅틱').
    // 진동은 판정 스크립트가 쥔 손에 바로 보낸다 (Haptics 패턴 표). 같은 결과에는 같은 소리·진동을 함께 낸다(18장).
    public class FeedbackAudio : MonoBehaviour
    {
        [Serializable]
        public class ItemSpot
        {
            public string itemId;
            [Tooltip("소리가 나는 곳 (물건 본체)")]
            public Transform where;
            [Tooltip("억지 손상 소리 등 물건별 값")]
            public AntiqueData data;
            [Tooltip("틀린 조작 소리: 곰 인형은 둔탁한 클릭, 거울은 낮은 유리음 (11장)")]
            public SfxKey wrongSound = SfxKey.WrongCloth;
        }

        public List<ItemSpot> items = new List<ItemSpot>();
        [Tooltip("억지 경고 삐걱 소리의 최대 크기")]
        [Range(0f, 1f)] public float creakMaxVolume = 0.9f;
        [Tooltip("경고가 끊기고 이만큼 지나면 삐걱 소리가 줄어든다 (초)")]
        public float creakRelease = 0.15f;

        AudioSource creak;
        float creakTarget, lastWarnTime = -10f;

        void Start()
        {
            if (Sfx.Instance != null) creak = Sfx.Instance.MakeLoop(SfxKey.Creak, transform);
        }

        void OnEnable()
        {
            GameEvents.PartFitted += OnFitted;
            GameEvents.WrongOperation += OnWrong;
            GameEvents.ForceWarning += OnWarning;
            GameEvents.ForceDamaged += OnDamaged;
            GameEvents.StepCompleted += OnStep;
            GameEvents.WrongProcessPlace += OnWrongPlace;
            GameEvents.ProcessCompleted += OnProcessed;
        }

        void OnDisable()
        {
            GameEvents.PartFitted -= OnFitted;
            GameEvents.WrongOperation -= OnWrong;
            GameEvents.ForceWarning -= OnWarning;
            GameEvents.ForceDamaged -= OnDamaged;
            GameEvents.StepCompleted -= OnStep;
            GameEvents.WrongProcessPlace -= OnWrongPlace;
            GameEvents.ProcessCompleted -= OnProcessed;
        }

        ItemSpot Spot(string item)
        {
            foreach (var s in items) if (s != null && s.itemId == item) return s;
            return null;
        }

        Vector3 Where(string item)
        {
            var s = Spot(item);
            if (s != null && s.where != null) return s.where.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward * 0.5f : Vector3.zero;
        }

        void OnFitted(string item, string key) { Sfx.Play(SfxKey.Fit, Where(item)); }

        void OnWrong(string item, int step)
        {
            var s = Spot(item);
            Sfx.Play(s != null ? s.wrongSound : SfxKey.WrongCloth, Where(item));
        }

        void OnStep(string item, int step) { if (item != "prologue") Sfx.Play(SfxKey.StepDone, Where(item), 0.6f); }

        void OnWarning(string item, int step, float ratio)
        {
            lastWarnTime = Time.time;
            creakTarget = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(ratio)) * creakMaxVolume;
            if (creak != null)
            {
                creak.transform.position = Where(item);
                if (!creak.isPlaying && creak.clip != null) creak.Play();
            }
        }

        void OnDamaged(string item, int step, string reason)
        {
            creakTarget = 0f;
            if (creak != null) creak.volume = 0f;
            var s = Spot(item);
            SfxKey key = SfxKey.Tear;
            if (s != null && s.data != null) key = s.data.DamageSoundFor(step);
            Sfx.Play(key, Where(item));
        }

        void OnWrongPlace(string item, string zone) { Sfx.Play(SfxKey.WrongPlace, Where(item)); }

        void OnProcessed(string item, string kind) { Sfx.Play(SfxKey.Place, Where(item)); }

        void Update()
        {
            if (creak == null) return;
            if (Time.time - lastWarnTime > creakRelease) creakTarget = 0f;
            creak.volume = Mathf.MoveTowards(creak.volume, creakTarget, Time.deltaTime * 6f);
            if (creak.volume <= 0.001f && creakTarget <= 0f && creak.isPlaying) creak.Stop();
        }
    }
}
