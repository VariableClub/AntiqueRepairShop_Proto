#!/usr/bin/env python3
"""대사 음성 생성 (실험): 대사 표(.asset)의 줄을 Google 음성 합성으로 읽어 파일로 저장한다.

  - 게임에서 API를 부르지 않는다. 미리 파일을 만들어 Assets/Audio/Voice/<대사ID>.mp3(.wav) 로 둔다.
  - API 키를 저장소에 넣지 않는다 (환경변수로만 쓴다). 표준 라이브러리만 쓴다 (설치할 것 없음).
  - 읽는 글·빠르기·음성은 tools/tts/voice_lines.json 에서 줄마다 고친다 (화면 글자는 그대로).

엔진 두 가지:
  chirp  (기본) Google Cloud Text-to-Speech 의 Chirp3-HD 음성. 환경변수 GOOGLE_TTS_API_KEY
         (없으면 `gcloud auth print-access-token`). 쉼([pause])과 빠르기로 말투를 다듬는다.
  gemini Gemini API(AI Studio 키)의 음성 모델. 환경변수 GEMINI_API_KEY.
         "지친 듯 천천히" 같은 연기 지시(voice_lines.json 의 style)를 따른다. 결과는 wav.

사용 예 (프로젝트 루트에서):
  python3 tools/tts/generate_dialogue.py --dry-run               # 보낼 글만 보기 (호출 안 함)
  python3 tools/tts/generate_dialogue.py --list-voices           # 쓸 수 있는 한국어 음성 이름
  python3 tools/tts/generate_dialogue.py --audition D01          # 한 줄을 여러 음성으로 만들어 비교 (tools/tts/audition/)
  python3 tools/tts/generate_dialogue.py --force                 # 도깨비 대사 전부 다시 만들기
  python3 tools/tts/generate_dialogue.py --ids D01 D02 --force   # 지정한 줄만 다시 만들기
  python3 tools/tts/generate_dialogue.py --engine gemini --force # 연기 지시를 따르는 모델로 만들기
"""
import argparse, base64, json, os, re, subprocess, sys, time, urllib.error, urllib.request, wave

ASSET = "Assets/Data/Dialogue/Dialogue_EP01_Bear.asset"
OUT_DIR = "Assets/Audio/Voice"
LINES_FILE = "tools/tts/voice_lines.json"
AUDITION_DIR = "tools/tts/audition"
SPEAKERS = {0: "Dokkaebi", 1: "Protagonist", 2: "MemoryPerson", 3: "System"}
CLOUD_API = "https://texttospeech.googleapis.com/v1"
GEMINI_API = "https://generativelanguage.googleapis.com/v1beta"
# --audition 에서 비교할 음성 (Chirp3-HD 이름의 끝부분. gemini 엔진에서도 같은 이름을 쓴다)
AUDITION_VOICES = ["Kore", "Charon", "Fenrir", "Orus", "Puck", "Aoede", "Leda", "Zephyr"]
PAUSE = re.compile(r"\[pause(?: short| long)?\]")


class ApiError(Exception):
    pass


class DailyLimit(ApiError):
    pass


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


def load_settings(path):
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def auto_markup(t):
    """화면용 대사를 읽기용으로 바꾼다: 구절 나눔(/)과 말줄임표를 쉼으로."""
    t = re.sub(r"\s*/\s*", " [pause short] ", t)
    t = re.sub(r"^[…\.\s]+", "", t)                 # 맨 앞 말줄임표는 읽지 않는다
    t = re.sub(r"(?:…|\.{3,})+\s*$", ".", t)         # 맨 끝은 마침표로 (말끝을 흐리지 않게)
    t = re.sub(r"\s*(?:…|\.{3,})+\s*", " [pause] ", t)  # 가운데는 쉼
    return re.sub(r"\s+", " ", t).strip()


def strip_pauses(t):
    """쉼 표시를 모르는 엔진·음성용: 쉼을 쉼표로 바꾼 보통 글."""
    t = PAUSE.sub(",", t)
    t = re.sub(r"\s*,(\s*,)+", ",", t)
    t = re.sub(r"([.!?…])\s*,", r"\1", t)
    t = re.sub(r"\s+,", ",", t)
    return re.sub(r"\s+", " ", t).strip(" ,")


def cloud_headers():
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


def gemini_headers():
    key = os.environ.get("GEMINI_API_KEY")
    if not key:
        sys.exit("인증 없음: GEMINI_API_KEY 를 설정하세요 (Google AI Studio 에서 무료로 발급)")
    return {"x-goog-api-key": key, "Content-Type": "application/json"}


