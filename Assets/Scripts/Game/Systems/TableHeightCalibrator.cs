using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Dokkaebi
{
    // 앱 시작·작업대 높이 측정 (SYS-01). 기획서 10장, 레벨 명세 1.1, 기능명세서 SYS-01.
    //  - 검은 화면 2초 동안 머리(헤드셋) 높이를 재서 H = 머리 높이 − 0.45m, 0.60~1.00m로 자른다
    //  - 작업대 위의 모든 것(heightRoot 아래)을 (H − 0.90)만큼 올리거나 내리고, 바닥에서 올라오는 다리·받침은 늘이거나 줄인다(FloorToHeight)
    //  - 헤드셋이 없으면(에디터 확인) 0.90m 그대로 둔다
    //  - MVP는 앱을 켤 때 한 번. 관람객마다 다시 재는 것은 타이틀 화면(SYS-02, 12월)에서
    public class TableHeightCalibrator : MonoBehaviour
    {
        [Tooltip("씬을 만들 때 쓴 기준 높이 (회색 박스 0.90m)")]
        public float builtHeight = 0.90f;
        public float headOffset = 0.45f;
        public float minHeight = 0.60f;
        public float maxHeight = 1.00f;
        public float measureTime = 2f;
        [Tooltip("작업대 높이를 따라 함께 움직일 것들의 부모")]
        public Transform heightRoot;

        public float Height { get; private set; }

        void Awake() { Height = builtHeight; }

        public IEnumerator Measure()
        {
            var cam = Camera.main;
            bool device = XRSettings.isDeviceActive;
            var samples = new List<float>();
            for (float t = 0f; t < measureTime; t += Time.deltaTime)
            {
                // 처음 0.5초는 트래킹이 자리 잡는 시간으로 보고 버린다
                if (t >= 0.5f && cam != null) samples.Add(cam.transform.position.y);
                yield return null;
            }

            float h = builtHeight;
            if (device && samples.Count > 0)
            {
                samples.Sort();
                float head = samples[samples.Count / 2]; // 중앙값 (고개를 잠깐 숙여도 덜 흔들림)
                h = Mathf.Clamp(head - headOffset, minHeight, maxHeight);
                Debug.Log($"[높이] 머리 높이 {head:0.000}m → H = {h:0.000}m (범위 {minHeight}~{maxHeight})");
            }
            else Debug.Log($"[높이] 헤드셋이 없어 H = {builtHeight:0.00}m 그대로 (에디터 확인)");

            Apply(h);
        }

        public void Apply(float h)
        {
            float delta = h - Height;
            Height = h;
            if (Mathf.Abs(delta) > 0.0005f && heightRoot != null)
            {
                // 물리가 섞이지 않게 잠깐 멈추고 옮긴다
                var moved = new List<Grabbable>();
                Grabbable.ForEach(g => { if (g.transform.IsChildOf(heightRoot)) { g.SetFrozen(true); moved.Add(g); } });
                heightRoot.position += Vector3.up * delta;
                Physics.SyncTransforms();
                foreach (var g in moved) { if (!g.Locked) g.SetHome(); }
                // 아직 꺼져 있는 물건(곰 인형 세트 등)은 켜질 때 Awake에서 제자리를 잡는다
            }
            foreach (var f in FindObjectsByType<FloorToHeight>(FindObjectsInactive.Include)) f.Fit(h);
            Grabbable.LostBelowY = h - 0.3f;
            GameEvents.RaiseTableHeightSet(h);
        }
    }
}
