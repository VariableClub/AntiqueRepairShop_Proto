#!/usr/bin/env python3
"""대사 음성 생성 (실험): 대사 표(.asset)의 줄을 Google Cloud Text-to-Speech로 읽어 파일로 저장한다.

  - 게임에서 API를 부르지 않는다. 미리 파일을 만들어 Assets/Audio/Voice/<대사ID>.mp3 로 둔다.
  - 인증은 둘 중 하나: 환경변수 GOOGLE_TTS_API_KEY (API 키) / 없으면 `gcloud auth print-access-token`.
    API 키를 저장소에 넣지 않는다 (환경변수로만 쓴다).
  - 표준 라이브러리만 쓴다 (설치할 것 없음).

사용 예 (프로젝트 루트에서):
  export GOOGLE_TTS_API_KEY=...            # 키 방식일 때
  python3 tools/tts/generate_dialogue.py --list-voices          # 쓸 수 있는 한국어 음성 이름 확인
  python3 tools/tts/generate_dialogue.py --dry-run              # 만들 줄만 보기 (호출 안 함)
  python3 tools/tts/generate_dialogue.py                        # 도깨비 대사 전부 (이미 있는 파일은 건너뜀)
  python3 tools/tts/generate_dialogue.py --ids D01 D02 --force  # 지정한 줄만 다시 만들기
  python3 tools/tts/generate_dialogue.py --voice ko-KR-Chirp3-HD-Kore --speed 0.95
"""
import argparse, base64, json, os, re, subprocess, sys, urllib.error, urllib.request

ASSET = "Assets/Data/Dialogue/Dialogue_EP01_Bear.asset"
OUT_DIR = "Assets/Audio/Voice"
SPEAKERS = {0: "Dokkaebi", 1: "Protagonist", 2: "MemoryPerson", 3: "System"}
API = "https://texttospeech.googleapis.com/v1"


def parse_lines(path):
    """Unity YAML(DialogueTable)에서 id / speaker / text 를 읽는다."""
    lines, cur = [], None
    with open(path, encoding="utf-8") as f:
        for raw in f:
            raw = raw.rstrip("\n")
            m = re.match(r"\s*- id: (.+)", raw)
            if m:
                cur = {"id": m.group(1).strip(), "speaker": 0, "text": ""}
                lines.append(cur)
                continue
            if cur is None:
                continue
            m = re.match(r"\s+speaker: (\d+)", raw)
            if m:
                cur["speaker"] = int(m.group(1))
                continue
            m = re.match(r"\s+text: (.*)", raw)
            if m:
                v = m.group(1).strip()
                if v.startswith('"') and v.endswith('"'):
                    v = json.loads(v)  # \uXXXX 이스케이프 처리
                cur["text"] = v
    return lines


def clean_text(t):
    # 대사 표의 구절 나눔 표시(/)는 읽지 않는다
    return re.sub(r"\s*/\s*", " ", t).strip()


def headers():
    key = os.environ.get("GOOGLE_TTS_API_KEY")
    if key:
        return {"X-Goog-Api-Key": key, "Content-Type": "application/json"}
    try:
        tok = subprocess.check_output(["gcloud", "auth", "print-access-token"], text=True).strip()
    except Exception as e:
        sys.exit(f"인증 없음: GOOGLE_TTS_API_KEY 를 설정하거나 gcloud 로그인을 하세요 ({e})")
    h = {"Authorization": f"Bearer {tok}", "Content-Type": "application/json"}
    proj = os.environ.get("GOOGLE_CLOUD_PROJECT")
    if proj:
        h["x-goog-user-project"] = proj
    return h


def call(method, url, body=None):
    req = urllib.request.Request(url, data=json.dumps(body).encode() if body is not None else None,
                                 headers=headers(), method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        sys.exit(f"API 오류 {e.code}: {e.read().decode('utf-8', 'replace')}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--asset", default=ASSET)
    ap.add_argument("--out", default=OUT_DIR)
    ap.add_argument("--voice", default="ko-KR-Chirp3-HD-Kore", help="음성 이름 (--list-voices 로 확인)")
    ap.add_argument("--lang", default="ko-KR")
    ap.add_argument("--speed", type=float, default=1.0, help="말 빠르기 (0.25~2.0). 음성 종류에 따라 지원 안 될 수 있음")
    ap.add_argument("--ids", nargs="*", help="이 ID만 만든다")
    ap.add_argument("--speaker", default="Dokkaebi", choices=list(SPEAKERS.values()), help="이 화자 줄만 (기본: 도깨비)")
    ap.add_argument("--force", action="store_true", help="이미 있는 파일도 다시 만든다")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--list-voices", action="store_true")
    a = ap.parse_args()

    if a.list_voices:
        res = call("GET", f"{API}/voices?languageCode={a.lang}")
        for v in sorted(res.get("voices", []), key=lambda v: v["name"]):
            print(v["name"], v.get("ssmlGender", ""))
        return

    lines = [l for l in parse_lines(a.asset) if SPEAKERS.get(l["speaker"]) == a.speaker]
    if a.ids:
        lines = [l for l in lines if l["id"] in a.ids]
    if not lines:
        sys.exit("만들 줄이 없음")
    os.makedirs(a.out, exist_ok=True)

    total = 0
    for l in lines:
        text = clean_text(l["text"])
        dest = os.path.join(a.out, f"{l['id']}.mp3")
        skip = os.path.exists(dest) and not a.force
        print(f"{l['id']:4} {len(text):3}자  {'건너뜀(있음)' if skip else '생성'}  {text}")
        if skip or a.dry_run:
            continue
        body = {"input": {"text": text},
                "voice": {"languageCode": a.lang, "name": a.voice},
                "audioConfig": {"audioEncoding": "MP3", "speakingRate": a.speed}}
        res = call("POST", f"{API}/text:synthesize", body)
        with open(dest, "wb") as f:
            f.write(base64.b64decode(res["audioContent"]))
        total += len(text)
    print(f"끝. 이번에 보낸 글자 수 {total}. 유니티에서 「도깨비 > 대사 음성 연결」을 실행하세요")


if __name__ == "__main__":
    main()