def explain(code, msg):
    """오류를 한 줄로 풀어 준다: (설명, 하루 한도를 다 썼는지, 서버가 알려 준 대기 초)."""
    quota, delay = "", 0.0
    try:
        err = json.loads(msg)
        err = (err[0] if isinstance(err, list) else err).get("error", {})
        for d in err.get("details", []):
            for v in d.get("violations", []):
                quota = v.get("quotaId", "") or quota
            if "retryDelay" in d:
                delay = float(re.sub(r"[^0-9.]", "", d["retryDelay"]) or 0)
    except Exception:
        pass
    if code == 429:
        daily = "PerDay" in quota
        return (f"하루 한도 초과 {quota}" if daily else f"1분 한도 초과 {quota}".strip()), daily, delay
    return f"구글 서버 오류 {code} (한도와 무관, 잠시 뒤 풀림)", False, delay


_last_call = [0.0]


def pace(seconds):
    """무료 한도(1분에 몇 번)에 걸리지 않게 호출 사이를 띄운다."""
    wait = _last_call[0] + seconds - time.time()
    if wait > 0:
        print(f"     (한도에 안 걸리게 {wait:.0f}초 쉼)", flush=True)
        time.sleep(wait)
    _last_call[0] = time.time()


def call(method, url, headers, body=None):
    req = urllib.request.Request(url, data=json.dumps(body).encode() if body is not None else None,
                                 headers=headers, method=method)
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=180) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            msg = e.read().decode("utf-8", "replace")
            why, daily, delay = explain(e.code, msg)
            if daily:
                raise DailyLimit(f"오늘 무료 한도를 다 씀 ({why}). 한국 시간 오후 4~5시쯤 다시 채워지니 그 뒤에 실행하세요")
            if e.code in (429, 500, 503) and attempt < 4:
                wait = max(delay + 2, 20 * (attempt + 1))
                print(f"     ({why} → {wait:.0f}초 기다렸다 다시 보냄)", flush=True)
                time.sleep(wait)
                continue
            raise ApiError(f"API 오류 {e.code}: {msg}")
        except urllib.error.URLError as e:
            if attempt < 4:
                time.sleep(5)
                continue
            raise ApiError(f"연결 실패: {e}")


# ---------- chirp (Cloud Text-to-Speech) ----------

def chirp_voice(name, lang):
    return name if name.startswith(lang) else f"{lang}-Chirp3-HD-{name}"


def chirp_synthesize(text, voice, lang, speed):
    """mp3 바이트를 돌려준다. 쉼 표시([pause])는 markup 으로 보내고, 안 받는 음성이면 보통 글로 다시 보낸다."""
    def request(inp, rate):
        cfg = {"audioEncoding": "MP3"}
        if rate is not None:
            cfg["speakingRate"] = rate
        res = call("POST", f"{CLOUD_API}/text:synthesize", cloud_headers(),
                   {"input": inp, "voice": {"languageCode": lang, "name": voice}, "audioConfig": cfg})
        return base64.b64decode(res["audioContent"])

    plain = {"text": strip_pauses(text)}
    attempts = [(plain, speed), (plain, None)]
    if PAUSE.search(text):
        attempts = [({"markup": text}, speed), ({"markup": text}, None)] + attempts
    last = None
    for i, (inp, rate) in enumerate(attempts):
        try:
            data = request(inp, rate)
            if i > 0:
                dropped = [n for n, on in (("쉼 표시", "text" in inp and len(attempts) == 4), ("빠르기", rate is None)) if on]
                print(f"     (이 음성은 {'·'.join(dropped)} 설정을 받지 않아 빼고 만듦)")
            return data
        except ApiError as e:
            last = e
            if " 400:" not in str(e):  # 인증·한도 오류는 다시 보내도 같다
                break
    sys.exit(str(last))


# ---------- gemini (Gemini API 음성 모델) ----------

_models = []


def gemini_models():
    if not _models:
        _models.extend(_gemini_models())
    return list(_models)


def _gemini_models():
    res = call("GET", f"{GEMINI_API}/models?pageSize=1000", gemini_headers())
    return sorted(m["name"].split("/")[-1] for m in res.get("models", []) if "tts" in m["name"])


def gemini_pick_model():
    names = gemini_models()
    if not names:
        sys.exit("이 키로 쓸 수 있는 음성(tts) 모델이 없음")
    flash = [n for n in names if "flash" in n and "lite" not in n] or names
    return flash[-1]  # 이름순으로 가장 뒤 = 가장 새 모델


def gemini_prompt(spoken, style):
    """연기 지시(style)는 '읽지 않는 메모'로, 읽을 글은 TRANSCRIPT 아래에만 둔다 (지시문까지 읽는 일을 막는다)."""
    if not style:
        return spoken
    return ("You are a voice actor recording one line of game dialogue. "
            "Speak ONLY the text under TRANSCRIPT, exactly once, in Korean. "
            "The director's notes describe HOW to speak. Never say them aloud.\n\n"
            f"### DIRECTOR'S NOTES (do not read aloud)\n{style.strip()}\n\n"
            f"### TRANSCRIPT\n{spoken}")


