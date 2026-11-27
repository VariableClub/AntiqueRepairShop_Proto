using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Dokkaebi.EditorTools
{
    // 3주차 알파 씬을 만든다: 메뉴 「도깨비 > 3주차 알파 씬 만들기」 (2주차 회색 박스 씬 + 3주차 시스템)
    //  - 좌표: 레벨 명세 v0.2 (원점 = 플레이 공간 중앙 바닥, +z 정면, +x 오른쪽, m). 정면 출입문·뒤쪽 반환 선반은 기획서 16장(11/6)
    //  - 씬은 작업대 높이 H = 0.90m로 만들고, 앱 시작 때 머리 높이로 H를 다시 정한다 (SYS-01, TableHeightCalibrator)
    //    작업대 위의 것은 「높이 기준 (작업대 H)」 아래에 두고, 바닥에서 올라오는 다리·받침은 FloorToHeight로 늘이고 줄인다
    //  - 씬: Assets/Scenes/Game_Logic.unity (게임 로직 씬, 17장 작업 경계상 강민우 담당). 다시 만들면 손으로 고친 배치는 사라진다
    //  - 3주차 에셋: 골동품 데이터 Assets/Data/Antiques/Antique_01_Bear.asset (SYS-04), 효과음 묶음 Assets/Audio/SoundBank_Placeholder.asset,
    //    임시음·자리표시 영상·콘티는 Placeholder 폴더. 신주연 임시 영상·소리가 오면 골동품 데이터 칸만 바꾼다
    //  - 4주차 시작(11/17): 곰 인형(수선 전·후)·단추·쪽지·솜·드라이버는 신주연 모델, 기억 영상은 EP01_Memory_Temp.mp4.
    //    모델이 없으면(LFS 미수신 등) 같은 크기·자리의 회색 박스 도형으로 만든다
    public static class GrayboxSceneBuilder
    {
        const float H = 0.90f;
        const string ScenePath = "Assets/Scenes/Game_Logic.unity";
        const string MatDir = "Assets/Art/Material/Graybox";
        const string FontTtf = "Assets/Art/Fonts/NotoSansKR-Regular.ttf";
        const string FontAssetPath = "Assets/Art/Fonts/NotoSansKR-Regular SDF.asset";
        const string DialoguePath = "Assets/Data/Dialogue/Dialogue_EP01_Bear.asset";
        const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string AntiquePath = "Assets/Data/Antiques/Antique_01_Bear.asset";
        const string BankPath = "Assets/Audio/SoundBank_Placeholder.asset";
        const string AudioDir = "Assets/Audio/Placeholder";
        const string VideoDir = "Assets/Video/Placeholder";
        const string StoryboardDir = "Assets/Art/Storyboard/Placeholder";
        const string HandDir = "Assets/Art/Textures/Handwriting";
        const string ShaderFade = "Dokkaebi/ScreenFade";
        const string ShaderScreen = "Dokkaebi/MemoryScreen";
        // 도깨비: 11/26 신주연 리깅 모델(곰 인형 도깨비, 뼈 67개, 키 1.7m)로 교체. 이전 베이스 모델은 SM_Dokkaebi_Base.fbx
        const string DokkaebiFbx = "Assets/Art/Dokkaebi/Bear/SK_Dokkaebi_Bear.fbx";
        const string DokkaebiTex = "Assets/Art/Textures/Dokkaebi/T_Dokkaebi.jpg";
        // 11/26 신주연 새 아트는 하위 폴더(Models/Workbench·ToolTray, Dokkaebi/Bear, Textures/Workbench·Dokkaebi)로 옮겼다.
        //  처음 커밋에 .meta가 없어 PC마다 다른 .meta가 생겨 있으면 같은 경로로는 pull이 막히기 때문 (경로를 바꾸면 남은 .meta는 Unity가 지움)
        // 작업대: 11/26 신주연 모델 (가로 1.95 × 높이 0.85 × 깊이 0.75m, 서랍 3개). 텍스처는 FBX가 신주연 PC 경로를 가리켜 직접 연결한다
        const string WorkbenchFbx = "Assets/Art/Models/Workbench/SM_Wooden.fbx";
        const string WorkbenchTex = "Assets/Art/Textures/Workbench/T_Wooden.jpg";
        const float WorkbenchFrontZ = 0.30f; // 플레이어(원점)에서 작업대 앞 모서리까지. 레벨 명세(상판 1.2×0.6, 가운데 z 0.6)의 앞 모서리와 같게 둔다
        // 신주연 아트 (11/17, week4/dev). 텍스처는 아직 없어 색 재질을 입힌다 (모델 재질에 텍스처가 붙어 있으면 그대로 쓴다)
        const string BearTornFbx = "Assets/Art/Models/SM_Bear_Torn.fbx";               // 수선 전: 몸 + 배 속 단추 + 접힌 쪽지
        const string BearRepairedFbx = "Assets/Art/Models/SM_Bear_Repaired.fbx";       // 수선 후
        const string StuffingFbx = "Assets/Art/Models/SM_Bear_Stuffing_Interactive.fbx"; // 솜 덩어리
        const string DriverFbx = "Assets/Art/Models/SM_Tool_Driver.fbx";               // 프롤로그 작은 드라이버
        // EP01 기억 영상 완성본 (11/27). 원본 input.realesrgan.1.mp4는 7680×4320이라 Quest용으로 1920×1080 H.264(30fps, 20.0초, 검은 배경)로 줄인 것
        const string MemoryVideoEP01 = "Assets/Art/Videos/EP01_Memory_Final.mp4";
        const string MemoryVideoEP01Temp = "Assets/Art/Videos/EP01_Memory_Temp.mp4";    // 이전 임시 영상 (1912×1080, 20.2초). 연결돼 있으면 완성본으로 바꾼다
        // MEM-06 투명 배경 영상 확인용: EP01 영상에 가장자리 12% 페이드 알파를 넣은 VP8 webm (1280×720, 30fps, 11/19).
        // 켜 두면 기억 영상이 투명 배경 영상 → (준비 실패 시) EP01 원본 순서로 재생된다. Quest에서 끊기면 끄고 씬을 다시 만든다
        const string AlphaTestVideo = "Assets/Video/Test/EP01_Memory_Temp_AlphaTest.webm";
        static bool useAlphaTestVideo = false; // 11/27 완성본 연결로 끔 (켜 두면 임시 영상으로 만든 webm이 먼저 재생됨)
        // 임포트한 곰 인형 모델이 배를 +z, 머리를 +y로 두지 않으면 여기서 돌린다
        static readonly Vector3 BearModelEuler = Vector3.zero;
        [Tooltip("도깨비 베이스 모델을 임시 연출에 쓸지 (없으면 기둥)")]
        static bool useDokkaebiModel = true;
        const float DokkaebiHeight = 1.6f;

        static Transform heightRoot;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static TMP_FontAsset font;

        [MenuItem("도깨비/3주차 알파 씬 만들기")]
        public static void Build()
        {
            // 지금 열린 씬은 건드리지 않는다: 새 씬을 추가로 열어 만들고, 저장한 뒤 닫는다
            mats.Clear();
            EnsureFolder(MatDir);
            EnsureFolder("Assets/Data/Dialogue");
            EnsureFolder("Assets/Data/Antiques");
            EnsureFolder("Assets/Audio");
            font = EnsureFont();
            var table = EnsureDialogue();
            ConfigureMediaImporters();
            var bank = EnsureSoundBank();
            var bearData = EnsureAntiqueData();

            var previous = SceneManager.GetActiveScene();
            var existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded)
            {
                if (SceneManager.sceneCount == 1) { Debug.LogWarning("[씬 만들기] Game_Logic 씬만 열려 있으면 다시 만들 수 없음. 다른 씬을 연 뒤 실행"); return; }
                EditorSceneManager.CloseScene(existing, true);
                if (previous == existing) previous = SceneManager.GetSceneAt(0);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            // ---- 조명 ----
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.19f);
            var moon = new GameObject("달빛 (보조광)").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.9f);
            moon.intensity = 0.25f;
            moon.transform.rotation = Quaternion.Euler(40f, 160f, 0f);
            var fill = new GameObject("천장 보조광 (주변 조명, 기억 장면 때 꺼짐)").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.9f, 0.78f);
            fill.intensity = 0.9f;
            fill.range = 4f;
            fill.shadows = LightShadows.None;
            fill.transform.position = new Vector3(0f, 2.3f, 0.3f);

            // ---- XR 리그 ----
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            GameObject rig = null;
            if (rigPrefab != null)
            {
                rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                rig.AddComponent<DirectHandSetup>();
                rig.AddComponent<NoLocomotion>();
            }
            else Debug.LogError($"[씬 만들기] XR 리그 프리팹을 찾지 못함: {RigPath}");
            new GameObject("XR Interaction Manager").AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();

            // ---- 공간 ----
            var env = new GameObject("공간 (회색 박스)").transform;
            heightRoot = new GameObject("높이 기준 (작업대 H, SYS-01이 앱 시작 때 옮김)").transform;
            BuildRoom(env);
            var table_ = BuildTable(env, out var lamp, out var lampShade, out var calendar, out var calendarMarks);
            BuildBlockouts(env);

            // ---- 프롤로그 ----
            var prologue = BuildPrologue(table_);

            // ---- 곰 인형 ----
            var bearSet = new GameObject("곰 인형 세트");
            bearSet.transform.SetParent(heightRoot, false);
            var repair = BuildBear(bearSet.transform, table_, out var bearSpawn);

            // ---- 처리 자리: 반환 선반(정답) + 봉인함·기억 선반(잘못된 자리, PRC-04) ----
            var shelf = BuildReturnShelf(env);
            var wrongs = BuildWrongPlaces(shelf);

            // ---- 도깨비 (임시 연출, NPC-01·02) ----
            var dok = BuildDokkaebi(env, bearData);
            // 대사 글자는 도깨비 머리 위에 띄운다 (머리 속에 묻혀 글자가 잘리지 않게). 도깨비 키는 작업대 높이와 무관하게 1.6m
            var talk = new GameObject("도깨비 대사 자리 (머리 위)").transform;
            talk.SetParent(env, false);
            talk.position = new Vector3(0f, DokkaebiHeight - 0.06f, DokkaebiZ - 0.12f);

            // ---- 시스템 ----
            var systems = new GameObject("게임 흐름");
            systems.AddComponent<AlphaLog>();
            systems.AddComponent<EventLog>();

            var sfx = new GameObject("효과음 (SND-01)").AddComponent<Sfx>();
            sfx.transform.SetParent(systems.transform, false);
            sfx.bank = bank;

            var feedback = new GameObject("조작 피드백 소리 (REP-01~04)").AddComponent<FeedbackAudio>();
            feedback.transform.SetParent(systems.transform, false);
            feedback.items.Add(new FeedbackAudio.ItemSpot { itemId = "bear", where = repair.bear.transform, data = bearData, wrongSound = SfxKey.WrongCloth });
            feedback.items.Add(new FeedbackAudio.ItemSpot { itemId = "prologue", where = prologue.toolSlots[0] != null ? prologue.toolSlots[0].transform : table_, data = null, wrongSound = SfxKey.WrongCloth });

            var fader = systems.AddComponent<ScreenFader>();
            fader.fadeMaterial = EnsureShaderMat("암전", ShaderFade);

            var calib = systems.AddComponent<TableHeightCalibrator>();
            calib.builtHeight = H;
            calib.heightRoot = heightRoot;

            var lighting = systems.AddComponent<LightingDirector>();
            lighting.lamp = lamp;
            lighting.lampShade = lampShade;
            lighting.lampBaseColor = lamp.color;
            lighting.lampBaseIntensity = lamp.intensity;
            lighting.others = new List<Light> { moon, fill };
            lighting.ambientBase = RenderSettings.ambientLight;

            var memSound = new GameObject("기억 소리 (MEM-01·02·03)").AddComponent<MemorySoundLayers>();
            memSound.transform.SetParent(systems.transform, false);
            memSound.data = bearData;

            var memVideo = BuildMemoryScreen();

            var trans = systems.AddComponent<NightTransition>();
            trans.fader = fader;
            trans.lighting = lighting;
            trans.label = BuildNightLabel(systems.transform);
            trans.calendar = calendar;
            trans.calendarMarks = calendarMarks;
            trans.returnRemnants = BuildReturnRemnants(env);

            var latency = systems.AddComponent<LatencyTest>();
            latency.flashObject = BuildLatencyFlash();
            if (rig != null)
            {
                foreach (var hp in rig.GetComponentsInChildren<HapticImpulsePlayer>(true))
                {
                    string n = hp.gameObject.name.ToLowerInvariant();
                    if (n.Contains("left") && latency.leftHand == null) latency.leftHand = hp.transform;
                    else if (n.Contains("right") && latency.rightHand == null) latency.rightHand = hp.transform;
                }
            }

            var dialogue = new GameObject("대사 표시").AddComponent<DialogueDisplay>();
            dialogue.transform.SetParent(systems.transform, false);
            dialogue.table = table;
            dialogue.font = font;
            dialogue.overlayMaterial = EnsureOverlayFontMat();

            var flow = systems.AddComponent<GameFlow>();
            flow.tableHeight = H;
            flow.calibrator = calib;
            flow.prologue = prologue;
            flow.bear = repair;
            flow.bearData = bearData;
            flow.bearSet = bearSet;
            flow.returnShelf = shelf;
            flow.wrongPlaces = wrongs;
            flow.dialogue = dialogue;
            flow.fader = fader;
            flow.lighting = lighting;
            flow.dokkaebi = dok;
            flow.dokkaebiTalkAnchor = talk;
            flow.memorySound = memSound;
            flow.memoryVideo = memVideo;
            flow.transition = trans;
            flow.doorBell = GameObject.Find("출입문 종")?.transform;
            flow.bearSpawn = bearSpawn;
            flow.ledger = BuildLedger(shelf.transform);

            // ---- 힌트 (HNT-01~04, 5주차) ----
            // 정답 위치: 1~3단계 찢어진 배(솜 넣는 자리), 4단계 눈 단추 자리, 5단계는 다음 바늘구멍(HintDirector가 따라감)
            var hints = new GameObject("힌트 (HNT-01~04)").AddComponent<HintDirector>();
            hints.transform.SetParent(systems.transform, false);
            hints.repair = repair;
            hints.data = bearData;
            hints.dialogue = dialogue;
            hints.lineAnchor = repair.bear.transform;
            var tear = repair.belly != null ? repair.belly.transform : repair.bear.transform;
            hints.stepTargets = new[] { tear, tear, tear, repair.eye != null ? repair.eye.transform : tear, tear };
            hints.lightDot = BuildHintDot(systems.transform);

            var keys = systems.AddComponent<DebugKeys>();
            keys.flow = flow;
            keys.hints = hints;

            // 높이를 따라 움직이는 것들은 정적 배치(Static Batching)에서 빼야 움직인다
            SetNonStatic(heightRoot);
            foreach (var f in Object.FindObjectsByType<FloorToHeight>(FindObjectsInactive.Include)) f.gameObject.isStatic = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            if (previous.IsValid() && previous != scene)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[씬 만들기] {ScenePath} 저장 완료 (Project 창에서 열기). 플레이 후 N=다음, B=억지 손상, H=힌트 한 단계, W=잘못된 처리 자리, V=기억 영상 방식, R=수선 기록, L=지연 확인");
        }

        // ================= 공간 =================

        static void BuildRoom(Transform env)
        {
            var wall = Mat("벽", new Color(0.36f, 0.33f, 0.30f));
            var floor = Prim(PrimitiveType.Plane, "바닥", env, Vector3.zero, new Vector3(0.3f, 1f, 0.3f), Mat("바닥", new Color(0.28f, 0.24f, 0.2f)), true);
            Prim(PrimitiveType.Cube, "정면 벽", env, new Vector3(0f, 1.2f, 1.45f), new Vector3(3f, 2.4f, 0.05f), wall, true);
            Prim(PrimitiveType.Cube, "뒤쪽 벽", env, new Vector3(0f, 1.2f, -1.15f), new Vector3(3f, 2.4f, 0.05f), wall, true);
            Prim(PrimitiveType.Cube, "왼쪽 벽", env, new Vector3(-1.4f, 1.2f, 0.15f), new Vector3(0.05f, 2.4f, 2.6f), wall, true);
            Prim(PrimitiveType.Cube, "오른쪽 벽", env, new Vector3(1.4f, 1.2f, 0.15f), new Vector3(0.05f, 2.4f, 2.6f), wall, true);
            Prim(PrimitiveType.Cube, "정면 출입문", env, new Vector3(0.55f, 1.0f, 1.42f), new Vector3(0.9f, 2.0f, 0.04f), Mat("문", new Color(0.33f, 0.22f, 0.14f)), false);
            Prim(PrimitiveType.Sphere, "출입문 종", env, new Vector3(0.55f, 2.08f, 1.38f), Vector3.one * 0.06f, Mat("금속", new Color(0.75f, 0.62f, 0.3f)), false);
            Prim(PrimitiveType.Cube, "뒤쪽 작은 창", env, new Vector3(0f, 1.45f, -1.12f), new Vector3(0.6f, 0.45f, 0.03f), Mat("창", new Color(0.12f, 0.16f, 0.25f)), false);
        }

        static Transform BuildTable(Transform env, out Light lamp, out Renderer lampShade, out Transform calendar, out GameObject[] calendarMarks)
        {
            var wood = Mat("작업대", new Color(0.45f, 0.32f, 0.2f));
            var root = new GameObject("작업대 (H 0.90m)").transform;
            root.SetParent(heightRoot, false);
            // 신주연 작업대 모델이 있으면 그 모델로, 없으면 회색 박스 상판·다리로 만든다 (11/26)
            if (!BuildWorkbenchModel(env, root))
            {
                Prim(PrimitiveType.Cube, "상판", root, new Vector3(0f, H - 0.02f, 0.6f), new Vector3(1.2f, 0.04f, 0.6f), wood, true);
                foreach (var x in new[] { -0.56f, 0.56f })
                    foreach (var z in new[] { 0.34f, 0.86f })
                    {
                        var leg = Prim(PrimitiveType.Cube, "다리", env, new Vector3(x, (H - 0.04f) / 2f, z), new Vector3(0.05f, H - 0.04f, 0.05f), wood, true);
                        leg.AddComponent<FloorToHeight>().topOffset = -0.04f;
                    }
            }
            Prim(PrimitiveType.Cube, "수선 매트", root, new Vector3(0f, H + 0.002f, 0.6f), new Vector3(0.5f, 0.004f, 0.4f), Mat("매트", new Color(0.2f, 0.35f, 0.3f)), false);
            Prim(PrimitiveType.Cube, "사진 자리 (셋째 밤)", root, new Vector3(0.5f, H + 0.002f, 0.8f), new Vector3(0.15f, 0.004f, 0.2f), Mat("표시", new Color(0.3f, 0.3f, 0.3f)), false);
            var cal = Prim(PrimitiveType.Cube, "탁상 달력", root, new Vector3(-0.42f, H + 0.075f, 0.86f), new Vector3(0.2f, 0.15f, 0.04f), Mat("종이", new Color(0.88f, 0.85f, 0.76f)), false);
            calendar = cal.transform;
            // 달력 날짜 칸 (밤마다 지난 날짜에 X, 10장). 첫째 날 칸에는 손으로 그린 동그라미
            var ink = Mat("잉크", new Color(0.15f, 0.1f, 0.08f));
            var red = Mat("달력 동그라미", new Color(0.7f, 0.15f, 0.1f));
            calendarMarks = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                var cell = new Vector3(-0.48f + i * 0.04f, H + 0.06f, 0.86f - 0.021f);
                Prim(PrimitiveType.Quad, $"날짜 칸 {i + 1}", root, cell, new Vector3(0.03f, 0.03f, 1f), Mat("날짜 칸", new Color(0.8f, 0.77f, 0.68f)), false);
                if (i == 0) Prim(PrimitiveType.Quad, "첫째 날 동그라미", root, cell - new Vector3(0f, 0f, 0.0005f), new Vector3(0.034f, 0.034f, 1f), red, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var x = new GameObject($"X 표시 {i + 1}째 밤 지남");
                x.transform.SetParent(root, false);
                x.transform.position = cell - new Vector3(0f, 0f, 0.001f);
                foreach (float a in new[] { 45f, -45f })
                    NoCol(Child(PrimitiveType.Cube, "획", x.transform, Vector3.zero, Quaternion.Euler(0f, 0f, a), new Vector3(0.036f, 0.004f, 0.001f), ink));
                calendarMarks[i] = x;
            }

            // 스탠드 (조명의 중심, 레벨 명세 7장). 점등 연출은 EP0-01 (LightingDirector)
            var metal = Mat("스탠드", new Color(0.2f, 0.2f, 0.22f));
            Prim(PrimitiveType.Cylinder, "스탠드 받침", root, new Vector3(-0.55f, H + 0.01f, 0.82f), new Vector3(0.12f, 0.01f, 0.12f), metal, false);
            Prim(PrimitiveType.Cylinder, "스탠드 기둥", root, new Vector3(-0.55f, H + 0.22f, 0.82f), new Vector3(0.015f, 0.21f, 0.015f), metal, false);
            var shade = Prim(PrimitiveType.Sphere, "스탠드 갓", root, new Vector3(-0.5f, H + 0.43f, 0.76f), new Vector3(0.12f, 0.07f, 0.12f), Mat("갓", new Color(0.85f, 0.7f, 0.4f)), false);
            lampShade = shade.GetComponent<Renderer>();
            lamp = new GameObject("스탠드 빛 (기본색)").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = new Vector3(-0.45f, H + 0.38f, 0.7f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.82f, 0.6f);
            // 밝기는 Performance URP 설정(추가 조명 켬, HDR 없음)에서 종이·도깨비가 하얗게 날아가지 않는 값
            lamp.range = 3f;
            lamp.intensity = 1.1f;
            return root;
        }

        // 작업대 모델 (11/26, 신주연 SM_Wooden). 서랍이 플레이어 쪽을 보게 180° 돌리고, 앞 모서리를 z 0.30에 둔다.
        //  - 높이: 모델은 0.85m로 고정이라 SYS-01(머리 높이로 H를 정함)을 따르려면 세로로 늘이고 줄여야 한다.
        //    「작업대 모델 (높이 맞춤)」(가운데 기준 높이 1)이 FloorToHeight로 바닥~H를 채우고, 모델은 그 안에 꽉 차게 넣는다 (H 0.90이면 세로 1.06배)
        //  - 가로·깊이는 모델 그대로 (1.95×0.75m). 레벨 명세(1.2×0.6m)보다 커서 옆의 할머니 서랍·기억 선반을 옮겼다 (BuildBlockouts)
        //  - 상판 위 물건이 놓이는 충돌은 보이지 않는 상판 상자 하나로 (모델 메시 충돌은 서랍 손잡이 등이 걸려서 쓰지 않음)
        static Vector2 workbenchSize = new Vector2(1.2f, 0.6f); // 가로·깊이. 모델이 있으면 모델 크기
        static float WorkbenchCenterZ => WorkbenchFrontZ + workbenchSize.y * 0.5f;
        static float DokkaebiZ => Mathf.Max(1.12f, WorkbenchFrontZ + workbenchSize.y + 0.19f);
        static float MemoryShelfZ => workbenchSize.x > 1.3f ? 0.10f : 0.4f;

        static bool BuildWorkbenchModel(Transform env, Transform tableRoot)
        {
            var model = InstantiateModel(WorkbenchFbx);
            if (model == null)
            {
                Debug.LogWarning($"[씬 만들기] 작업대 모델을 찾지 못해 회색 박스로 만듦: {WorkbenchFbx} (Git LFS를 받았는지 확인)");
                workbenchSize = new Vector2(1.2f, 0.6f);
                return false;
            }
            model.name = "작업대 모델 (SM_Wooden, 신주연)";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WorkbenchTex);
            var mat = ModelMat("M_Wooden (작업대, 신주연 텍스처)", tex, new Color(0.45f, 0.32f, 0.2f), 0.25f);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++) shared[i] = mat;
                r.sharedMaterials = shared;
            }

            var fit = new GameObject("작업대 모델 (높이 맞춤, SYS-01)").transform;
            fit.SetParent(env, false);
            model.transform.SetParent(fit, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 서랍이 플레이어(원점) 쪽
            var b = WorldBounds(model);
            if (b.size.y < 0.01f) { Object.DestroyImmediate(fit.gameObject); workbenchSize = new Vector2(1.2f, 0.6f); return false; }
            workbenchSize = new Vector2(b.size.x, b.size.z);
            // fit 안에서 모델이 높이 1(-0.5~0.5)을 꽉 채우게
            var ls = model.transform.localScale;
            model.transform.localScale = new Vector3(ls.x, ls.y / b.size.y, ls.z);
            b = WorldBounds(model);
            model.transform.localPosition += new Vector3(-b.center.x, -0.5f - b.min.y, -b.center.z);
            fit.position = new Vector3(0f, H * 0.5f, WorkbenchCenterZ);
            fit.localScale = new Vector3(1f, H, 1f);
            fit.gameObject.AddComponent<FloorToHeight>().topOffset = 0f;

            // 물건이 놓이는 상판 (보이지 않는 충돌 상자)
            var top = Prim(PrimitiveType.Cube, "상판 (충돌만, 모습은 작업대 모델)", tableRoot, new Vector3(0f, H - 0.02f, WorkbenchCenterZ), new Vector3(workbenchSize.x, 0.04f, workbenchSize.y), Mat("작업대", new Color(0.45f, 0.32f, 0.2f)), true);
            top.GetComponent<Renderer>().enabled = false;
            Debug.Log($"[씬 만들기] 작업대 모델 사용: {workbenchSize.x:0.00}×{workbenchSize.y:0.00}m, 앞 모서리 z {WorkbenchFrontZ:0.00}, 높이 H {H:0.00}에 맞춤");
            return true;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        // 신주연 모델용 재질: 텍스처가 있으면 텍스처, 없으면 색 (URP Lit). 모델 FBX의 임베디드 재질은 텍스처 경로가 신주연 PC라 쓰지 않는다 (11/26)
        static Material ModelMat(string name, Texture2D tex, Color fallback, float smoothness)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
                m.SetColor("_BaseColor", Color.white);
                m.color = Color.white;
            }
            else
            {
                Debug.LogWarning($"[씬 만들기] {name}: 텍스처를 못 찾아 색으로 대신함");
                m.SetColor("_BaseColor", fallback);
                m.color = fallback;
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static void BuildBlockouts(Transform env)
        {
            var gray = Mat("블록아웃", new Color(0.5f, 0.5f, 0.52f));
            // 11/26 작업대 모델(가로 1.95m)과 겹치지 않게 작업대 왼쪽 끝 바깥으로 (이전 x -0.8). 왼쪽 벽 안쪽 면 x -1.375까지
            float drawerX = -Mathf.Max(0.8f, workbenchSize.x * 0.5f + 0.2f);
            Prim(PrimitiveType.Cube, "할머니 서랍 (블록아웃)", env, new Vector3(drawerX, H / 2f, 0.45f), new Vector3(0.4f, H, 0.4f), gray, true).AddComponent<FloorToHeight>().topOffset = 0f;
            var mem = new GameObject("기억 선반 (블록아웃, 격자 책장은 레벨 명세 v0.3)").transform;
            mem.SetParent(env, false);
            // 11/26 작업대 모델과 겹치지 않게 작업대 앞 모서리보다 플레이어 쪽(z 0.10)으로 (이전 z 0.4). 오른쪽 봉인함(z -0.35~-0.05)과는 2.5cm 띄움
            Prim(PrimitiveType.Cube, "틀", mem, new Vector3(0.9f, (H + 0.45f) / 2f, MemoryShelfZ), new Vector3(0.5f, H + 0.45f, 0.25f), gray, true).AddComponent<FloorToHeight>().topOffset = 0.45f;
            Prim(PrimitiveType.Cube, "봉인함 (블록아웃)", env, new Vector3(0.85f, (H - 0.1f) / 2f, -0.2f), new Vector3(0.4f, H - 0.1f, 0.3f), Mat("봉인함", new Color(0.3f, 0.25f, 0.22f)), true).AddComponent<FloorToHeight>().topOffset = -0.1f;
        }

        // 잘못된 처리 자리 (PRC-04): 봉인함 위, 기억 선반 앞. 곰 인형을 놓으면 튕기고 반환 선반 윤곽이 깜박인다
        static WrongPlaceZone[] BuildWrongPlaces(PlaceZone correct)
        {
            var seal = new GameObject("잘못된 처리 자리 - 봉인함 (PRC-04)").AddComponent<WrongPlaceZone>();
            seal.transform.SetParent(heightRoot, false);
            seal.transform.position = new Vector3(0.85f, H - 0.1f + 0.14f, -0.2f);
            seal.halfExtents = new Vector3(0.24f, 0.16f, 0.19f);
            seal.zoneName = "봉인함";
            seal.correct = correct;
            var keep = new GameObject("잘못된 처리 자리 - 기억 선반 (PRC-04)").AddComponent<WrongPlaceZone>();
            keep.transform.SetParent(heightRoot, false);
            keep.transform.position = new Vector3(0.9f, H + 0.12f, MemoryShelfZ);
            keep.halfExtents = new Vector3(0.3f, 0.35f, 0.2f);
            keep.zoneName = "기억 선반";
            keep.correct = correct;
            return new[] { seal, keep };
        }

        static PlaceZone BuildReturnShelf(Transform env)
        {
            var root = new GameObject("반환 선반 (뒤쪽 작은 창 앞)").transform;
            root.SetParent(heightRoot, false);
            float top = H - 0.15f;
            var wood = Mat("선반", new Color(0.42f, 0.3f, 0.2f));
            Prim(PrimitiveType.Cube, "선반 판", root, new Vector3(0f, top - 0.01f, -0.70f), new Vector3(0.5f, 0.02f, 0.25f), wood, true);
            Prim(PrimitiveType.Cube, "받침", env, new Vector3(0f, (top - 0.02f) / 2f, -0.70f), new Vector3(0.44f, top - 0.02f, 0.2f), wood, true).AddComponent<FloorToHeight>().topOffset = -0.17f;

            var zoneGo = new GameObject("반환 판정 (PRC-01)");
            zoneGo.transform.SetParent(root, false);
            zoneGo.transform.position = new Vector3(0f, top + 0.1f, -0.70f);
            var seat = new GameObject("앉는 자리").transform;
            seat.SetParent(zoneGo.transform, false);
            seat.position = new Vector3(0f, top, -0.70f);
            var zone = zoneGo.AddComponent<PlaceZone>();
            zone.seat = seat;
            // 빛 윤곽: 수선 후 곰 인형 모델 모양 (없으면 캡슐). 앉은 곰이 플레이어(+z) 쪽을 본다
            var glowMat = TransparentMat("빛 윤곽", new Color(1f, 0.92f, 0.6f, 0.35f));
            GameObject outline = null;
            var repModel = InstantiateModel(BearRepairedFbx);
            var repMf = repModel != null ? repModel.GetComponentInChildren<MeshFilter>() : null;
            if (repMf != null && repMf.sharedMesh != null)
            {
                outline = new GameObject("곰 인형 빛 윤곽 (SM_Bear_Repaired 모양)");
                outline.transform.SetParent(zoneGo.transform, false);
                outline.AddComponent<MeshFilter>().sharedMesh = repMf.sharedMesh;
                var mr = outline.AddComponent<MeshRenderer>();
                var glow = new Material[repMf.sharedMesh.subMeshCount];
                for (int i = 0; i < glow.Length; i++) glow[i] = glowMat;
                mr.sharedMaterials = glow;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.transform.rotation = repMf.transform.rotation; // 임포트 회전 그대로 + 곰 인형 보정
                outline.transform.localRotation = Quaternion.Euler(BearModelEuler) * outline.transform.localRotation;
                outline.transform.localScale = Vector3.one * 1.04f;
                outline.transform.position = seat.position;
                var ob = BoundsIn(zoneGo.transform, outline);
                outline.transform.localPosition += seat.localPosition - new Vector3(ob.center.x, ob.min.y, ob.center.z);
            }
            if (repModel != null) Object.DestroyImmediate(repModel);
            if (outline == null)
                outline = Prim(PrimitiveType.Capsule, "곰 인형 빛 윤곽 (임시)", zoneGo.transform, new Vector3(0f, top + 0.15f, -0.70f), new Vector3(0.2f, 0.15f, 0.17f), glowMat, false);
            zone.outline = outline;
            outline.SetActive(false);
            return zone;
        }

        // ================= 프롤로그 =================

        static PrologueController BuildPrologue(Transform table)
        {
            var root = new GameObject("프롤로그").transform;
            root.SetParent(table, false);

            // 도구·부품 트레이 (레벨 명세 2.3)
            var trayMat = Mat("트레이", new Color(0.36f, 0.27f, 0.18f));
            var groove = Mat("홈", new Color(0.1f, 0.08f, 0.06f));
            var tray = Prim(PrimitiveType.Cube, "도구·부품 트레이", root, new Vector3(-0.43f, H + 0.01f, 0.48f), new Vector3(0.30f, 0.02f, 0.35f), trayMat, true).transform;

            string[] keys = { "tool_tweezers", "tool_driver", "tool_magnifier", "tool_scissors" };
            string[] names = { "핀셋", "작은 드라이버", "돋보기", "가위" };
            Vector3[] footprint = { new Vector3(0.02f, 0.002f, 0.13f), new Vector3(0.03f, 0.002f, 0.14f), new Vector3(0.065f, 0.002f, 0.14f), new Vector3(0.055f, 0.002f, 0.13f) };
            float[] slotX = { -0.545f, -0.495f, -0.43f, -0.355f };
            var slots = new FitSocket[4];
            var slotGlows = new Renderer[4];
            // 홈 빛 힌트 (11/25): 처리 자리 빛 윤곽과 같은 재질. 홈보다 사방 4mm 크게, 홈 바로 위에 얹는다
            var slotGlowMat = TransparentMat("빛 윤곽", new Color(1f, 0.92f, 0.6f, 0.35f));
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(slotX[i], H + 0.03f, 0.42f);
                Prim(PrimitiveType.Cube, $"홈 - {names[i]}", root, pos - new Vector3(0f, 0.0085f, 0f), footprint[i], groove, false);
                var glowGo = Prim(PrimitiveType.Cube, $"홈 빛 - {names[i]} (힌트, 11/25)", root, pos - new Vector3(0f, 0.0065f, 0f), footprint[i] + new Vector3(0.008f, 0f, 0.008f), slotGlowMat, false);
                slotGlows[i] = glowGo.GetComponent<Renderer>();
                glowGo.SetActive(false);
                var s = new GameObject($"도구 자리 - {names[i]} (EP0-02)").AddComponent<FitSocket>();
                s.transform.SetParent(root, false);
                s.transform.position = pos;
                s.acceptKey = keys[i];
                s.positionTolerance = 0.02f;
                s.angleMode = AngleMode.AxisLine;
                s.angleTolerance = 20f;
                s.detectRadius = 0.07f;
                s.itemId = "prologue";
                s.step = 0;
                s.active = false;
                slots[i] = s;
            }

            // 흩어진 도구 4개
            Vector3[] toolPos = { new Vector3(0.12f, H + 0.02f, 0.42f), new Vector3(0.27f, H + 0.025f, 0.5f), new Vector3(0.12f, H + 0.02f, 0.68f), new Vector3(0.3f, H + 0.02f, 0.74f) };
            float[] toolYaw = { 30f, -60f, 80f, 10f };
            var steel = Mat("도구 쇠", new Color(0.72f, 0.74f, 0.78f));
            var handle = Mat("도구 손잡이", new Color(0.55f, 0.18f, 0.12f));
            var toolGlows = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                var t = NewGrabbable($"도구 - {names[i]}", root, toolPos[i], Quaternion.Euler(0f, toolYaw[i], 0f), keys[i], GrabCategory.Tool, 0.05f);
                switch (i)
                {
                    case 0:
                        NoCol(Child(PrimitiveType.Cube, "날", t, Vector3.zero, Quaternion.identity, new Vector3(0.012f, 0.01f, 0.12f), steel));
                        AddBox(t, new Vector3(0.02f, 0.02f, 0.12f));
                        break;
                    case 1:
                        // 신주연 모델 SM_Tool_Driver (길이 14cm). 긴 축을 앞(Z)으로, 손잡이가 -Z 쪽 (홈 AxisLine 판정 축)
                        var drv = InstantiateModel(DriverFbx);
                        if (drv != null)
                        {
                            drv.name = "작은 드라이버 (SM_Tool_Driver, 신주연)";
                            CenterIn(drv.transform, t, Vector3.forward); // 모델 +Y(날 끝)가 +Z, 손잡이가 -Z
                            Paint(drv, handle);
                            foreach (var c in drv.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                            var db = BoundsIn(t, drv);
                            AddBox(t, Vector3.Max(db.size, new Vector3(0.025f, 0.025f, 0.1f))).center = db.center;
                        }
                        else
                        {
                            NoCol(Child(PrimitiveType.Cylinder, "손잡이", t, new Vector3(0f, 0f, -0.03f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.025f, 0.035f, 0.025f), handle));
                            NoCol(Child(PrimitiveType.Cylinder, "축", t, new Vector3(0f, 0f, 0.035f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.006f, 0.035f, 0.006f), steel));
                            AddBox(t, new Vector3(0.03f, 0.03f, 0.14f));
                        }
                        break;
                    case 2:
                        NoCol(Child(PrimitiveType.Cylinder, "렌즈", t, new Vector3(0f, 0f, 0.035f), Quaternion.identity, new Vector3(0.06f, 0.004f, 0.06f), Mat("렌즈", new Color(0.7f, 0.85f, 0.95f))));
                        NoCol(Child(PrimitiveType.Cube, "손잡이", t, new Vector3(0f, 0f, -0.035f), Quaternion.identity, new Vector3(0.016f, 0.012f, 0.07f), handle));
                        AddBox(t, new Vector3(0.06f, 0.02f, 0.14f));
                        break;
                    case 3:
                        NoCol(Child(PrimitiveType.Cube, "날 1", t, new Vector3(0.006f, 0f, 0.02f), Quaternion.Euler(0f, 8f, 0f), new Vector3(0.01f, 0.006f, 0.09f), steel));
                        NoCol(Child(PrimitiveType.Cube, "날 2", t, new Vector3(-0.006f, 0.004f, 0.02f), Quaternion.Euler(0f, -8f, 0f), new Vector3(0.01f, 0.006f, 0.09f), steel));
                        NoCol(Child(PrimitiveType.Cylinder, "고리 1", t, new Vector3(0.014f, 0f, -0.04f), Quaternion.identity, new Vector3(0.025f, 0.004f, 0.025f), handle));
                        NoCol(Child(PrimitiveType.Cylinder, "고리 2", t, new Vector3(-0.014f, 0f, -0.04f), Quaternion.identity, new Vector3(0.025f, 0.004f, 0.025f), handle));
                        AddBox(t, new Vector3(0.055f, 0.02f, 0.13f));
                        break;
                }
                // 도구 빛 힌트 (11/25): 아직 홈에 넣지 않은 도구도 빛나게 해 '무엇을' 넣는지 알려 준다. 도구 크기보다 사방 4mm 큰 반투명 상자, 도구를 따라다닌다
                var tb = BoundsIn(t, t.gameObject);
                var tg = NoCol(Child(PrimitiveType.Cube, $"도구 빛 - {names[i]} (힌트, 11/25)", t, tb.center, Quaternion.identity, tb.size + Vector3.one * 0.008f, slotGlowMat));
                toolGlows[i] = tg.GetComponent<Renderer>();
                tg.SetActive(false);
            }

            // 바늘꽂이·솜 상자·부품 칸(필름)은 곰 인형 쪽에서 만든다

            // 도구함 + 뚜껑 안쪽 할머니 메모 (EP0-03)
            var boxMat = Mat("도구함", new Color(0.4f, 0.28f, 0.17f));
            Prim(PrimitiveType.Cube, "도구함", root, new Vector3(-0.18f, H + 0.025f, 0.8f), new Vector3(0.22f, 0.05f, 0.12f), boxMat, true);
            var hinge = new GameObject("도구함 뚜껑 경첩").transform;
            hinge.SetParent(root, false);
            hinge.position = new Vector3(-0.18f, H + 0.05f, 0.86f);
            Child(PrimitiveType.Cube, "뚜껑", hinge, new Vector3(0f, 0.004f, -0.06f), Quaternion.identity, new Vector3(0.22f, 0.008f, 0.12f), boxMat);
            var memoGo = new GameObject("할머니 메모 (뚜껑 안쪽)");
            memoGo.transform.SetParent(hinge, false);
            memoGo.transform.localPosition = new Vector3(0f, -0.001f, -0.06f);
            memoGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 닫혀 있을 때 아래를 보고, 열리면 플레이어 쪽을 본다
            var memoTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HandDir}/Memo_Grandma_Prologue.png");
            NoCol(Child(PrimitiveType.Quad, "종이 (UI-04 손글씨)", memoGo.transform, Vector3.zero, Quaternion.identity, new Vector3(0.18f, 0.09f, 1f),
                memoTex != null ? TexturedMat("메모 손글씨", memoTex, Vector2.one, Vector2.zero) : Mat("메모 종이", new Color(0.92f, 0.88f, 0.78f))));
            if (memoTex == null)
            {
                var memoText = Text("메모 글씨 (임시 글자, 손글씨 텍스처 없음)", memoGo.transform, new Vector3(0f, 0f, -0.001f), "물건은 망가져 제 모습을 잃어도,\n그 안에 담긴 기억까지\n사라지는 건 아니란다.", 0.012f, new Color(0.2f, 0.15f, 0.1f));
                // 종이(18×9cm) 안에 들어가게 글자 크기를 맞춘다
                memoText.rectTransform.sizeDelta = new Vector2(0.165f, 0.08f);
                memoText.fontSizeMax = memoText.fontSize;
                memoText.fontSizeMin = memoText.fontSize * 0.4f;
                memoText.enableAutoSizing = true;
            }
            var glow = NoCol(Child(PrimitiveType.Quad, "메모 은은한 빛 (10초 안 볼 때)", memoGo.transform, new Vector3(0f, 0f, 0.001f), Quaternion.identity, new Vector3(0.2f, 0.11f, 1f), TransparentMat("메모 빛", new Color(1f, 0.9f, 0.5f, 0.35f))));
            var gaze = memoGo.AddComponent<GazeTarget>();

            var pc = root.gameObject.AddComponent<PrologueController>();
            pc.toolSlots = slots;
            pc.slotGlows = slotGlows;
            pc.toolGlows = toolGlows;
            pc.lid = hinge;
            pc.lidOpenEuler = new Vector3(100f, 0f, 0f); // 앞쪽 가장자리가 위·뒤로 들린다
            pc.memo = gaze;
            pc.memoGlow = glow;
            return pc;
        }

        // ================= 곰 인형 =================

        // 곰 인형 기준점(로컬): 발 아래 가운데 = 원점, +y 머리, +z 배, 단위 m.
        // 아래 자리들은 신주연 모델 SM_Bear_Torn(높이 30cm, 폭 25.5cm, 깊이 16.8cm) 표면에서 잰 값 (11/17).
        // 모델이 없을 때(LFS를 받지 않았을 때 등)도 같은 자리를 쓰고, 회색 박스 도형을 모델 크기에 맞춰 둔다.
        static readonly Vector3 BearEyePos = new Vector3(-0.036f, 0.220f, 0.038f);           // 빠진 눈 자리 얼굴 표면 (플레이어가 보면 오른쪽 눈). 수선 후 모델의 눈은 여기서 7mm 솟음
        static readonly Vector3 BearEyeNormal = new Vector3(-0.45f, 0.11f, 0.885f).normalized; // 그 자리 얼굴 바깥 방향
        static readonly Vector3 BearOtherEyePos = new Vector3(0.036f, 0.220f, 0.038f);       // 남은 눈 자리 (회색 박스용, 빠진 눈의 거울 자리)
        static readonly Vector3 BearTearPos = new Vector3(0f, 0.085f, 0.052f);               // 찢어진 배 가운데, 벌어진 입구 높이 (세로 약 7cm, 가로 3.5cm)
        static readonly Vector3 BearTearNormal = new Vector3(0f, 0.28f, 0.96f).normalized;   // 배 바깥 방향 (위로 약 16° 기움)
        static readonly Vector3[] BearHolePos =                                              // 찢어진 곳 양옆 바늘구멍 4개 (위→아래 지그재그)
        {
            new Vector3(-0.024f, 0.109f, 0.0415f), new Vector3(0.024f, 0.0934f, 0.0468f),
            new Vector3(-0.024f, 0.0772f, 0.0513f), new Vector3(0.024f, 0.0614f, 0.0538f),
        };
        static readonly Vector3 BearButtonPosFallback = new Vector3(-0.004f, 0.041f, 0.040f); // 모델 안 단추 자리
        static readonly Vector3 BearNotePosFallback = new Vector3(-0.009f, 0.089f, 0.019f);   // 모델 안 쪽지 자리
        const float BearDepthHalf = 0.084f; // 등까지 거리 (눕혔을 때 매트 위로 띄울 높이)
        // 쪽지 충돌 두께. 보이는 종이(1mm)보다 두껍게 잡아 작업대에 파고들지 않게 한다 (작업대 위 약 5mm 뜸, 11/19)
        const float NoteColliderThickness = 0.012f;

        static BearRepair BuildBear(Transform set, Transform table, out Transform spawn)
        {
            var fur = Mat("곰 인형 천", new Color(0.62f, 0.45f, 0.3f));
            var nose = Mat("곰 인형 코", new Color(0.12f, 0.08f, 0.06f));
            var dark = Mat("어두운 표시", new Color(0.08f, 0.06f, 0.05f));
            var tearBig = Mat("찢어진 곳 (손상 A)", new Color(0.5f, 0.08f, 0.06f));
            var cotton = Mat("솜", new Color(0.96f, 0.96f, 0.94f));
            var thread = Mat("실", new Color(0.85f, 0.2f, 0.2f));
            var buttonMat = Mat("단추", new Color(0.18f, 0.1f, 0.06f));
            var paper = Mat("메모 종이", new Color(0.92f, 0.88f, 0.78f));

            // 배가 위로 오게 눕힌다 (레벨 명세 4장). 기준점은 발 쪽 아래, 머리는 플레이어 쪽. 등이 매트에 닿게 띄운다
            spawn = new GameObject("곰 인형 놓이는 자리 (매트)").transform;
            spawn.SetParent(table, false);
            spawn.SetPositionAndRotation(new Vector3(0f, H + BearDepthHalf + 0.006f, 0.74f), Quaternion.Euler(-90f, 0f, 0f));

            var bearT = NewGrabbable("곰 인형 (EP1-01)", set, spawn.position, spawn.rotation, "bear", GrabCategory.Item, 1.5f);
            var body = bearT.GetComponent<Rigidbody>();
            body.linearDamping = 1f;
            body.angularDamping = 2f;

            // ---- 모습: 신주연 모델 (수선 전 / 수선 후) ----
            Transform buttonMesh = null, noteMesh = null, pulseTarget = null;
            GameObject before = null, after = null;
            var torn = InstantiateModel(BearTornFbx);
            if (torn != null)
            {
                torn.name = "모습 - 수선 전 (SM_Bear_Torn, 신주연)";
                var tornBody = FindDeep(torn.transform, "SM_Bear_Belly_Torn");
                AlignToBear(torn.transform, bearT, tornBody != null ? tornBody.gameObject : torn);
                pulseTarget = tornBody; // 몸 메시는 기준점이 몸 가운데. FBX 뿌리는 기준점이 멀리 있어 키우면 몸이 밀려난다
                buttonMesh = FindDeep(torn.transform, "SM_Bear_Button");
                noteMesh = FindDeep(torn.transform, "SM_Teddy_Note_Folded");
                // 단추·쪽지는 따로 잡는 부품이 되므로 아래에서 꺼내 간다. 몸에만 천·코 색을 입힌다 (텍스처가 오면 모델 재질을 그대로 쓴다)
                if (buttonMesh != null) buttonMesh.SetParent(null, true);
                if (noteMesh != null) noteMesh.SetParent(null, true);
                PaintBearBody(torn, fur, nose);
                before = torn;
            }
            else
            {
                Debug.LogWarning($"[씬 만들기] 곰 인형 모델을 찾지 못해 회색 박스 도형으로 만듦: {BearTornFbx} (Git LFS를 받았는지 확인)");
                before = BuildBearBlockout(bearT, fur, dark);
                pulseTarget = before.transform.Find("몸통");
            }
            var repairedModel = InstantiateModel(BearRepairedFbx);
            if (repairedModel != null)
            {
                repairedModel.name = "모습 - 수선 후 (SM_Bear_Repaired, 신주연)";
                AlignToBear(repairedModel.transform, bearT, repairedModel);
                PaintBearBody(repairedModel, fur, nose);
                after = repairedModel;
                after.SetActive(false); // S5 완성 때 BearView가 켠다
            }
            foreach (var c in bearT.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // 잡기·충돌용 콜라이더 (몸통 상자 + 머리 구)
            //  - 몸통 상자 앞면은 배 표면(z 약 0.045~0.055)보다 안쪽 z 0.035: 바늘 끝이 바늘구멍에 닿고 솜을 입구까지 가져갈 수 있게
            //  - 머리 구는 빠진 눈 자리 얼굴 표면보다 5mm 안쪽: 단추가 여기에 막혀 멈춘 자리가 눈 자리(1.5cm 허용) 안에 들고, 억지 밀어 넣기 깊이를 잰다
            //  - 등 쪽은 모델 등(z -0.084)에 맞춰 눕혔을 때 매트에 묻히지 않게
            var bodyGo = new GameObject("콜라이더 - 몸통");
            bodyGo.transform.SetParent(bearT, false);
            var bodyCol = bodyGo.AddComponent<BoxCollider>();
            bodyCol.center = new Vector3(0f, 0.075f, -0.0225f);
            bodyCol.size = new Vector3(0.18f, 0.15f, 0.115f);
            var headGo = new GameObject("콜라이더 - 머리");
            headGo.transform.SetParent(bearT, false);
            var headCol = headGo.AddComponent<SphereCollider>();
            headCol.center = new Vector3(0f, 0.218f, -0.022f);
            headCol.radius = 0.065f;

            // 빈 눈 자리 (INP-04 맞추기, 1.5cm·기울기 ±20°). 자리의 위축 = 얼굴 바깥쪽
            var eyeRot = Quaternion.FromToRotation(Vector3.up, BearEyeNormal);
            var eyeRoot = new GameObject("눈 자리 표시").transform;
            eyeRoot.SetParent(bearT, false);
            eyeRoot.localPosition = BearEyePos + BearEyeNormal * 0.0015f;
            eyeRoot.localRotation = eyeRot;
            var emptyEye = NoCol(Child(PrimitiveType.Cylinder, "빈 눈 자국", eyeRoot, Vector3.zero, Quaternion.identity, new Vector3(0.024f, 0.001f, 0.024f), Mat("빈 눈", new Color(0.35f, 0.25f, 0.18f))));
            var eye = new GameObject("눈 단추 자리 (4단계)").AddComponent<FitSocket>();
            eye.transform.SetParent(bearT, false);
            eye.transform.localPosition = BearEyePos + BearEyeNormal * 0.004f; // 단추(두께 1.5cm) 가운데가 얼굴 위 4mm
            eye.transform.localRotation = eyeRot;
            eye.acceptKey = "button";
            // 11/25 실행 확인 '단추가 생각보다 잘 안 붙음' → 허용 범위 넓힘 (이전: 위치 1.5cm, 기울기 20°, 한쪽 면만, 감지 5cm).
            // 레벨 명세 5장 값과 달라지므로 명세도 고칠 것. 플레이테스트(5주차 3행)에서 다시 조정
            eye.positionTolerance = 0.025f;
            eye.angleMode = AngleMode.Tilt;
            eye.angleTolerance = 35f;
            eye.tiltTwoSided = true;
            eye.detectRadius = 0.07f;
            eye.itemId = "bear";
            eye.step = 4;
            eye.forceJudge = true;
            eye.push = new PushForceJudge { warnDepth = 0.01f, damageDepth = 0.02f, warnTime = 0.5f, damageTime = 1f };
            eye.active = false;

            // 배: 찢어진 모양은 모델에 있다. 그 위에 손상 A(더 벌어짐), 솜(납작→둥글게), 바늘구멍, 실을 얹는다
            var tearFace = Quaternion.LookRotation(-BearTearNormal, Vector3.up); // Quad 앞면이 배 바깥을 본다
            var tearL = NoCol(Child(PrimitiveType.Quad, "찢어진 곳 - 더 벌어짐 (손상 A)", bearT, BearTearPos + BearTearNormal * 0.002f, tearFace, new Vector3(0.055f, 0.09f, 1f), tearBig));
            var fill = NoCol(Child(PrimitiveType.Sphere, "솜 채움", bearT, BearTearPos - BearTearNormal * 0.006f, Quaternion.LookRotation(BearTearNormal, Vector3.up), new Vector3(0.03f, 0.06f, 0.006f), cotton));
            var holes = new Transform[4];
            for (int i = 0; i < 4; i++)
                holes[i] = NoCol(Child(PrimitiveType.Sphere, $"바늘구멍 {i + 1}", bearT, BearHolePos[i], Quaternion.identity, Vector3.one * 0.007f, dark)).transform;
            var threads = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = BearHolePos[i];
                Vector3 b = i < 3 ? BearHolePos[i + 1] : BearHolePos[i] + new Vector3(-0.02f, -0.01f, 0.002f);
                Vector3 mid = (a + b) / 2f + BearTearNormal * 0.001f;
                float len = Vector3.Distance(a, b);
                var rot = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                threads[i] = NoCol(Child(PrimitiveType.Cylinder, $"실 {i + 1}", bearT, mid, rot, new Vector3(0.0025f, len / 2f, 0.0025f), thread));
            }

            // 솜 넣는 영역 (3단계): 찢어진 곳 둘레
            var bellyZone = new GameObject("솜 넣는 자리 (3단계)").AddComponent<InsertZone>();
            bellyZone.transform.SetParent(bearT, false);
            bellyZone.transform.localPosition = BearTearPos;
            bellyZone.halfExtents = new Vector3(0.045f, 0.05f, 0.04f);
            bellyZone.required = 3;
            bellyZone.itemId = "bear";
            bellyZone.step = 3;
            bellyZone.active = false;

            // ---- 부품: 단추·쪽지 (곰 인형의 자식이 아니라 형제로 둔다: XRI 잡기 콜라이더가 섞이지 않게) ----
            // 모델 안에 놓인 자리를 그대로 '배 속 자리'로 쓴다. 부품의 위(Y)축 = 납작한 면의 바깥 방향
            Transform buttonOrigin, noteOrigin;
            var button = AdoptPart(buttonMesh, "눈 단추", "배 속 단추 자리", set, bearT, BearButtonPosFallback, Vector3.forward, "button", 0.02f, out buttonOrigin);
            GameObject buttonVis;
            if (buttonMesh != null) { buttonVis = buttonMesh.gameObject; Paint(buttonVis, buttonMat); }
            else buttonVis = NoCol(Child(PrimitiveType.Cylinder, "단추", button, Vector3.zero, Quaternion.identity, new Vector3(0.032f, 0.0075f, 0.032f), buttonMat));
            var bb = BoundsIn(button, buttonVis);
            AddBox(button, Vector3.Max(bb.size, new Vector3(0.03f, 0.015f, 0.03f))).center = bb.center;
            var crack = NoCol(Child(PrimitiveType.Cube, "금 (손상 B)", button, new Vector3(bb.center.x, bb.max.y + 0.0003f, bb.center.z), Quaternion.Euler(0f, 35f, 0f), new Vector3(bb.size.x * 0.95f, 0.0008f, 0.003f), Mat("금", new Color(0.9f, 0.85f, 0.7f))));
            var buttonPull = button.gameObject.AddComponent<PullOutJudge>();
            buttonPull.origin = buttonOrigin;
            buttonPull.step = 2;
            buttonPull.ignoreWhileInside = new Collider[] { bodyCol, headCol };
            buttonPull.container = bearT.GetComponent<XRGrabInteractable>();

            var note = AdoptPart(noteMesh, "접힌 쪽지", "배 속 쪽지 자리", set, bearT, BearNotePosFallback, Vector3.right, "note", 0.01f, out noteOrigin);
            GameObject folded;
            if (noteMesh != null) { folded = noteMesh.gameObject; Paint(folded, paper); }
            else folded = NoCol(Child(PrimitiveType.Cube, "쪽지 (접힘)", note, Vector3.zero, Quaternion.identity, new Vector3(0.06f, 0.006f, 0.06f), paper));
            var nb = BoundsIn(note, folded);
            AddBox(note, Vector3.Max(nb.size, new Vector3(0.03f, NoteColliderThickness, 0.03f))).center = nb.center;
            // 얇고 가벼운 종이(10g)는 빠르게 놓으면 작업대에 파고든다 (4주차 실행 확인, 11/19). 연속 충돌 판정으로 바꾼다
            note.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var notePull = note.gameObject.AddComponent<PullOutJudge>();
            notePull.origin = noteOrigin;
            notePull.step = 2;
            notePull.ignoreWhileInside = new Collider[] { bodyCol, headCol };
            notePull.container = bearT.GetComponent<XRGrabInteractable>();
            if (torn != null) // 단추·쪽지를 꺼내 간 뒤 남은 빈 부모 정리는 하지 않는다 (몸이 그 아래에 있다)
                Debug.Log($"[씬 만들기] 곰 인형 모델 사용: 단추 {(buttonMesh != null ? "모델" : "도형")}, 쪽지 {(noteMesh != null ? "모델" : "도형")}");

            // 펼친 쪽지 (EP1-02, UI-04): 두 쪽. 왼쪽은 고정, 오른쪽은 가운데 접힌 선을 경첩으로 180° 열린다
            // 원래 9×7cm(손글씨 텍스처 비율)를 접힌 쪽지 크기에 맞춰 같은 비율로 키운다
            float k = Mathf.Clamp(Mathf.Max(nb.size.x, nb.size.z) / 0.07f, 0.8f, 1.6f);
            float halfW = 0.045f * k, pageH = 0.07f * k;
            var noteTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HandDir}/Note_N01.png");
            // 텍스처가 없으면 글씨 없는 종이로 만들어진다. 11/17 씬이 이렇게 만들어져 쪽지 글씨가 안 보였음 (11/24 확인) → 콘솔에 분명히 알린다
            if (noteTex == null) Debug.LogWarning($"[씬 만들기] {HandDir}/Note_N01.png를 못 찾음 → 쪽지가 글씨 없는 종이로 만들어짐. Git LFS로 텍스처를 받은 뒤 다시 실행");
            var unfolded = new GameObject("쪽지 (펼침, N01 손글씨)");
            unfolded.transform.SetParent(note, false);
            unfolded.transform.localPosition = nb.center;
            Material leftMat = noteTex != null ? TexturedMat("쪽지 N01 왼쪽", noteTex, new Vector2(0.5f, 1f), Vector2.zero) : paper;
            Material rightMat = noteTex != null ? TexturedMat("쪽지 N01 오른쪽", noteTex, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f)) : paper;
            // 종이 두께 1mm 판 위아래에 손글씨를 붙인다. 손에 쥔 방향에 따라 뒷면을 보면 글씨가 없다고 느끼므로(11/19 실행 확인)
            // 뒷면에도 글씨를 넣는다: 쪽지를 세로축으로 뒤집으면 왼쪽·오른쪽 쪽이 바뀌므로 뒷면은 반대쪽 절반을 좌우 반대로 붙인다
            const float sheet = 0.001f, face = sheet / 2f + 0.0002f;
            var frontFace = Quaternion.Euler(90f, 0f, 0f);       // 앞면(위, +Y), 글자 오른쪽 = +X
            var backFace = Quaternion.Euler(-90f, 180f, 0f);     // 뒷면(아래, -Y), 글자 오른쪽 = -X (뒤집어 본 사람 기준)
            NoCol(Child(PrimitiveType.Cube, "왼쪽 쪽 종이", unfolded.transform, new Vector3(-halfW / 2f, 0f, 0f), Quaternion.identity, new Vector3(halfW, sheet, pageH), paper));
            NoCol(Child(PrimitiveType.Quad, "왼쪽 쪽", unfolded.transform, new Vector3(-halfW / 2f, face, 0f), frontFace, new Vector3(halfW, pageH, 1f), leftMat));
            NoCol(Child(PrimitiveType.Quad, "왼쪽 쪽 뒷면 (글씨 뒷절반)", unfolded.transform, new Vector3(-halfW / 2f, -face, 0f), backFace, new Vector3(halfW, pageH, 1f), rightMat));
            var hingeN = new GameObject("오른쪽 쪽 경첩").transform;
            hingeN.SetParent(unfolded.transform, false);
            hingeN.localPosition = new Vector3(0f, 0.0016f, 0f); // 닫혀 있을 때 왼쪽 쪽 아래로 접혀 들어가도 겹치지 않게
            NoCol(Child(PrimitiveType.Cube, "오른쪽 쪽 종이", hingeN, new Vector3(halfW / 2f, 0f, 0f), Quaternion.identity, new Vector3(halfW, sheet, pageH), paper));
            NoCol(Child(PrimitiveType.Quad, "오른쪽 쪽", hingeN, new Vector3(halfW / 2f, face, 0f), frontFace, new Vector3(halfW, pageH, 1f), rightMat));
            NoCol(Child(PrimitiveType.Quad, "오른쪽 쪽 뒷면 (글씨 앞절반)", hingeN, new Vector3(halfW / 2f, -face, 0f), backFace, new Vector3(halfW, pageH, 1f), leftMat));
            var unfold = note.gameObject.AddComponent<NoteUnfold>();
            unfold.folded = folded;
            unfold.unfoldedRoot = unfolded;
            unfold.flap = hingeN;
            unfold.unfoldedCollider = new Vector3(halfW * 2f, NoteColliderThickness, pageH);

            // 바늘꽂이와 실 꿴 바늘 (5단계). 바늘 끝이 구멍에 들어가야 한다
            Prim(PrimitiveType.Cube, "바늘꽂이", table, new Vector3(-0.33f, H + 0.035f, 0.62f), new Vector3(0.05f, 0.03f, 0.05f), Mat("바늘꽂이", new Color(0.7f, 0.2f, 0.25f)), true);
            var needle = NewGrabbable("바늘 (실 꿴 것)", table, new Vector3(-0.33f, H + 0.06f, 0.62f), Quaternion.Euler(0f, 0f, 90f), "needle", GrabCategory.Tool, 0.01f);
            Child(PrimitiveType.Cylinder, "바늘", needle, Vector3.zero, Quaternion.identity, new Vector3(0.004f, 0.045f, 0.004f), Mat("도구 쇠", new Color(0.72f, 0.74f, 0.78f)));
            Object.DestroyImmediate(needle.GetChild(0).GetComponent<Collider>());
            var cap = needle.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1; cap.radius = 0.012f; cap.height = 0.1f;
            var tip = new GameObject("바늘 끝").transform;
            tip.SetParent(needle, false);
            tip.localPosition = new Vector3(0f, 0.045f, 0f);
            var stitch = bearT.gameObject.AddComponent<NeedleStitch>();
            stitch.needle = needle.GetComponent<Grabbable>();
            stitch.tip = tip;
            stitch.holes = holes;

            // 솜 상자 (3단계). 솜 덩어리는 신주연 모델 SM_Bear_Stuffing_Interactive (지름 약 5cm)
            var boxRoot = new GameObject("솜 상자").transform;
            boxRoot.SetParent(table, false);
            var wood = Mat("트레이", new Color(0.36f, 0.27f, 0.18f));
            Prim(PrimitiveType.Cube, "솜 상자 바닥", boxRoot, new Vector3(-0.47f, H + 0.025f, 0.6f), new Vector3(0.09f, 0.01f, 0.09f), wood, true);
            foreach (var d in new[] { new Vector3(0.045f, 0f, 0f), new Vector3(-0.045f, 0f, 0f) })
                Prim(PrimitiveType.Cube, "솜 상자 벽", boxRoot, new Vector3(-0.47f, H + 0.045f, 0.6f) + d, new Vector3(0.008f, 0.04f, 0.09f), wood, true);
            foreach (var d in new[] { new Vector3(0f, 0f, 0.045f), new Vector3(0f, 0f, -0.045f) })
                Prim(PrimitiveType.Cube, "솜 상자 벽", boxRoot, new Vector3(-0.47f, H + 0.045f, 0.6f) + d, new Vector3(0.09f, 0.04f, 0.008f), wood, true);
            var cottonSpawn = new GameObject("솜 나오는 자리").transform;
            cottonSpawn.SetParent(boxRoot, false);
            cottonSpawn.position = new Vector3(-0.47f, H + 0.057f, 0.6f);
            var cottonT = NewGrabbable("솜 덩어리 (원본)", boxRoot, cottonSpawn.position, Quaternion.identity, "cotton", GrabCategory.Tool, 0.01f);
            var cottonModel = InstantiateModel(StuffingFbx);
            float cottonRadius = 0.0225f;
            if (cottonModel != null)
            {
                cottonModel.name = "솜 (SM_Bear_Stuffing_Interactive, 신주연)";
                CenterIn(cottonModel.transform, cottonT);
                Paint(cottonModel, cotton);
                foreach (var c in cottonModel.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var cb = BoundsIn(cottonT, cottonModel);
                cottonRadius = Mathf.Max(cb.extents.x, cb.extents.y, cb.extents.z);
            }
            else NoCol(Child(PrimitiveType.Sphere, "솜", cottonT, Vector3.zero, Quaternion.identity, Vector3.one * 0.045f, cotton));
            var sc = cottonT.gameObject.AddComponent<SphereCollider>();
            sc.radius = cottonRadius;
            var cbox = boxRoot.gameObject.AddComponent<CottonBox>();
            cbox.template = cottonT.GetComponent<Grabbable>();
            cbox.spawnPoint = cottonSpawn;

            // 부품 칸 - 오래된 필름 한 통 (셋째 밤, 프롤로그부터 보임)
            Prim(PrimitiveType.Cylinder, "오래된 필름 (셋째 밤)", table, new Vector3(-0.54f, H + 0.04f, 0.6f), new Vector3(0.03f, 0.02f, 0.03f), Mat("필름", new Color(0.15f, 0.15f, 0.15f)), false);

            // ---- 상태 표현·수선 진행 ----
            var view = bearT.gameObject.AddComponent<BearView>();
            view.tearSmall = null;           // 처음 찢어진 모양은 모델에 있다
            view.tearLarge = tearL;
            view.bellyFill = fill.transform;
            view.fillFlatScale = new Vector3(0.03f, 0.06f, 0.006f);
            view.fillFullScale = new Vector3(0.04f, 0.072f, 0.03f); // 솜이 찢어진 틈 밖으로 보일 만큼 부풀게 (교수님 피드백 3, 11/24)
            view.threads = threads;
            view.seamClosed = null;          // 완성 모습은 수선 후 모델
            view.modelBefore = before;
            view.modelAfter = after;
            // 흔들 때 들썩임: 모델이면 몸 전체를 살짝(4%), 회색 박스면 몸통 구를 조금 더
            view.bellyPatch = pulseTarget;
            view.pulseAmount = torn != null ? 0.04f : 0.1f;
            view.emptyEyeMark = emptyEye;
            view.eyeMarkRoot = eyeRoot;
            view.buttonCrack = crack;

            var shake = bearT.gameObject.AddComponent<ShakeDetector>();
            var repair = bearT.gameObject.AddComponent<BearRepair>();
            repair.bear = bearT.GetComponent<Grabbable>();
            repair.shake = shake;
            repair.buttonPull = buttonPull;
            repair.notePull = notePull;
            repair.belly = bellyZone;
            repair.eye = eye;
            repair.stitch = stitch;
            repair.view = view;
            // 수선 부위 반응 빛 (교수님 피드백 3): 지름 5cm 반투명 구. 맞는 도구를 쥐고 다가갈 때와 단계 완료 순간에만 보인다
            var spot = Prim(PrimitiveType.Sphere, "수선 부위 빛 (피드백 3)", bearT, bearT.TransformPoint(BearTearPos), Vector3.one * 0.05f, TransparentMat("수선 부위 빛", new Color(1f, 0.9f, 0.6f, 0.3f)), false);
            var spotR = spot.GetComponent<Renderer>();
            spotR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spotR.enabled = false;
            repair.spotGlow = spotR;
            return repair;
        }

        // 모델이 없을 때의 회색 박스 곰 인형 (모델과 같은 크기·자리). 앉은 자세, 배가 +z
        static GameObject BuildBearBlockout(Transform bearT, Material fur, Material dark)
        {
            var root = new GameObject("모습 - 회색 박스 (모델 없음)").transform;
            root.SetParent(bearT, false);
            NoCol(Child(PrimitiveType.Sphere, "몸통", root, new Vector3(0f, 0.075f, -0.01f), Quaternion.identity, new Vector3(0.15f, 0.15f, 0.125f), fur)); // 배 표면 ≈ 찢어진 곳·바늘구멍 높이
            NoCol(Child(PrimitiveType.Sphere, "머리", root, new Vector3(0f, 0.218f, -0.022f), Quaternion.identity, Vector3.one * 0.14f, fur)); // 눈 자리가 표면에 오게
            NoCol(Child(PrimitiveType.Sphere, "주둥이", root, new Vector3(0f, 0.195f, 0.055f), Quaternion.identity, new Vector3(0.05f, 0.04f, 0.035f), fur));
            foreach (var x in new[] { -1f, 1f })
            {
                NoCol(Child(PrimitiveType.Sphere, "귀", root, new Vector3(x * 0.07f, 0.27f, -0.02f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.025f), fur));
                NoCol(Child(PrimitiveType.Sphere, "팔", root, new Vector3(x * 0.095f, 0.1f, 0.01f), Quaternion.Euler(0f, 0f, x * 30f), new Vector3(0.045f, 0.09f, 0.045f), fur));
                NoCol(Child(PrimitiveType.Sphere, "다리", root, new Vector3(x * 0.06f, 0.035f, 0.045f), Quaternion.identity, new Vector3(0.065f, 0.06f, 0.08f), fur));
            }
            var otherN = new Vector3(-BearEyeNormal.x, BearEyeNormal.y, BearEyeNormal.z);
            NoCol(Child(PrimitiveType.Cylinder, "남은 눈", root, BearOtherEyePos + otherN * 0.0015f, Quaternion.FromToRotation(Vector3.up, otherN), new Vector3(0.022f, 0.003f, 0.022f), dark));
            NoCol(Child(PrimitiveType.Quad, "찢어진 곳", root, BearTearPos + BearTearNormal * 0.002f, Quaternion.LookRotation(-BearTearNormal, Vector3.up), new Vector3(0.035f, 0.07f, 1f), Mat("찢어진 곳", new Color(0.25f, 0.12f, 0.08f))));
            return root.gameObject;
        }

        // 모델 안에 놓인 부품을 잡을 수 있는 부품으로 만든다.
        //  - 부품(잡기 대상)의 위치 = 메시 가운데, 위(Y)축 = 메시가 가장 얇은 축 중 bearOutward에 가까운 쪽
        //  - 같은 자세로 곰 인형 안에 '배 속 자리'(PullOutJudge.origin)를 만든다
        static Transform AdoptPart(Transform mesh, string name, string originName, Transform set, Transform bearT, Vector3 fallbackLocalPos, Vector3 bearOutward, string key, float mass, out Transform origin)
        {
            origin = new GameObject(originName).transform;
            origin.SetParent(bearT, false);
            if (mesh == null)
            {
                origin.localPosition = fallbackLocalPos;
                origin.localRotation = Quaternion.FromToRotation(Vector3.up, bearOutward);
            }
            else
            {
                var mf = mesh.GetComponentInChildren<MeshFilter>();
                Vector3 size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;
                Vector3 thin = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
                Vector3 thinWorld = mesh.TransformDirection(thin);
                if (Vector3.Dot(thinWorld, bearT.TransformDirection(bearOutward)) < 0f) thinWorld = -thinWorld;
                var b = BoundsIn(bearT, mesh.gameObject);
                origin.position = bearT.TransformPoint(b.center);
                origin.rotation = Quaternion.FromToRotation(mesh.up, thinWorld) * mesh.rotation;
                // 위 회전은 메시의 위축을 얇은 축으로 돌린 자세. 메시는 원래 자세 그대로 자식으로 붙는다
            }
            var part = NewGrabbable(name, set, origin.position, origin.rotation, key, GrabCategory.Part, mass);
            if (mesh != null)
            {
                mesh.SetParent(part, true);
                foreach (var c in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            return part;
        }

        // ================= 3주차: 도깨비·기억 영상·밤 전환 =================

        static DokkaebiStandIn BuildDokkaebi(Transform env, AntiqueData data)
        {
            var root = new GameObject("도깨비 (임시 연출, NPC-01·02)");
            root.transform.SetParent(env, false);
            // 작업대 뒤 모서리 바로 뒤 (11/26 작업대 모델은 뒤 모서리 z 1.05 → 도깨비 몸 앞이 닿지 않게 z 1.24). 기억 영상 화면(z 1.30) 앞
            root.transform.position = new Vector3(0f, 0f, DokkaebiZ);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 플레이어(원점) 쪽을 본다
            var visual = new GameObject("모습 (11/20 신주연 모델·효과로 교체)").transform;
            visual.SetParent(root.transform, false);

            GameObject model = null;
            var fbx = useDokkaebiModel ? AssetDatabase.LoadAssetAtPath<GameObject>(DokkaebiFbx) : null;
            if (fbx != null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.name = "도깨비 모델 (SK_Dokkaebi_Bear, 신주연 11/26)";
                model.transform.SetParent(visual, false);
                // 높이 1.6m, 발을 바닥에 맞춘다 (모델 축척·방향은 임포트 설정에 따라 다를 수 있어 눈으로 확인)
                var rends = model.GetComponentsInChildren<Renderer>();
                // 신주연 텍스처(T_Dokkaebi.jpg)를 입힌다. FBX 임베디드 재질은 텍스처 경로가 신주연 PC라 쓰지 않는다 (11/26). 텍스처가 없으면 회백색
                var baseMat = ModelMat("M_Dokkaebi (신주연 텍스처)", AssetDatabase.LoadAssetAtPath<Texture2D>(DokkaebiTex), new Color(0.72f, 0.7f, 0.66f), 0.1f);
                foreach (var r in rends)
                {
                    var shared = r.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++) shared[i] = baseMat;
                    r.sharedMaterials = shared;
                }
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    if (b.size.y > 0.01f)
                    {
                        float k = DokkaebiHeight / b.size.y;
                        model.transform.localScale *= k;
                        b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                        model.transform.position += new Vector3(root.transform.position.x - b.center.x, -b.min.y, root.transform.position.z - b.center.z);
                    }
                }
            }
            if (model == null)
            {
                Child(PrimitiveType.Capsule, "임시 기둥", visual, new Vector3(0f, 0.85f, 0f), Quaternion.identity, new Vector3(0.45f, 0.8f, 0.35f), Mat("도깨비_임시", new Color(0.35f, 0.55f, 0.45f)));
                NoCol(Child(PrimitiveType.Sphere, "단추 눈 (곰 인형 특징)", visual, new Vector3(0.06f, 1.45f, 0.17f), Quaternion.identity, Vector3.one * 0.05f, Mat("단추", new Color(0.18f, 0.1f, 0.06f))));
            }
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var puffPoint = new GameObject("연기 자리").transform;
            puffPoint.SetParent(root.transform, false);
            puffPoint.localPosition = new Vector3(0f, 1.1f, 0f);

            var puffGo = new GameObject("빛·연기 (임시, NPC-01·02)");
            puffGo.transform.SetParent(env, false);
            var puff = puffGo.AddComponent<PuffEffect>();
            puff.smokeMaterial = TransparentMat("연기", new Color(0.85f, 0.85f, 0.9f, 0.5f));
            var flash = new GameObject("등장 빛").AddComponent<Light>();
            flash.transform.SetParent(puffGo.transform, false);
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.9f, 0.7f);
            flash.range = 2.5f;
            flash.shadows = LightShadows.None;
            puff.flash = flash;

            var voice = root.AddComponent<AudioSource>();
            voice.clip = data != null ? data.dokkaebiVoice : null;
            voice.volume = 0.5f;
            voice.minDistance = 0.5f;
            voice.maxDistance = 6f;
            voice.rolloffMode = AudioRolloffMode.Linear;

            var d = root.AddComponent<DokkaebiStandIn>();
            d.visual = visual;
            d.puff = puff;
            d.puffPoint = puffPoint;
            d.voice = voice;
            return d;
        }

        // 기억 영상 화면 (MEM-05): 정면 작업대 너머, 플레이어에게서 약 1.3m(정면 출입문·벽 10cm 앞), 가로 1.6m
        static MemoryVideoPlayer BuildMemoryScreen()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "기억 영상 화면 (MEM-05·06·07)";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(heightRoot, false);
            go.transform.position = new Vector3(0f, H + 0.55f, 1.30f); // 정면 출입문 앞면(z 1.40)과 같은 평면이면 깜박이므로 10cm 앞
            go.transform.localScale = new Vector3(1.6f, 0.9f, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = EnsureShaderMat("기억 영상 화면", ShaderScreen);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            var m = go.AddComponent<MemoryVideoPlayer>();
            m.screen = r;
            m.player = vp;
            return m;
        }

        // 밤 전환 글자: 암전 화면 위에 그린다 (TMP Distance Field Overlay)
        static TextMeshPro BuildNightLabel(Transform parent)
        {
            var go = new GameObject("밤 전환 글자 (암전 위)");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null)
            {
                tmp.font = font;
                var overlay = EnsureOverlayFontMat();
                if (overlay != null) tmp.fontSharedMaterial = overlay;
            }
            tmp.text = "둘째 밤";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontSize = 0.12f * 10f / 0.72f; // 2m 앞 글자 높이 약 12cm
            tmp.color = new Color(0.95f, 0.92f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            go.SetActive(false);
            return tmp;
        }

        // 다른 물체 위에 그리는 글자 재질 (대사, 밤 전환 글자). 암전 판(Overlay 4000)보다 나중에 그린다
        static Material EnsureOverlayFontMat()
        {
            if (font == null) return null;
            string path = "Assets/Art/Fonts/NotoSansKR Overlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (overlay == null && shader != null && font.material != null)
            {
                overlay = new Material(font.material) { name = "NotoSansKR Overlay" };
                overlay.shader = shader;
                AssetDatabase.CreateAsset(overlay, path);
            }
            if (overlay != null) overlay.renderQueue = 4001;
            return overlay;
        }

        // 반환 뒤 다음 밤부터: 출입문 옆 사진·꽃 (PRC-05, 21-6). 비주얼은 신주연 담당으로 바꾼다
        static GameObject BuildReturnRemnants(Transform env)
        {
            var root = new GameObject("반환 흔적 - 출입문 옆 사진·꽃 (PRC-05, 임시)");
            root.transform.SetParent(env, false);
            Prim(PrimitiveType.Cube, "작은 사진 액자", root.transform, new Vector3(-0.15f, 1.35f, 1.42f), new Vector3(0.12f, 0.16f, 0.01f), Mat("액자", new Color(0.35f, 0.25f, 0.18f)), false);
            Prim(PrimitiveType.Quad, "사진 (아이와 곰 인형, 임시)", root.transform, new Vector3(-0.15f, 1.35f, 1.414f), new Vector3(0.09f, 0.12f, 1f), Mat("사진", new Color(0.85f, 0.75f, 0.55f)), false).transform.rotation = Quaternion.identity;
            Prim(PrimitiveType.Cylinder, "꽃병", root.transform, new Vector3(-0.15f, 0.12f, 1.3f), new Vector3(0.08f, 0.12f, 0.08f), Mat("꽃병", new Color(0.4f, 0.55f, 0.6f)), false);
            foreach (var p in new[] { new Vector3(-0.17f, 0.3f, 1.3f), new Vector3(-0.13f, 0.32f, 1.29f), new Vector3(-0.15f, 0.34f, 1.32f) })
                Prim(PrimitiveType.Sphere, "꽃", root.transform, p, Vector3.one * 0.04f, Mat("꽃", new Color(0.95f, 0.75f, 0.3f)), false);
            root.SetActive(false);
            return root;
        }

        // 에어팟 지연 확인용 빛 (SND-04)
        // 힌트 3단계 빛 점 (HNT-04): 지름 1.4cm 작은 구. 처리 자리의 물건 모양 빛 윤곽과 모양으로 구분한다 (12장)
        static GameObject BuildHintDot(Transform parent)
        {
            var go = Prim(PrimitiveType.Sphere, "힌트 빛 점 (HNT-04)", parent, Vector3.zero, Vector3.one * 0.014f, TransparentMat("힌트 빛 점", new Color(1f, 0.95f, 0.75f, 0.9f)), false);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        // 수선 장부 기록 카드 (교수님 피드백 11/24): 반환 선반 옆, 선반을 보는 플레이어 쪽을 향한다. 처리 뒤 기록이 남고 다음 밤에도 있다
        static TextMeshPro BuildLedger(Transform shelfZone)
        {
            var card = new GameObject("수선 장부 기록 (처리 뒤)").transform;
            card.SetParent(shelfZone.parent, false);
            float top = H - 0.15f;
            card.position = new Vector3(-0.17f, top + 0.09f, -0.66f);
            card.rotation = Quaternion.Euler(-15f, 180f, 0f); // 선반 앞(+z)에 선 플레이어가 읽게
            var paper = Child(PrimitiveType.Quad, "장부 종이", card, new Vector3(0f, 0f, 0.002f), Quaternion.identity, new Vector3(0.15f, 0.12f, 1f), Mat("메모 종이", new Color(0.92f, 0.88f, 0.78f)));
            NoCol(paper);
            var text = Text("장부 글자", card, Vector3.zero, "", 0.011f, new Color(0.18f, 0.12f, 0.08f, 0f));
            text.rectTransform.sizeDelta = new Vector2(0.14f, 0.11f);
            text.textWrappingMode = TextWrappingModes.Normal;
            card.gameObject.SetActive(false); // 처리 뒤 첫 기록 때 GameFlow가 켠다
            return text;
        }

        static Renderer BuildLatencyFlash()
        {
            var go = Prim(PrimitiveType.Sphere, "지연 확인 빛 (SND-04, 왼손 메뉴 버튼)", heightRoot, new Vector3(0f, H + 0.3f, 0.95f), Vector3.one * 0.04f, Mat("지연 확인 빛", new Color(1f, 1f, 1f)), false);
            var m = go.GetComponent<Renderer>().sharedMaterial;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * 2f);
            return go.GetComponent<Renderer>();
        }

        static void SetNonStatic(Transform t)
        {
            t.gameObject.isStatic = false;
            foreach (Transform c in t) SetNonStatic(c);
        }

        // ================= 3주차: 에셋 =================

        static Material EnsureShaderMat(string name, string shaderName)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError($"[씬 만들기] 셰이더 {shaderName}를 찾지 못함 (Assets/Shaders)"); return null; }
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material TexturedMat(string name, Texture2D tex, Vector2 scale, Vector2 offset)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.mainTextureScale = scale;
            m.mainTextureOffset = offset;
            m.SetColor("_BaseColor", Color.white);
            // 손글씨 종이 (UI-04): 스탠드가 도구함 바로 옆이라 반사광이 글씨를 덮어 안 읽힘 (11/19 실행 확인).
            // 매끄러움 0, 반사 하이라이트·환경 반사를 꺼서 빛을 받아도 무광 종이처럼 보이게 한다
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning($"[씬 만들기] 에셋 없음: {path}");
            return a;
        }

        // 영상: 투명 배경 webm은 알파를 살리고 변환하지 않는다 (MEM-06). 손글씨는 밉맵·클램프
        static void ConfigureMediaImporters()
        {
            foreach (var path in new[] { $"{VideoDir}/Memory_Bear_Placeholder_Alpha.webm", $"{VideoDir}/Memory_Bear_Placeholder.mp4", MemoryVideoEP01, AlphaTestVideo })
            {
                if (AssetImporter.GetAtPath(path) is VideoClipImporter vi)
                {
                    bool alpha = path.EndsWith(".webm");
                    var ts = vi.defaultTargetSettings;
                    bool changed = ts.enableTranscoding || (alpha && !vi.keepAlpha);
                    ts.enableTranscoding = false;
                    vi.defaultTargetSettings = ts;
                    if (alpha) vi.keepAlpha = true;
                    if (changed) vi.SaveAndReimport();
                }
            }
            foreach (var path in new[] { $"{HandDir}/Note_N01.png", $"{HandDir}/Memo_Grandma_Prologue.png" })
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && (ti.wrapMode != TextureWrapMode.Clamp || ti.anisoLevel < 4))
                {
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.anisoLevel = 4; // 비스듬히 볼 때 글씨가 덜 뭉개지게
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
            }
        }

        static SoundBank EnsureSoundBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank != null) return bank;
            bank = ScriptableObject.CreateInstance<SoundBank>();
            void Add(SfxKey k, string file, float vol = 0.8f, float spatial = 1f, float jitter = 0.03f)
            {
                bank.entries.Add(new SoundBank.Entry { key = k, clip = Load<AudioClip>($"{AudioDir}/{file}"), volume = vol, spatial = spatial, pitchJitter = jitter });
            }
            Add(SfxKey.Fit, "SFX_Fit.wav");
            Add(SfxKey.WrongCloth, "SFX_WrongCloth.wav");
            Add(SfxKey.WrongGlass, "SFX_WrongGlass.wav");
            Add(SfxKey.Creak, "SFX_Creak_Loop.wav", 0.7f, 1f, 0f);
            Add(SfxKey.Tear, "SFX_Tear.wav", 0.9f);
            Add(SfxKey.Thud, "SFX_Thud.wav", 0.9f);
            Add(SfxKey.Crack, "SFX_Crack.wav", 0.9f);
            Add(SfxKey.Extract, "SFX_Extract.wav", 0.6f);
            Add(SfxKey.StepDone, "SFX_StepDone.wav", 0.6f, 0.5f, 0f);
            Add(SfxKey.RepairDone, "SFX_RepairDone.wav", 0.8f, 0.5f, 0f);
            Add(SfxKey.DoorBell, "SFX_DoorBell.wav", 0.8f, 1f, 0f);
            Add(SfxKey.Appear, "SFX_Appear.wav", 0.7f, 1f, 0f);
            Add(SfxKey.Transform, "SFX_Transform.wav", 0.7f, 1f, 0f);
            Add(SfxKey.LampOn, "SFX_LampOn.wav", 0.6f, 1f, 0f);
            Add(SfxKey.Pen, "SFX_Pen.wav", 0.7f, 1f, 0f);
            Add(SfxKey.Place, "SFX_Place.wav", 0.8f);
            Add(SfxKey.WrongPlace, "SFX_WrongPlace.wav", 0.8f);
            Add(SfxKey.LatencyClick, "SFX_LatencyClick.wav", 0.9f, 0f, 0f); // 지연만 보려고 머리에서 들리게(2D)
            AssetDatabase.CreateAsset(bank, BankPath);
            return bank;
        }

        // 곰 인형 골동품 데이터 (SYS-04, 기획서 21-3 예). 손상 마스크 부위는 가안 (신주연 기억 연출 정의서 v0.2에서 확정)
        static AntiqueData EnsureAntiqueData()
        {
            var d = AssetDatabase.LoadAssetAtPath<AntiqueData>(AntiquePath);
            if (d != null)
            {
                UseEp01Video(d);
                FillPlaceholderSounds(d);
                return d;
            }
            d = ScriptableObject.CreateInstance<AntiqueData>();
            d.itemId = "bear";
            d.displayName = "곰 인형";
            d.night = 1;
            d.nightLabel = "첫째 밤";
            d.dokkaebiFeature = "단추 눈";
            d.requestLine = "D01";
            // 도깨비 고유음(NPC-04)은 비워 둔다. 자리표시 파일(Dokkaebi_Bear_Voice_Placeholder.wav)이 잡음이라 대사마다 지지직거림 (11/24 실행 확인).
            // 고유음 파일이 정해지면 Antique_01_Bear.asset의 Dokkaebi Voice 칸에 직접 넣는다
            d.dokkaebiVoice = null;
            d.steps = new[]
            {
                new RepairStepData { name = "살피기", action = RepairAction.Inspect, doneCondition = "흔들기 2회 (15m/s²)", discovery = true, hintLine = "H01", force = ForceKind.None },
                new RepairStepData { name = "꺼내기", action = RepairAction.ExtractInsert, doneCondition = "단추와 쪽지를 둘 다 배 밖 10cm", discovery = true, force = ForceKind.Pull,
                    damageShape = "찢어진 배가 더 벌어짐 (손상 A)", damageSound = SfxKey.Tear,
                    mask = new DamageMask { enabled = true, uv = new Rect(0.46f, 0.18f, 0.18f, 0.24f), start = 8f, end = 17f, strength = 1f, note = "④~⑤ 아이 품의 곰 인형 (가안)" } },
                new RepairStepData { name = "넣기", action = RepairAction.ExtractInsert, doneCondition = "솜 3번", force = ForceKind.Push,
                    damageShape = "솜이 뭉쳐 단추 자리가 틀어짐", damageSound = SfxKey.Thud,
                    mask = new DamageMask { enabled = true, uv = new Rect(0.5f, 0.38f, 0.16f, 0.22f), start = 8f, end = 13f, strength = 0.8f, note = "④ 잠든 아이 얼굴 쪽 (가안)" } },
                new RepairStepData { name = "맞추기", action = RepairAction.Fit, doneCondition = "단추를 눈 자리에 (1.5cm, ±20°)", force = ForceKind.Push,
                    damageShape = "단추에 금 (손상 B)", damageSound = SfxKey.Crack,
                    mask = new DamageMask { enabled = true, uv = new Rect(0.25f, 0.25f, 0.4f, 0.5f), start = 13f, end = 17f, strength = 1f, note = "⑤ 할머니가 아이를 안아 올리는 순간 (가안)" } },
                new RepairStepData { name = "맞추기 (순서)", action = RepairAction.Fit, doneCondition = "바늘구멍 4개 차례로 + 실 15cm", hintLine = "H02", force = ForceKind.None },
            };
            d.clueNote = "2단계 쪽지 \"조금 늦어도 꼭 데리러 갈게.\" (N01)";
            d.memoryLayers = new AudioClip[5];
            for (int i = 0; i < 5; i++) d.memoryLayers[i] = Load<AudioClip>($"{AudioDir}/Memory_Bear_Layer{i + 1}_Placeholder.wav");
            d.memoryLayersDamaged = new AudioClip[0];
            d.memoryVideo = Load<VideoClip>($"{VideoDir}/Memory_Bear_Placeholder.mp4");
            d.memoryVideoAlpha = Load<VideoClip>($"{VideoDir}/Memory_Bear_Placeholder_Alpha.webm");
            UseEp01Video(d);
            d.memoryAudio = Load<AudioClip>($"{AudioDir}/Memory_Bear_Full_Placeholder.wav");
            d.memoryLength = 20f;
            d.storyboard = new Texture2D[6];
            for (int i = 0; i < 6; i++) d.storyboard[i] = Load<Texture2D>($"{StoryboardDir}/Bear_Storyboard_{i + 1:00}_Placeholder.png");
            d.emotionColor = new Color(1f, 0.68f, 0.28f); // 황금빛·호박색 (14장). 세부 색값은 Unity 테스트에서
            d.reactionLine = "P01";
            d.processLine = "D02";
            d.process = ProcessKind.Return;
            d.spaceChange = "출입문 옆 사진·꽃, 밝아진 조명";
            d.fourthNightReplacement = "잠드는 장면 → 시계를 벗어둔 장면";
            d.fourthNightTrace = "찢어진 천의 결";
            d.journalNormal = "배 속의 단추를 찾아 다시 달아 주었다.";
            AssetDatabase.CreateAsset(d, AntiquePath);
            return d;
        }

        // 신주연 EP01 임시 기억 영상이 있으면 자리표시 영상 대신 쓴다. 투명 배경 자리표시 영상이 남아 있으면 그쪽이 먼저 재생되므로 비운다
        static void UseEp01Video(AntiqueData d)
        {
            var ep01 = AssetDatabase.LoadAssetAtPath<VideoClip>(MemoryVideoEP01);
            if (ep01 == null) return;
            bool changed = false;
            string curPath = d.memoryVideo != null ? AssetDatabase.GetAssetPath(d.memoryVideo) : null;
            if (d.memoryVideo == null || curPath.Contains("Placeholder") || curPath == MemoryVideoEP01Temp) { d.memoryVideo = ep01; changed = true; }
            if (d.memoryVideoAlpha != null && AssetDatabase.GetAssetPath(d.memoryVideoAlpha).Contains("Placeholder")) { d.memoryVideoAlpha = null; changed = true; }
            // MEM-06 확인용 투명 배경 영상: 켜면 빈 칸에 넣고, 끄면 확인용 영상일 때만 뺀다 (신주연 투명 영상을 넣어 두면 건드리지 않음)
            var alphaTest = AssetDatabase.LoadAssetAtPath<VideoClip>(AlphaTestVideo);
            if (useAlphaTestVideo && alphaTest != null && d.memoryVideoAlpha == null) { d.memoryVideoAlpha = alphaTest; changed = true; }
            if (!useAlphaTestVideo && d.memoryVideoAlpha != null && d.memoryVideoAlpha == alphaTest) { d.memoryVideoAlpha = null; changed = true; }
            if (changed)
            {
                d.memoryLength = (float)ep01.length;
                EditorUtility.SetDirty(d);
                Debug.Log($"[씬 만들기] 기억 영상 → {MemoryVideoEP01} ({ep01.length:0.0}초)");
            }
        }

        // 4주차 3·4행: 이미 있는 골동품 데이터의 빈 소리 칸을 자리표시 소리로 채운다 (MEM-01 겹 5개, MEM-03 영상 소리, 도깨비 목소리).
        // 빈 칸만 채우므로 신주연 소리를 다른 이름으로 연결해 두면 덮어쓰지 않는다. 같은 경로에 덮어쓰면 연결이 그대로 유지된다
        static void FillPlaceholderSounds(AntiqueData d)
        {
            bool changed = false;
            AudioClip Fill(AudioClip cur, string file)
            {
                if (cur != null) return cur;
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{file}");
                if (c != null) changed = true;
                return c;
            }
            if (d.memoryLayers == null || d.memoryLayers.Length < 5)
            {
                var old = d.memoryLayers ?? new AudioClip[0];
                d.memoryLayers = new AudioClip[5];
                System.Array.Copy(old, d.memoryLayers, old.Length);
                changed = true;
            }
            for (int i = 0; i < 5; i++) d.memoryLayers[i] = Fill(d.memoryLayers[i], $"Memory_Bear_Layer{i + 1}_Placeholder.wav");
            d.memoryAudio = Fill(d.memoryAudio, "Memory_Bear_Full_Placeholder.wav");
            // dokkaebiVoice는 채우지 않는다 (위 EnsureAntiqueData 주석, 11/24)
            if (changed)
            {
                EditorUtility.SetDirty(d);
                Debug.Log($"[씬 만들기] 골동품 데이터 빈 소리 칸을 {AudioDir}의 자리표시 소리로 채움");
            }
        }

        // ================= 도우미 =================

        static Transform NewGrabbable(string name, Transform parent, Vector3 pos, Quaternion rot, string key, GrabCategory cat, float mass)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var xr = go.AddComponent<XRGrabInteractable>();
            xr.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            xr.useDynamicAttach = true;
            var g = go.AddComponent<Grabbable>();
            g.key = key;
            g.category = cat;
            return go.transform;
        }

        static BoxCollider AddBox(Transform t, Vector3 size)
        {
            var b = t.gameObject.AddComponent<BoxCollider>();
            b.size = size;
            return b;
        }

        // ---- 모델 (신주연 FBX) ----

        // FBX를 씬에 꺼내 프리팹 연결을 푼다 (자식 부품을 다른 곳으로 옮길 수 있게). 메시는 FBX를 계속 가리키므로 모델을 고치면 따라 바뀐다
        static GameObject InstantiateModel(string path)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (go == null) return null;
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        // go의 메시들을 space 좌표로 감싼 상자 (꺼진 것 포함)
        static Bounds BoundsIn(Transform space, GameObject go)
        {
            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        // 곰 인형 모델을 곰 인형 기준점(발 아래 가운데)에 맞춘다. measure = 크기를 잴 몸 메시
        static void AlignToBear(Transform model, Transform bearT, GameObject measure)
        {
            model.SetParent(bearT, false);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.Euler(BearModelEuler) * model.localRotation; // 임포트가 준 회전은 살린다
            var b = BoundsIn(bearT, measure);
            model.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            if (Mathf.Abs(b.size.y - 0.30f) > 0.05f)
                Debug.LogWarning($"[씬 만들기] {model.name}: 높이 {b.size.y:0.000}m (모델 기준 0.30m). 임포트 축척·방향을 확인 (BearModelEuler)");
        }

        // 모델을 parent 가운데에 놓는다. longAxisTo를 주면 메시의 가장 긴 축을 parent의 그 방향으로 돌린다 (메시 가운데 = parent 원점)
        static void CenterIn(Transform model, Transform parent, Vector3? longAxisTo = null)
        {
            model.SetParent(parent, false);
            model.localPosition = Vector3.zero;
            var mf = model.GetComponentInChildren<MeshFilter>();
            if (longAxisTo.HasValue && mf != null && mf.sharedMesh != null)
            {
                Vector3 sz = mf.sharedMesh.bounds.size;
                Vector3 axis = sz.y >= sz.x && sz.y >= sz.z ? Vector3.up : sz.z >= sz.x ? Vector3.forward : Vector3.right;
                Vector3 dir = parent.InverseTransformDirection(mf.transform.TransformDirection(axis));
                model.localRotation = Quaternion.FromToRotation(dir, longAxisTo.Value) * model.localRotation;
            }
            var b = BoundsIn(parent, model.gameObject);
            model.localPosition -= b.center;
        }

        // 텍스처가 없는 재질만 색 재질로 바꾼다 (텍스처가 붙은 모델 재질은 그대로)
        static void Paint(GameObject go, Material m)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] == null || shared[i].mainTexture == null) shared[i] = m;
                r.sharedMaterials = shared;
            }
        }

        // 곰 인형 몸: 재질 칸 jjin = 천, Material.001 = 코 (SM_Bear_Torn·SM_Bear_Repaired 공통)
        static void PaintBearBody(GameObject go, Material fur, Material nose)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                bool named = false;
                foreach (var m in shared) if (m != null && m.name.Contains("Material.001")) named = true;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] != null && shared[i].mainTexture != null) continue;
                    bool isNose = (shared[i] != null && shared[i].name.Contains("Material.001")) || (!named && i > 0);
                    shared[i] = isNose ? nose : fur;
                }
                r.sharedMaterials = shared;
            }
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Vector3 scale, Material mat, bool keepCollider, bool worldScaleChild = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            if (worldScaleChild) go.transform.rotation = parent.rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = keepCollider && parent.GetComponentInParent<Rigidbody>() == null && !worldScaleChild;
            return go;
        }

        static GameObject Child(PrimitiveType type, string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject NoCol(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }

        static TextMeshPro Text(string name, Transform parent, Vector3 localPos, string text, float letterHeight, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = letterHeight * 10f / 0.72f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        static Material Mat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var m)) return m;
            string path = $"{MatDir}/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Material TransparentMat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var m)) return m;
            string path = $"{MatDir}/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static TMP_FontAsset EnsureFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
            if (ttf == null) { Debug.LogWarning($"[씬 만들기] 한글 글꼴이 없음: {FontTtf}. 대사가 □로 보일 수 있음"); return null; }
            var fa = TMP_FontAsset.CreateFontAsset(ttf);
            fa.name = "NotoSansKR-Regular SDF";
            AssetDatabase.CreateAsset(fa, FontAssetPath);
            if (fa.atlasTextures != null)
                foreach (var tex in fa.atlasTextures) { if (tex != null) { tex.name = "NotoSansKR Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); } }
            if (fa.material != null) { fa.material.name = "NotoSansKR Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[씬 만들기] 한글 TMP 글꼴 생성: {FontAssetPath} (동적 아틀라스)");
            return fa;
        }

        static DialogueTable EnsureDialogue()
        {
            var t = AssetDatabase.LoadAssetAtPath<DialogueTable>(DialoguePath);
            if (t != null) { AddFeedbackLines(t); return t; }
            t = ScriptableObject.CreateInstance<DialogueTable>();
            // 곰 인형 파트 대본 v0 (신주연, 2026-11-05). D01·D02는 수정 초안, 11/13 v1로 교체
            t.lines.Add(new DialogueLine { id = "D01", speaker = Speaker.Dokkaebi, text = "버려진 지 오래야. 그런데 이상하게… 기다리는 마음만은 낡질 않더라.", next = "대사 끝 → 곰 인형으로 변신" });
            t.lines.Add(new DialogueLine { id = "H01", speaker = Speaker.Dokkaebi, text = "배 속에서 뭔가 달그락거려…", next = "힌트 2단계 (4주차)" });
            t.lines.Add(new DialogueLine { id = "N01", speaker = Speaker.System, text = "조금 늦어도 꼭 데리러 갈게.", next = "쪽지 손글씨 (대사창 없음)" });
            t.lines.Add(new DialogueLine { id = "H02", speaker = Speaker.Dokkaebi, text = "한 땀씩… 차례대로.", next = "힌트 2단계 (4주차)" });
            t.lines.Add(new DialogueLine { id = "P01", speaker = Speaker.Protagonist, text = "누군가를 기다린다는 게 이런 느낌이었나?", next = "기억 종료 뒤" });
            t.lines.Add(new DialogueLine { id = "D02", speaker = Speaker.Dokkaebi, text = "……그 아이였구나. 할머니가 오실 때까지 나를 꼭 안고 있던 아이. 그 작은 품이 아직도 기억나. 나, 다시 그 아이에게 돌아가고 싶어.", next = "대사 끝 → 곰 인형으로 돌아감 → 반환 윤곽" });
            AddFeedbackLines(t);
            AssetDatabase.CreateAsset(t, DialoguePath);
            return t;
        }

        // 교수님 피드백(11/24)으로 추가한 대사. ★가안: 신주연 대본 v1에서 문장 확정. 이미 있는 줄은 덮어쓰지 않는다
        //  M01: 기억 영상 직전, 곰 인형이 1인칭으로 '내 기억'임을 알린다 (타인의 기억 → 바깥에서 바라봄)
        //  C01: 반환 선반에 놓은 뒤 주인공의 마무리 대사 (반환의 의미: 그 아이에게 돌아감)
        static void AddFeedbackLines(DialogueTable t)
        {
            bool added = false;
            void Add(string id, Speaker sp, string text, string next)
            {
                if (t.Find(id) != null) return;
                t.lines.Add(new DialogueLine { id = id, speaker = sp, text = text, next = next });
                added = true;
            }
            // M01 문장 변경(11/25): "내가 기억하는 그 아이야"는 누구의 기억인지 분명하지 않다는 실행 확인 → 기억 주인(그 아이)을 직접 말한다
            Add("M01", Speaker.Dokkaebi, "이건 그 아이의 기억이야. 나를 안고 할머니를 기다리던 아이.", "가안(11/25, 누구의 기억인지 도깨비가 직접 말함). 대사 끝 → 기억 영상");
            Add("C01", Speaker.Protagonist, "이제 그 아이에게 돌아가렴.", "가안(11/24). 반환 선반에 놓은 뒤 → 수선 장부 → 밤 전환");
            if (added) { EditorUtility.SetDirty(t); Debug.Log("[씬 만들기] 대사 표에 피드백 대사(M01·C01, 가안) 추가"); }
        }

        static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
