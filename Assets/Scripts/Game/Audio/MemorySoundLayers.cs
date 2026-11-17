using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    // 기억 소리 (MEM-01 쌓기, MEM-02 손상 단계 갈라진 소리, MEM-03 소리 → 영상 교차 페이드). 기획서 14장 '기억 소리'.
    //  - 단계 완료마다 그 단계의 한 겹을 반복 재생으로 더한다. 겹끼리 박자를 맞추려고 모두 같은 시각(dspTime) 기준으로 시작한다
    //  - 억지가 일어난 단계의 겹은 갈라진 소리로: 따로 만든 소리가 있으면 그것을, 없으면 같은 소리를
    //    음높이를 낮추고(늘어진 소리) 찌그러뜨리고(Distortion) 높은음을 깎아(Low Pass) 재생한다 (가안)
    //  - 기억 트리거: 쌓인 겹을 1.5초 동안 줄이며 영상 소리를 처음부터 재생 (교차 페이드)
    //  - 소리 겹은 머리에서 들리게(2D) 한다. 물건 고유음·효과음과 섞이지 않게 (가안)
    public class MemorySoundLayers : MonoBehaviour
    {
        public AntiqueData data;
        [Range(0f, 1f)] public float layerVolume = 0.55f;
        [Tooltip("한 겹이 들어올 때 커지는 시간 (초)")]
        public float layerFadeIn = 1.2f;
        public float crossFade = 1.5f;
        [Range(0f, 1f)] public float memoryVolume = 0.9f;

        [Header("갈라진 소리 (따로 만든 소리가 없을 때)")]
        public float damagedPitch = 0.86f;
        [Range(0f, 1f)] public float damagedDistortion = 0.55f;
        public float damagedLowPass = 2600f;

        readonly List<AudioSource> layers = new List<AudioSource>();
        AudioSource memorySource;
        double startDsp = -1;
        bool memoryPlaying;

        public int LayerCount => layers.Count;
        public AudioSource MemorySource => memorySource;

        void OnEnable()
        {
            GameEvents.StepCompleted += OnStep;
            GameEvents.MemoryTriggered += OnMemory;
            GameEvents.MemoryEnded += OnMemoryEnded;
        }

        void OnDisable()
        {
            GameEvents.StepCompleted -= OnStep;
            GameEvents.MemoryTriggered -= OnMemory;
            GameEvents.MemoryEnded -= OnMemoryEnded;
        }

        void OnStep(string item, int step)
        {
            if (data == null || item != data.itemId) return;
            bool damaged = RepairRecord.IsStepDamaged(item, step);
            var clip = damaged && data.DamagedLayerFor(step) != null ? data.DamagedLayerFor(step) : data.LayerFor(step);
            if (clip == null) { Debug.Log($"[기억 소리] {item}: {step}단계 소리 파일 없음 (신주연 단계별 소리 5개)"); return; }

            var go = new GameObject($"기억 소리 {step}겹{(damaged ? " (갈라짐)" : "")}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;

            bool synthDamage = damaged && data.DamagedLayerFor(step) == null;
            if (synthDamage)
            {
                src.pitch = damagedPitch;
                var dist = go.AddComponent<AudioDistortionFilter>();
                dist.distortionLevel = damagedDistortion;
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = damagedLowPass;
            }

            // 첫 겹의 시작 시각에 맞춰 같은 박자 위치에서 들어온다
            double now = AudioSettings.dspTime;
            if (startDsp < 0) startDsp = now + 0.05;
            double when = now + 0.05;
            if (!synthDamage && clip.length > 0.1f)
            {
                double offset = (when - startDsp) % clip.length;
                src.time = (float)offset;
            }
            src.PlayScheduled(when);
            layers.Add(src);
            StartCoroutine(Fade(src, layerVolume, layerFadeIn));
            Debug.Log($"[기억 소리] {item}: {step}단계 한 겹 추가{(damaged ? " (손상 단계 → 갈라진 소리)" : "")}, 지금 {layers.Count}겹");
        }

        void OnMemory(string item)
        {
            if (data == null || item != data.itemId || memoryPlaying) return;
            memoryPlaying = true;
            foreach (var l in layers) StartCoroutine(Fade(l, 0f, crossFade, stopAtEnd: true));

            if (data.memoryAudio != null)
            {
                if (memorySource == null)
                {
                    var go = new GameObject("기억 영상 소리");
                    go.transform.SetParent(transform, false);
                    memorySource = go.AddComponent<AudioSource>();
                    memorySource.playOnAwake = false;
                    memorySource.spatialBlend = 0f;
                }
                memorySource.clip = data.memoryAudio;
                memorySource.time = 0f;
                memorySource.volume = 0f;
                memorySource.Play();
                StartCoroutine(Fade(memorySource, memoryVolume, crossFade));
                Debug.Log($"[기억 소리] {item}: 쌓인 {layers.Count}겹을 {crossFade}초 동안 줄이며 영상 소리를 처음부터 재생");
            }
            else Debug.Log($"[기억 소리] {item}: 영상 소리 파일 없음. 쌓인 겹만 줄임");
        }

        void OnMemoryEnded(string item)
        {
            if (data == null || item != data.itemId) return;
            if (memorySource != null && memorySource.isPlaying) StartCoroutine(Fade(memorySource, 0f, 1f, stopAtEnd: true));
        }

        // 다음 물건을 위해 비운다
        public void ResetLayers()
        {
            StopAllCoroutines();
            foreach (var l in layers) if (l != null) Destroy(l.gameObject);
            layers.Clear();
            startDsp = -1;
            memoryPlaying = false;
            if (memorySource != null) memorySource.Stop();
        }

        IEnumerator Fade(AudioSource src, float to, float time, bool stopAtEnd = false)
        {
            if (src == null) yield break;
            float from = src.volume;
            for (float t = 0f; t < time && src != null; t += Time.deltaTime)
            {
                src.volume = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            if (src == null) yield break;
            src.volume = to;
            if (stopAtEnd && to <= 0f) src.Stop();
        }
    }
}
