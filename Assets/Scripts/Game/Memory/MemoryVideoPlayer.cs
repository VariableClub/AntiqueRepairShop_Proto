using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Dokkaebi
{
    // 기억 영상 (MEM-05 재생, MEM-06 투명 배경 영상 확인, MEM-07 손상 마스크). 기획서 14장 '기억 영상', 21-4.
    //  - 플레이어 정면 작업대 너머 평면 화면 (1.5m, 가로 1.6m). 플레이어는 움직이지 않는다
    //  - 등장·소멸은 투명도 페이드
    //  - 재생 순서(Auto): 투명 배경 영상 → 안 되면 검은 배경 영상 + 가장자리 페이드 → 안 되면 콘티 이미지를 같은 길이 동안
    //  - 기억 종료: VideoPlayer 재생 끝 이벤트. 콘티 이미지면 길이 타이머. 어느 쪽이든 길이 + 5초면 강제로 끝낸다
    //  - 손상 마스크: 손상된 단계마다 데이터의 영역·구간을 덮는다 (부위는 신주연 기억 연출 정의서 기준, 지금은 가안)
    //  - 대사 글자는 영상에 굽지 않는다 (D62). 영상 소리는 MemorySoundLayers가 따로 재생한다
    public class MemoryVideoPlayer : MonoBehaviour
    {
        public enum Mode { Auto, AlphaOnly, OpaqueOnly, StoryboardOnly }

        [Tooltip("알파 확인(MEM-06)을 위해 바꿔 가며 시험한다. 에디터에서는 V 키로 바꾼다")]
        public Mode mode = Mode.Auto;
        public Renderer screen;
        public VideoPlayer player;
        public float fadeTime = 1f;
        [Tooltip("검은 배경 영상일 때 가장자리 페이드 폭 (uv)")]
        public float opaqueEdgeFade = 0.12f;
        [Tooltip("영상을 준비하는 데 기다리는 최대 시간 (초)")]
        public float prepareTimeout = 4f;
        public float forceEndExtra = 5f;

        public string LastResult { get; private set; } = "아직 재생 안 함";

        Material mat;
        RenderTexture rt;
        bool prepared, failed, reachedEnd;
        string error;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int UseAlphaId = Shader.PropertyToID("_UseVideoAlpha");
        static readonly int EdgeId = Shader.PropertyToID("_EdgeFade");
        static readonly int[] MaskRectIds = { Shader.PropertyToID("_MaskRect0"), Shader.PropertyToID("_MaskRect1"), Shader.PropertyToID("_MaskRect2") };
        static readonly int[] MaskAmtIds = { Shader.PropertyToID("_MaskAmt0"), Shader.PropertyToID("_MaskAmt1"), Shader.PropertyToID("_MaskAmt2") };

        void Awake()
        {
            if (screen != null)
            {
                mat = screen.material;
                mat.SetFloat(AlphaId, 0f);
                screen.enabled = false;
            }
            if (player != null)
            {
                player.playOnAwake = false;
                player.isLooping = false;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                player.prepareCompleted += _ => prepared = true;
                player.errorReceived += (_, msg) => { failed = true; error = msg; };
                player.loopPointReached += _ => reachedEnd = true;
            }
        }

        void OnDestroy() { if (rt != null) { rt.Release(); Destroy(rt); } }

        public void CycleMode()
        {
            mode = (Mode)(((int)mode + 1) % 4);
            Debug.Log($"[기억 영상] 재생 방식 → {mode}");
        }

        // 영상을 재생하고 끝날 때까지 기다린다. nearEnd는 끝나기 restoreLead초 전에 한 번 부른다(조명 복귀용).
        public IEnumerator Play(AntiqueData data, float restoreLead, Action nearEnd)
        {
            float length = Mathf.Max(1f, data != null ? data.memoryLength : 20f);
            var masks = BuildMasks(data);
            AlphaLog.BeginSection("기억 영상");

            // 1) 재생할 것을 고른다
            string used = null;
            if (player != null && data != null)
            {
                if ((mode == Mode.Auto || mode == Mode.AlphaOnly) && data.memoryVideoAlpha != null)
                {
                    yield return Prepare(data.memoryVideoAlpha);
                    if (prepared) used = "투명 배경 영상(알파)";
                    else Debug.LogWarning($"[기억 영상] MEM-06 투명 배경 영상을 준비하지 못함 ({error ?? "시간 초과"}) → 검은 배경 영상으로");
                }
                if (used == null && (mode == Mode.Auto || mode == Mode.OpaqueOnly) && data.memoryVideo != null)
                {
                    yield return Prepare(data.memoryVideo);
                    if (prepared) used = "검은 배경 영상 + 가장자리 페이드";
                    else Debug.LogWarning($"[기억 영상] 검은 배경 영상을 준비하지 못함 ({error ?? "시간 초과"}) → 콘티 이미지로");
                }
            }

            bool video = used != null;
            if (!video) used = data != null && data.storyboard != null && data.storyboard.Length > 0 ? "콘티 이미지" : "빈 화면 (영상·콘티 없음)";
            bool alpha = video && used.StartsWith("투명");
            if (video) length = Mathf.Max(1f, (float)player.length > 0.1f ? (float)player.length : length);

            if (mat != null)
            {
                mat.SetFloat(UseAlphaId, alpha ? 1f : 0f);
                mat.SetFloat(EdgeId, alpha ? 0f : opaqueEdgeFade);
                mat.SetTexture(MainTexId, video ? (Texture)rt : FirstStoryboard(data));
                for (int i = 0; i < 3; i++) { mat.SetVector(MaskRectIds[i], Vector4.zero); mat.SetFloat(MaskAmtIds[i], 0f); }
                for (int i = 0; i < masks.Count && i < 3; i++)
                {
                    var r = masks[i].uv;
                    mat.SetVector(MaskRectIds[i], new Vector4(r.xMin, r.yMin, r.xMax, r.yMax));
                }
            }
            Debug.Log($"[기억 영상] {(data != null ? data.itemId : "?")}: {used}로 재생, 길이 {length:0.0}초, 손상 마스크 {masks.Count}칸" +
                      (video ? $", {player.width}×{player.height} {player.frameRate:0.#}fps" : ""));

            // 2) 재생
            if (screen != null) screen.enabled = true;
            reachedEnd = false;
            if (video) player.Play();
            StartCoroutine(FadeScreen(1f, fadeTime));

            float t = 0f;
            bool near = false;
            int board = -1;
            while (true)
            {
                t += Time.deltaTime;
                float clock = video ? (float)player.time : t;

                if (!video && data != null && data.storyboard != null && data.storyboard.Length > 0 && mat != null)
                {
                    int idx = Mathf.Clamp((int)(t / length * data.storyboard.Length), 0, data.storyboard.Length - 1);
                    if (idx != board) { board = idx; mat.SetTexture(MainTexId, data.storyboard[idx]); }
                }

                for (int i = 0; i < masks.Count && i < 3; i++)
                {
                    var m = masks[i];
                    float on = Mathf.Clamp01(Mathf.Min(clock - m.start, m.end - clock) / 0.3f);
                    if (mat != null) mat.SetFloat(MaskAmtIds[i], on * m.strength);
                }

                if (!near && clock >= length - restoreLead) { near = true; nearEnd?.Invoke(); }

                bool done = video ? reachedEnd : t >= length;
                if (done) break;
                if (t >= length + forceEndExtra)
                {
                    Debug.LogWarning($"[기억 영상] 길이 + {forceEndExtra}초가 지나도 끝 이벤트가 없어 강제로 끝냄 (21-4)");
                    break;
                }
                yield return null;
            }

            if (!near) nearEnd?.Invoke();
            yield return FadeScreen(0f, fadeTime);
            if (video) player.Stop();
            if (screen != null) screen.enabled = false;
            AlphaLog.EndSection();
            LastResult = used;
        }

        IEnumerator Prepare(VideoClip clip)
        {
            prepared = false;
            failed = false;
            error = null;
            player.Stop();
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            int w = (int)Mathf.Max(16, clip.width), h = (int)Mathf.Max(16, clip.height);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "기억 영상 RT" };
                rt.Create();
            }
            player.targetTexture = rt;
            player.Prepare();
            for (float t = 0f; t < prepareTimeout && !prepared && !failed; t += Time.unscaledDeltaTime) yield return null;
            if (prepared) Debug.Log($"[기억 영상] 준비 완료: {clip.name} ({clip.width}×{clip.height}, {clip.frameRate:0.#}fps, {clip.length:0.0}초)");
        }

        IEnumerator FadeScreen(float to, float time)
        {
            if (mat == null) yield break;
            float from = mat.GetFloat(AlphaId);
            for (float t = 0f; t < time; t += Time.deltaTime) { mat.SetFloat(AlphaId, Mathf.Lerp(from, to, t / time)); yield return null; }
            mat.SetFloat(AlphaId, to);
        }

        static Texture FirstStoryboard(AntiqueData data)
        {
            return data != null && data.storyboard != null && data.storyboard.Length > 0 ? data.storyboard[0] : Texture2D.blackTexture;
        }

        static List<DamageMask> BuildMasks(AntiqueData data)
        {
            var list = new List<DamageMask>();
            if (data == null) return list;
            foreach (int s in RepairRecord.DamagedSteps(data.itemId))
            {
                var step = data.Step(s);
                if (step == null || step.mask == null || !step.mask.enabled) continue;
                list.Add(step.mask);
                Debug.Log($"[기억 영상] MEM-07 손상 {s}단계 마스크: {step.mask.note} ({step.mask.start:0}~{step.mask.end:0}초)");
            }
            if (list.Count > 3) Debug.LogWarning("[기억 영상] 손상 마스크는 3칸까지만 덮는다");
            return list;
        }
    }
}
