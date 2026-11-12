using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    // 효과음을 위치에서 한 번 재생한다 (SND-01). 소리 칸은 AudioSource를 몇 개 미리 만들어 돌려 쓴다.
    // 씬에 하나 두고 bank를 넣는다. 없으면 재생하지 않고 로그만 남긴다(소리로만 전달하는 정보는 없으므로 진행에는 문제가 없다, 18장).
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        public SoundBank bank;
        public int voices = 12;
        [Tooltip("3D 소리가 거의 줄지 않는 거리 (m). 2×2m 공간이라 작게")]
        public float minDistance = 0.5f;
        public float maxDistance = 6f;

        readonly List<AudioSource> pool = new List<AudioSource>();
        int next;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < voices; i++) pool.Add(NewSource($"효과음 {i + 1}"));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource NewSource(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0f;
            return src;
        }

        // 반복 재생용 소리 칸 (억지 경고 삐걱 등). 호출한 쪽이 크기를 조절한다.
        public AudioSource MakeLoop(SfxKey key, Transform parent)
        {
            var e = bank != null ? bank.Find(key) : null;
            var src = NewSource($"반복 - {key}");
            src.transform.SetParent(parent != null ? parent : transform, false);
            src.loop = true;
            src.volume = 0f;
            if (e != null) { src.clip = e.clip; src.spatialBlend = e.spatial; }
            return src;
        }

        public static void Play(SfxKey key, Vector3 position, float volumeScale = 1f)
        {
            if (Instance == null) { Debug.Log($"[효과음] {key} (Sfx 없음)"); return; }
            Instance.PlayInternal(key, position, volumeScale);
        }

        public static float Length(SfxKey key)
        {
            var e = Instance != null && Instance.bank != null ? Instance.bank.Find(key) : null;
            return e != null && e.clip != null ? e.clip.length : 0f;
        }

        void PlayInternal(SfxKey key, Vector3 position, float volumeScale)
        {
            var e = bank != null ? bank.Find(key) : null;
            if (e == null || e.clip == null) { Debug.Log($"[효과음] {key} (소리 파일 없음)"); return; }
            var src = pool[next];
            next = (next + 1) % pool.Count;
            src.transform.position = position;
            src.clip = e.clip;
            src.volume = e.volume * volumeScale;
            src.spatialBlend = e.spatial;
            src.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
            src.Play();
        }
    }
}
