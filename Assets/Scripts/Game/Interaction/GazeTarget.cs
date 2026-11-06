using System;
using UnityEngine;

namespace Dokkaebi
{
    // 시선 판정 (INP-07). 머리 정면과 물체 방향 사이가 15° 이내인 시간이 2초 쌓이면 확인.
    //  - 0.3초 이내로 잠깐 벗어난 것은 무시하고, 그보다 길면 처음부터 (Q-060, Q-173)
    public class GazeTarget : MonoBehaviour
    {
        public float angle = 15f;
        public float dwell = 2f;
        public float grace = 0.3f;
        public float maxDistance = 2.5f;
        public bool active;

        public float Progress => Mathf.Clamp01(looked / dwell);
        public bool Confirmed { get; private set; }
        public event Action OnConfirmed;

        float looked, away;

        public void Activate() { active = true; Confirmed = false; looked = away = 0f; }

        void Update()
        {
            if (!active || Confirmed) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 to = transform.position - cam.transform.position;
            bool looking = to.magnitude <= maxDistance && Vector3.Angle(cam.transform.forward, to) <= angle;
            if (looking) { looked += Time.deltaTime; away = 0f; }
            else
            {
                away += Time.deltaTime;
                if (away > grace) looked = 0f;
            }

            if (looked >= dwell)
            {
                Confirmed = true;
                active = false;
                Debug.Log($"[시선] {name}: {dwell}초 확인");
                OnConfirmed?.Invoke();
            }
        }

        // 에디터 확인용
        public void ForceConfirm()
        {
            if (Confirmed) return;
            looked = dwell;
            active = true;
        }
    }
}
