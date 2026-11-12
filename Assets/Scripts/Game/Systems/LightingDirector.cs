using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dokkaebi
{
    // 조명 (EP0-01 스탠드 점등, MEM-04 감정 조명, PRC-05 공간 변화의 '밝아진 조명'). 기획서 10장·14장 감정 조명.
    //  - 프롤로그 스탠드 색이 기본색(기준색). 수선 단계 중에는 스탠드가 기본색을 유지한다
    //  - 기억 시작: 3초 동안 스탠드 외 조명·주변광을 끄고 스탠드를 감정 색으로
    //  - 기억 끝: 스탠드는 기본색, 주변은 그 밤까지 쌓인 공간 상태(spaceLevel)로 돌아온다
    //  - 점멸 대신 부드럽게 바꾼다
    public class LightingDirector : MonoBehaviour
    {
        [Header("스탠드")]
        public Light lamp;
        public Color lampBaseColor = new Color(1f, 0.82f, 0.6f);
        public float lampBaseIntensity = 2.5f;
        [Tooltip("스탠드 갓 (켜지면 빛나는 재질)")]
        public Renderer lampShade;

        [Header("스탠드 외 조명 (기억 장면 때 꺼짐)")]
        public List<Light> others = new List<Light>();
        public Color ambientBase = new Color(0.16f, 0.16f, 0.19f);
        [Tooltip("기억 장면 때 주변광")]
        public Color ambientMemory = new Color(0.015f, 0.015f, 0.02f);

        [Header("공간 상태 (PRC-05)")]
        [Tooltip("1 = 첫째 밤 기본 밝기. 반환 뒤에는 커진다")]
        public float spaceLevel = 1f;
        [Tooltip("곰 인형 반환 뒤 밝아지는 정도 (가안)")]
        public float returnBrightening = 1.35f;

        readonly List<float> othersBase = new List<float>();
        Coroutine running;
        float lampLevel = 1f;      // 0 = 꺼짐
        float othersLevel = 1f;    // 기억 장면 때 0
        Color lampColor;

        public bool InMemory { get; private set; }

        void Awake()
        {
            othersBase.Clear();
            foreach (var l in others) othersBase.Add(l != null ? l.intensity : 0f);
            lampColor = lampBaseColor;
            Apply();
        }

        void Apply()
        {
            if (lamp != null)
            {
                lamp.color = lampColor;
                lamp.intensity = lampBaseIntensity * lampLevel;
                lamp.enabled = lampLevel > 0.001f;
            }
            if (lampShade != null && lampShade.material.HasProperty("_EmissionColor"))
            {
                lampShade.material.EnableKeyword("_EMISSION");
                lampShade.material.SetColor("_EmissionColor", lampColor * (0.8f * lampLevel));
            }
            float k = othersLevel * spaceLevel;
            for (int i = 0; i < others.Count; i++)
                if (others[i] != null) others[i].intensity = othersBase[i] * k;
            RenderSettings.ambientLight = Color.Lerp(ambientMemory, ambientBase * spaceLevel, othersLevel);
        }

        // ---- 앱 시작 (EP0-01) ----
        public void SetDark()
        {
            Stop();
            lampLevel = 0f;
            othersLevel = 0f;
            lampColor = lampBaseColor;
            Apply();
        }

        // 스탠드가 짧게 깜박이며 켜지고, 주변이 천천히 드러난다
        public IEnumerator LampOn(float lampTime = 0.6f, float roomTime = 2f)
        {
            Stop();
            lampColor = lampBaseColor;
            if (lamp != null) Sfx.Play(SfxKey.LampOn, lamp.transform.position);
            float[] flicker = { 0.35f, 0.05f, 0.6f, 0.2f };
            foreach (var f in flicker) { lampLevel = f; Apply(); yield return new WaitForSeconds(0.06f); }
            for (float t = 0f; t < lampTime; t += Time.deltaTime) { lampLevel = Mathf.Lerp(0.2f, 1f, t / lampTime); Apply(); yield return null; }
            lampLevel = 1f;
            for (float t = 0f; t < roomTime; t += Time.deltaTime) { othersLevel = Mathf.SmoothStep(0f, 1f, t / roomTime); Apply(); yield return null; }
            othersLevel = 1f;
            Apply();
        }

        // ---- 감정 조명 (MEM-04) ----
        public void EnterMemory(Color emotion, float time = 3f)
        {
            InMemory = true;
            Stop();
            running = StartCoroutine(To(emotion, 1f, 0f, time));
            Debug.Log($"[조명] 기억 시작: 스탠드 외 소등, 스탠드 → 감정 색 #{ColorUtility.ToHtmlStringRGB(emotion)} ({time}초)");
        }

        public void ExitMemory(float time = 3f)
        {
            InMemory = false;
            Stop();
            running = StartCoroutine(To(lampBaseColor, 1f, 1f, time));
            Debug.Log($"[조명] 기억 끝: 스탠드 기본색, 주변은 공간 상태 {spaceLevel:0.00}로 ({time}초)");
        }

        // ---- 공간 변화 (PRC-05) ----
        public IEnumerator Brighten(float to, float time)
        {
            float from = spaceLevel;
            for (float t = 0f; t < time; t += Time.deltaTime) { spaceLevel = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)); Apply(); yield return null; }
            spaceLevel = to;
            Apply();
        }

        public IEnumerator FadeAll(float to, float time)
        {
            float fromLamp = lampLevel, fromOthers = othersLevel;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                lampLevel = Mathf.Lerp(fromLamp, to, k);
                othersLevel = Mathf.Lerp(fromOthers, to, k);
                Apply();
                yield return null;
            }
            lampLevel = othersLevel = to;
            Apply();
        }

        IEnumerator To(Color color, float lampTo, float othersTo, float time)
        {
            Color c0 = lampColor;
            float l0 = lampLevel, o0 = othersLevel;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                lampColor = Color.Lerp(c0, color, k);
                lampLevel = Mathf.Lerp(l0, lampTo, k);
                othersLevel = Mathf.Lerp(o0, othersTo, k);
                Apply();
                yield return null;
            }
            lampColor = color; lampLevel = lampTo; othersLevel = othersTo;
            Apply();
            running = null;
        }

        void Stop() { if (running != null) { StopCoroutine(running); running = null; } }
    }
}
