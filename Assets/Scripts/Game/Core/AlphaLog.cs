using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dokkaebi
{
    // 알파(11/13) 증빙용 기록.
    //  1) [..]로 시작하는 게임 로그를 기기 안 파일로 남긴다 (Quest에서는 adb로 꺼낸다)
    //     파일: Application.persistentDataPath/alpha_log_yyyyMMdd_HHmmss.txt
    //     Quest: /sdcard/Android/data/<패키지 이름>/files/
    //  2) 프레임 측정 (MEM-06 '72fps 기록', OPS-03의 앞 단계):
    //     구간(기억 영상, 도깨비 변신, 밤 전환 등)마다 평균 fps, 13.9ms를 넘은 프레임 수,
    //     13.9ms를 넘는 프레임이 3개 이상 이어진 횟수를 센다 (기획서 17장 성능 기준).
    //     정식 측정은 OVR Metrics Tool로 한다. 이 값은 앱 안에서 본 참고값이다.
    public class AlphaLog : MonoBehaviour
    {
        public static AlphaLog Instance { get; private set; }

        [Tooltip("파일로 남길지 (에디터에서도 남긴다)")]
        public bool writeFile = true;
        public float frameBudgetMs = 13.9f;

        StreamWriter writer;
        string section;
        int frames, slow, runs, currentRun;
        float elapsed, worstMs;

        public string FilePath { get; private set; }

        void Awake()
        {
            Instance = this;
            if (writeFile)
            {
                try
                {
                    FilePath = Path.Combine(Application.persistentDataPath, $"alpha_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
                    writer.WriteLine($"# 도깨비 골동품 수선소 알파 기록 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# 기기 {SystemInfo.deviceModel} / {SystemInfo.operatingSystem} / 화면 주사율 {Screen.currentResolution.refreshRateRatio.value:0.#}Hz");
                }
                catch (Exception e) { Debug.LogWarning($"[기록] 파일을 만들지 못함: {e.Message}"); writer = null; }
            }
            Application.logMessageReceived += OnLog;
            Debug.Log($"[기록] 알파 기록 시작 {(writer != null ? FilePath : "(파일 없음)")}");
        }

        void OnDestroy()
        {
            EndSection();
            Application.logMessageReceived -= OnLog;
            writer?.Dispose();
            writer = null;
            if (Instance == this) Instance = null;
        }

        void OnApplicationPause(bool paused) { if (paused) writer?.Flush(); }

        void OnLog(string message, string stack, LogType type)
        {
            if (writer == null) return;
            bool ours = message.StartsWith("[");
            if (!ours && type != LogType.Error && type != LogType.Exception) return;
            writer.WriteLine($"{Time.realtimeSinceStartup,8:0.00} {(type == LogType.Log ? "" : type + " ")}{message}");
        }

        // ---- 프레임 측정 ----

        public static void BeginSection(string name) { if (Instance != null) Instance.Begin(name); }
        public static void EndSection(string name = null) { if (Instance != null) Instance.EndSection(); }

        void Begin(string name)
        {
            EndSection();
            section = name;
            frames = slow = runs = currentRun = 0;
            elapsed = worstMs = 0f;
        }

        void EndSection()
        {
            if (string.IsNullOrEmpty(section)) return;
            float fps = elapsed > 0f ? frames / elapsed : 0f;
            Debug.Log($"[프레임] {section}: {elapsed:0.0}초, 평균 {fps:0.0}fps, {frameBudgetMs}ms 초과 {slow}프레임, 3프레임 이상 연속 초과 {runs}회, 가장 긴 프레임 {worstMs:0.0}ms");
            section = null;
        }

        void Update()
        {
            if (string.IsNullOrEmpty(section)) return;
            float ms = Time.unscaledDeltaTime * 1000f;
            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (ms > worstMs) worstMs = ms;
            if (ms > frameBudgetMs)
            {
                slow++;
                currentRun++;
                if (currentRun == 3) runs++;
            }
            else currentRun = 0;
        }
    }
}
