using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Dokkaebi
{
    // 대사 표시 (UI-01). 기획서 18장.
    //  - 한 번에 한 줄, 한 구절 20자 이내, 표시 시간 1초 + 글자당 0.1초 (공백 제외, 문장부호 포함 Q-096)
    //  - 도깨비 글자는 그 도깨비나 물건 옆, 주인공 글자는 작업대 위 시선 높이 (대사 시작 때 정하고 그 대사 동안 고정, Q-101)
    //  - 새 대사는 대기열 (Q-095). 대사가 떠 있어도 조작할 수 있다 (21-4)
    //  - 서체·색은 UI 시안(11/13, 강민우) 방향으로 맞춤. 트리거로 빨리 넘기기(INP-09)·구절 나누기 정리(UI-02)는 4주차
    public class DialogueDisplay : MonoBehaviour
    {
        public DialogueTable table;
        public TMP_FontAsset font;
        [Tooltip("다른 물체에 가려지지 않게 그리는 글자 재질 (TMP Distance Field Overlay). 비우면 도깨비 머리·손에 글자가 잘릴 수 있다")]
        public Material overlayMaterial;
        [Tooltip("글자 크기 (m, 대략 글자 높이). 1m 거리 가독성은 Quest 3 실기에서 정한다 (T20)")]
        public float letterHeight = 0.03f;
        public int maxChars = 20;
        public float baseTime = 1f;
        public float perCharTime = 0.1f;
        public Color dokkaebiColor = new Color(1f, 0.86f, 0.55f);
        public Color protagonistColor = new Color(0.9f, 0.95f, 1f);
        public Color systemColor = Color.white;
        [Tooltip("도깨비 글자를 화자 위쪽에 띄우는 높이 (m)")]
        public float speakerOffset = 0.18f;
        [Tooltip("주인공 글자: 눈높이보다 얼마나 아래 (m)")]
        public float protagonistDrop = 0.12f;
        public float protagonistDistance = 0.75f;
        [Tooltip("주인공 글자: 고개를 숙이거나 든 만큼 따라가는 한계 (도, 아래 / 위)")]
        public float protagonistPitchDown = 40f;
        public float protagonistPitchUp = 15f;

        [Header("대사 음성 (실험)")]
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Tooltip("음성이 끝난 뒤 글자를 더 보여주는 시간 (초)")]
        public float voiceTailPad = 0.25f;

        struct Request { public DialogueLine line; public Transform anchor; public int ticket; }

        readonly Queue<Request> queue = new Queue<Request>();
        TextMeshPro label;
        AudioSource voiceSource;
        Transform follow;
        Vector3 fixedPos;
        bool useFixed;
        int issued, finished;
        bool running;

        public bool IsBusy => running || queue.Count > 0;

        void Awake()
        {
            var go = new GameObject("대사 글자");
            go.transform.SetParent(transform, false);
            label = go.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            if (overlayMaterial != null) label.fontSharedMaterial = overlayMaterial;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 200);
            // TextMeshPro 3D의 fontSize 10 ≒ 1m 줄 높이. 글자 높이 기준으로 맞춘다
            label.fontSize = letterHeight * 10f / 0.72f;
            label.rectTransform.sizeDelta = new Vector2(1.2f, 0.2f);
            label.gameObject.SetActive(false);

            var vgo = new GameObject("대사 음성");
            vgo.transform.SetParent(transform, false);
            voiceSource = vgo.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.minDistance = 0.5f;
            voiceSource.maxDistance = 6f;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
        }

        // 대사를 대기열에 넣는다. 돌려받은 번호로 끝났는지 확인할 수 있다.
        public int Say(string id, Transform anchor = null)
        {
            var line = table != null ? table.Find(id) : null;
            if (line == null) { Debug.LogWarning($"[대사] {id}: 대사 표에 없음"); return -1; }
            int ticket = ++issued;
            queue.Enqueue(new Request { line = line, anchor = anchor, ticket = ticket });
            if (!running) StartCoroutine(Run());
            return ticket;
        }

        public IEnumerator SayAndWait(string id, Transform anchor = null)
        {
            int ticket = Say(id, anchor);
            if (ticket < 0) yield break;
            while (finished < ticket) yield return null;
        }

        // 다음 단계로 바뀌면 이전 단계 힌트 대사를 지운다 (21-4). 힌트(H) 대사는 4주차에 연결
        public void DropQueued(System.Predicate<DialogueLine> match)
        {
            var keep = new List<Request>();
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                if (match(r.line)) finished = Mathf.Max(finished, r.ticket); else keep.Add(r);
            }
            foreach (var r in keep) queue.Enqueue(r);
        }

        IEnumerator Run()
        {
            running = true;
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                var phrases = Split(r.line.text, maxChars);
                Debug.Log($"[대사] {r.line.id} ({r.line.speaker}): {string.Join(" / ", phrases)}");
                PreparePosition(r);
                label.color = ColorFor(r.line.speaker);
                label.gameObject.SetActive(true);
                var clip = r.line.voice;
                PlayVoice(clip, r.anchor, r.line.speaker);
                var times = PhraseTimes(phrases, clip);
                for (int i = 0; i < phrases.Count; i++)
                {
                    label.text = phrases[i];
                    yield return new WaitForSeconds(times[i]);
                }
                if (voiceSource != null) voiceSource.Stop();
                label.gameObject.SetActive(false);
                finished = Mathf.Max(finished, r.ticket);
            }
            running = false;
        }

        // 음성이 있으면 음성 길이를 글자 수 비율로 나눠 구절마다 보여주고, 없으면 글자 수로 계산한다
        List<float> PhraseTimes(List<string> phrases, AudioClip clip)
        {
            var times = new List<float>();
            if (clip == null)
            {
                foreach (var p in phrases) times.Add(DurationOf(p));
                return times;
            }
            var weights = new List<int>();
            int sum = 0;
            foreach (var p in phrases)
            {
                int n = 0;
                foreach (char c in p) if (!char.IsWhiteSpace(c)) n++;
                n = Mathf.Max(1, n);
                weights.Add(n);
                sum += n;
            }
            foreach (var w in weights) times.Add(clip.length * w / sum);
            if (times.Count > 0) times[times.Count - 1] += voiceTailPad;
            return times;
        }

        void PlayVoice(AudioClip clip, Transform anchor, Speaker speaker)
        {
            if (voiceSource == null) return;
            voiceSource.Stop();
            if (clip == null) return;
            bool spatial = anchor != null && speaker != Speaker.Protagonist;
            voiceSource.spatialBlend = spatial ? 1f : 0f;
            if (spatial) voiceSource.transform.position = anchor.position;
            voiceSource.clip = clip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
        }

        void PreparePosition(Request r)
        {
            var cam = Camera.main;
            follow = null;
            useFixed = false;
            if (r.line.speaker == Speaker.Protagonist || r.anchor == null)
            {
                if (cam != null)
                {
                    // 작업대를 내려다보고 있어도 시야 안에 들어오게, 보는 방향의 위아래 기울기를 따라간다
                    Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
                    Vector3 fwd = flat.sqrMagnitude < 0.01f ? Vector3.forward : flat.normalized;
                    float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg, -protagonistPitchDown, protagonistPitchUp);
                    Vector3 dir = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, fwd)) * fwd;
                    fixedPos = cam.transform.position + dir * protagonistDistance + Vector3.down * protagonistDrop;
                    useFixed = true;
                }
            }
            else follow = r.anchor;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (voiceSource != null && voiceSource.isPlaying && voiceSource.spatialBlend > 0f && follow != null)
                voiceSource.transform.position = follow.position;
            if (label == null || !label.gameObject.activeSelf) return;
            Vector3 target = useFixed ? fixedPos : follow != null ? follow.position + Vector3.up * speakerOffset : label.transform.position;
            // 화자가 움직이면 부드럽게 따라간다 (Q-100)
            label.transform.position = useFixed ? target : Vector3.Lerp(label.transform.position, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = label.transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 1e-4f) label.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }

        Color ColorFor(Speaker s)
        {
            switch (s)
            {
                case Speaker.Dokkaebi: return dokkaebiColor;
                case Speaker.Protagonist: return protagonistColor;
                default: return systemColor;
            }
        }

        public float DurationOf(string phrase)
        {
            int n = 0;
            foreach (char c in phrase) if (!char.IsWhiteSpace(c)) n++;
            return baseTime + perCharTime * n;
        }

        // '/'가 있으면 그 자리에서, 없으면 maxChars 안에서 어절 단위로 나눈다 (Q-097)
        public static List<string> Split(string text, int maxChars)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;
            if (text.Contains("/"))
            {
                foreach (var part in text.Split('/'))
                {
                    var t = part.Trim();
                    if (t.Length > 0) result.Add(t);
                }
                return result;
            }

            // 문장 단위로 먼저 끊고, 긴 문장은 구절 길이가 비슷해지게 나눈다 (끝에 한두 어절만 남지 않게)
            var sentence = new List<string>();
            foreach (var w in text.Trim().Split(' '))
            {
                if (w.Length == 0) continue;
                sentence.Add(w);
                if (w.EndsWith(".") || w.EndsWith("?") || w.EndsWith("!") || w.EndsWith("…"))
                { SplitEven(sentence, maxChars, result); sentence.Clear(); }
            }
            if (sentence.Count > 0) SplitEven(sentence, maxChars, result);
            return result;
        }

        static void SplitEven(List<string> words, int maxChars, List<string> result)
        {
            int total = words.Count - 1;
            foreach (var w in words) total += w.Length;
            int parts = Mathf.Max(1, Mathf.CeilToInt(total / (float)Mathf.Max(1, maxChars)));
            while (true)
            {
                var lines = new List<string>();
                float target = total / (float)parts;
                string cur = "";
                foreach (var w in words)
                {
                    string next = cur.Length == 0 ? w : cur + " " + w;
                    // 목표 길이에 더 가까운 쪽에서 끊는다. 남은 구절 수를 넘기지 않는다
                    bool breakHere = cur.Length > 0 && lines.Count < parts - 1
                                     && (next.Length > maxChars || Mathf.Abs(cur.Length - target) < Mathf.Abs(next.Length - target));
                    if (breakHere) { lines.Add(cur); cur = w; }
                    else cur = next;
                }
                if (cur.Length > 0) lines.Add(cur);
                bool fits = true;
                foreach (var l in lines) if (l.Length > maxChars && l.Contains(" ")) fits = false;
                if (fits || parts >= words.Count) { result.AddRange(lines); return; }
                parts++;
            }
        }
    }
}
