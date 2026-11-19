using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Dokkaebi
{
    // 밤 전환 (SYS-06) + 공간 변화 (PRC-05). 기획서 10장 '밤과 밤 사이', 15장, 21-6, 레벨 명세 6장.
    //  공간 변화 3초 → 암전 1초 → "둘째 밤" 글자 + 펜 소리 2.5초 → 점등 1초 → 등장 1.5초 (약 9초)
    //  - 글자는 공중에 떠 있는 오브젝트가 아니라 암전 화면에 잠깐 나타났다 사라지는 글자. 암전 직전 보던 방향 2m 앞에 고정
    //  - 펜으로 긋는 사각사각 소리는 작업대 왼쪽 뒤(달력)에서. 불이 켜지면 달력의 지난 날짜에 X
    //  - 반환한 곰 인형은 암전 때 사라지고(주인에게 돌아감, D172), 다음 밤부터 출입문 옆 사진·꽃과 밝아진 조명이 남는다
    //  - 밤이 바뀌어도 도구 위치·떨어진 부품·솜·손상 흔적은 그대로 둔다 (초기화하지 않음, 10장)
    public class NightTransition : MonoBehaviour
    {
        public ScreenFader fader;
        public LightingDirector lighting;
        public TextMeshPro label;
        [Tooltip("펜 소리가 나는 곳 (탁상 달력)")]
        public Transform calendar;
        [Tooltip("달력의 지난 날짜에 그어지는 X (밤마다 하나씩)")]
        public GameObject[] calendarMarks;
        [Tooltip("반환 뒤 다음 밤부터 남는 것 (출입문 옆 사진·꽃)")]
        public GameObject returnRemnants;

        [Header("시간 (초, 레벨 명세 6장)")]
        public float spaceChange = 3f;
        public float blackout = 1f;
        public float labelTime = 2.5f;
        public float lightsOn = 1f;
        public float arrive = 1.5f;
        public float labelDistance = 2f;

        public float TotalLength => spaceChange + blackout + labelTime + lightsOn + arrive;

        void Awake()
        {
            if (label != null) label.gameObject.SetActive(false);
            if (returnRemnants != null) returnRemnants.SetActive(false);
            if (calendarMarks != null) foreach (var m in calendarMarks) if (m != null) m.SetActive(false);
        }

        // nightIndex: 막 끝난 밤(1부터). duringBlack: 암전 동안 할 일(처리한 물건 정리 등)
        public IEnumerator Run(ProcessKind processed, int nightIndex, string nextLabel, Action duringBlack)
        {
            AlphaLog.BeginSection("밤 전환");
            Debug.Log($"[밤 전환] 시작 (약 {TotalLength:0.#}초): 공간 변화 → 암전 → '{nextLabel}' → 점등 → 등장");

            // 1) 공간 변화
            if (processed == ProcessKind.Return && lighting != null)
                StartCoroutine(lighting.Brighten(lighting.spaceLevel * lighting.returnBrightening, spaceChange));
            yield return new WaitForSeconds(spaceChange);

            // 2) 암전. 다 어두워진 순간 보던 방향을 기억해 둔다 (고개를 숙이고 있어도 글자가 시야 안에 오게 위아래 기울기까지)
            if (fader != null) yield return fader.FadeTo(1f, blackout);
            else yield return new WaitForSeconds(blackout);
            var cam = Camera.main;
            Vector3 dir = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 eye = cam != null ? cam.transform.position : new Vector3(0f, 1.5f, 0f);

            duringBlack?.Invoke();
            if (processed == ProcessKind.Return && returnRemnants != null) returnRemnants.SetActive(true);
            if (calendarMarks != null && nightIndex - 1 < calendarMarks.Length && nightIndex >= 1 && calendarMarks[nightIndex - 1] != null)
                calendarMarks[nightIndex - 1].SetActive(true);

            // 3) 글자 + 펜 소리
            if (label != null)
            {
                label.text = nextLabel;
                label.transform.position = eye + dir * labelDistance;
                label.transform.rotation = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.98f && cam != null ? cam.transform.up : Vector3.up);
                label.alpha = 0f;
                label.gameObject.SetActive(true);
            }
            Sfx.Play(SfxKey.Pen, calendar != null ? calendar.position : eye);
            float fade = Mathf.Min(0.5f, labelTime / 4f);
            for (float t = 0f; t < labelTime; t += Time.deltaTime)
            {
                if (label != null) label.alpha = Mathf.Min(Mathf.Clamp01(t / fade), Mathf.Clamp01((labelTime - t) / fade));
                yield return null;
            }
            if (label != null) label.gameObject.SetActive(false);
            Debug.Log($"[밤 전환] '{nextLabel}' 글자 (보던 방향 {labelDistance}m 앞), 펜 소리, 달력 X");

            // 4) 점등
            if (fader != null) yield return fader.FadeTo(0f, lightsOn);
            else yield return new WaitForSeconds(lightsOn);

            // 5) 등장: 출입문 종 (다음 밤 S1의 시작)
            yield return new WaitForSeconds(arrive);
            AlphaLog.EndSection();
            Debug.Log("[밤 전환] 끝");
        }
    }
}