def too_long(spoken, seconds):
    """지시문까지 읽었으면 음성이 대사 길이에 비해 지나치게 길다."""
    n = sum(1 for c in spoken if not c.isspace())
    return seconds > n / 1.8 + 3.0


def gemini_synthesize(text, voice, model, style):
    """(pcm 바이트, 샘플레이트)를 돌려준다. 지시문까지 읽은 것 같으면 다시 만든다."""
    spoken = strip_pauses(text)
    best = None
    for attempt in range(2):
        pcm, rate = gemini_once(spoken, voice, model, style)
        seconds = len(pcm) / 2 / rate
        if best is None or len(pcm) < len(best[0]):
            best = (pcm, rate)
        if not too_long(spoken, seconds):
            return pcm, rate
        print(f"     ({seconds:.1f}초: 대사에 비해 너무 김 → 지시문까지 읽은 듯해 다시 만듦 {attempt + 1}/2)", flush=True)
    print("     !! 두 번 모두 길게 나옴. 가장 짧은 것을 저장함. 꼭 들어 보고, 이상하면 이 줄만 --ids 로 다시 만드세요")
    return best


_spent = set()  # 오늘 한도를 다 쓴 모델


def gemini_once(spoken, voice, model, style):
    """무료 한도는 모델마다 하루 10번쯤이라, 다 쓴 모델은 건너뛰고 다른 음성 모델로 만든다."""
    order = [model] + [m for m in reversed(gemini_models()) if m != model and "pro" not in m]
    for m in order:
        if m in _spent:
            continue
        try:
            return gemini_request(spoken, voice, m, style)
        except DailyLimit:
            _spent.add(m)
            print(f"     ({m}: 오늘 무료 한도를 다 씀 → 다른 모델로 넘어감)", flush=True)
    raise DailyLimit("쓸 수 있는 음성 모델 모두 오늘 무료 한도를 다 씀. 한국 시간 오후 4~5시쯤 다시 채워지니 그 뒤에 실행하세요")


def gemini_request(spoken, voice, model, style):
    prompt = gemini_prompt(spoken, style)
    pace(21)
    body = {"contents": [{"parts": [{"text": prompt}]}],
            "generationConfig": {"responseModalities": ["AUDIO"],
                                 "speechConfig": {"voiceConfig": {"prebuiltVoiceConfig": {"voiceName": voice}}}}}
    res = call("POST", f"{GEMINI_API}/models/{model}:generateContent", gemini_headers(), body)
    try:
        part = next(p for p in res["candidates"][0]["content"]["parts"] if "inlineData" in p)["inlineData"]
    except (KeyError, IndexError, StopIteration):
        raise ApiError(f"음성이 오지 않음: {json.dumps(res, ensure_ascii=False)[:400]}")
    m = re.search(r"rate=(\d+)", part.get("mimeType", ""))
    return base64.b64decode(part["data"]), int(m.group(1)) if m else 24000


def write_wav(path, pcm, rate):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm)


# ---------- 공통 ----------

def save(dest_noext, engine, text, voice, lang, speed, model, style):
    """한 줄을 만들어 저장하고 파일 경로를 돌려준다."""
    if engine == "gemini":
        pcm, rate = gemini_synthesize(text, voice.split("-")[-1].capitalize(), model, style)
        dest = dest_noext + ".wav"
        write_wav(dest, pcm, rate)
    else:
        dest = dest_noext + ".mp3"
        with open(dest, "wb") as f:
            f.write(chirp_synthesize(text, chirp_voice(voice, lang), lang, speed))
    return dest


def remove_other_ext(dest):
    """같은 대사의 다른 형식 파일을 지운다 (유니티 연결 메뉴가 옛 파일을 잡지 않게)."""
    base, ext = os.path.splitext(dest)
    for other in (".mp3", ".wav", ".ogg"):
        if other == ext:
            continue
        for p in (base + other, base + other + ".meta"):
            if os.path.exists(p):
                os.remove(p)
                print(f"     (옛 파일 삭제: {p})")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--asset", default=ASSET)
    ap.add_argument("--out", default=OUT_DIR)
    ap.add_argument("--lines", default=LINES_FILE, help="줄마다 읽는 글·빠르기·음성을 적는 파일")
    ap.add_argument("--engine", choices=["chirp", "gemini"], help="기본: voice_lines.json 의 engine, 없으면 chirp")
    ap.add_argument("--voice", help="음성 이름 (예: Charon 또는 ko-KR-Chirp3-HD-Charon). --list-voices 로 확인")
    ap.add_argument("--lang", default="ko-KR")
    ap.add_argument("--speed", type=float, help="말 빠르기 (0.25~2.0, chirp 엔진만)")
    ap.add_argument("--model", help="gemini 엔진의 모델 이름 (기본: 쓸 수 있는 음성 모델 중 가장 새 것)")
    ap.add_argument("--ids", nargs="*", help="이 ID만 만든다")
    ap.add_argument("--speaker", default="Dokkaebi", choices=list(SPEAKERS.values()), help="이 화자 줄만 (기본: 도깨비)")
    ap.add_argument("--force", action="store_true", help="이미 있는 파일도 다시 만든다")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--list-voices", action="store_true")
    ap.add_argument("--list-models", action="store_true", help="gemini 엔진에서 쓸 수 있는 음성 모델")
    ap.add_argument("--audition", metavar="ID", help=f"이 줄을 여러 음성으로 만들어 {AUDITION_DIR}/ 에 둔다 (비교용)")
    a = ap.parse_args()

    cfg = load_settings(a.lines)
    engine = a.engine or cfg.get("engine", "chirp")
    voice = a.voice or cfg.get("voice", "Kore")
    speed = a.speed if a.speed is not None else cfg.get("speed", 1.0)
    style = cfg.get("style", "")
    per_line = cfg.get("lines", {})

    try:
        if a.list_models:
            print("\n".join(gemini_models()))
            return
        if a.list_voices:
            if engine == "gemini":
                print("gemini 엔진 음성 (일부):", ", ".join(AUDITION_VOICES))
                return
            res = call("GET", f"{CLOUD_API}/voices?languageCode={a.lang}", cloud_headers())
            for v in sorted(res.get("voices", []), key=lambda v: v["name"]):
                print(v["name"], v.get("ssmlGender", ""))
            return

        all_lines = parse_lines(a.asset)
        model = a.model or cfg.get("model")
        if engine == "gemini" and not model and not a.dry_run:
            model = gemini_pick_model()
            print(f"모델: {model}")

        def spoken(l):
            o = per_line.get(l["id"], {})
            return o.get("text") or auto_markup(l["text"]), o

        if a.audition:
            l = next((x for x in all_lines if x["id"] == a.audition), None)
            if l is None:
                sys.exit(f"대사 ID 없음: {a.audition}")
            text, o = spoken(l)
            os.makedirs(AUDITION_DIR, exist_ok=True)
            print(f"{l['id']}  {text}")
            for v in ([a.voice] if a.voice else AUDITION_VOICES):
                if a.dry_run:
                    print(f"  {v}")
                    continue
                try:
                    dest = save(os.path.join(AUDITION_DIR, f"{l['id']}_{v.split('-')[-1]}"), engine, text, v, a.lang,
                                o.get("speed", speed), model, o.get("style", style))
                    print(f"  {v} → {dest}")
                except (ApiError, SystemExit) as e:
                    print(f"  {v} 실패: {e}")
            print(f"들어 보고 고른 음성 이름을 {a.lines} 의 \"voice\" 에 적으세요")
            return

        lines = [l for l in all_lines if SPEAKERS.get(l["speaker"]) == a.speaker]
        if a.ids:
            lines = [l for l in lines if l["id"] in a.ids]
        if not lines:
            sys.exit("만들 줄이 없음")
        os.makedirs(a.out, exist_ok=True)

        print(f"엔진 {engine}, 음성 {voice}" + (f", 빠르기 {speed}" if engine == "chirp" else ""))
        total, made, failed = 0, [], []
        for l in lines:
            text, o = spoken(l)
            base = os.path.join(a.out, l["id"])
            exists = any(os.path.exists(base + e) for e in (".mp3", ".wav", ".ogg"))
            skip = exists and not a.force
            shown = text if engine == "chirp" else strip_pauses(text)
            print(f"{l['id']:4} {'건너뜀(있음, 다시 만들려면 --force)' if skip else '생성'}  {shown}")
            if skip or a.dry_run:
                continue
            try:
                dest = save(base, engine, text, a.voice or o.get("voice", voice), a.lang,
                            a.speed if a.speed is not None else o.get("speed", speed), model, o.get("style", style))
            except (ApiError, SystemExit) as e:
                failed.append(l["id"])
                print(f"     !! 실패 (옛 파일이 그대로 남음): {e}")
                continue
            remove_other_ext(dest)
            made.append(l["id"])
            total += len(text)
        if not a.dry_run:
            print(f"만든 줄: {' '.join(made) or '없음'}")
            if failed:
                print(f"!! 실패한 줄: {' '.join(failed)}  → 잠시 뒤 `--ids {' '.join(failed)} --force` 로 다시 만드세요")
        print(f"끝. 이번에 보낸 글자 수 {total}. 유니티에서 「도깨비 > 대사 음성 연결」을 실행하세요")
    except ApiError as e:
        sys.exit(str(e))


if __name__ == "__main__":
    main()
