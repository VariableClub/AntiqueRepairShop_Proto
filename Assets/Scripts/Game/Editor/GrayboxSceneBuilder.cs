using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Dokkaebi.EditorTools
{
    // 2주차 회색 박스 씬을 만든다: 메뉴 「도깨비 > 2주차 회색 박스 씬 만들기」
    //  - 좌표: 레벨 명세 v0.2 (원점 = 플레이 공간 중앙 바닥, +z 정면, +x 오른쪽, m). 정면 출입문·뒤쪽 반환 선반은 기획서 16장(11/6)
    //  - 작업대 높이 H = 0.90m 고정 (자동 측정은 3주차)
    //  - 씬: Assets/Scenes/Game_Logic.unity (게임 로직 씬, 17장 작업 경계상 강민우 담당)
    public static class GrayboxSceneBuilder
    {
        const float H = 0.90f;
        const string ScenePath = "Assets/Scenes/Game_Logic.unity";
        const string MatDir = "Assets/Art/Material/Graybox";
        const string FontTtf = "Assets/Art/Fonts/NotoSansKR-Regular.ttf";
        const string FontAssetPath = "Assets/Art/Fonts/NotoSansKR-Regular SDF.asset";
        const string DialoguePath = "Assets/Data/Dialogue/Dialogue_EP01_Bear.asset";
        const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static TMP_FontAsset font;

        [MenuItem("도깨비/2주차 회색 박스 씬 만들기")]
        public static void Build()
        {
            // 지금 열린 씬은 건드리지 않는다: 새 씬을 추가로 열어 만들고, 저장한 뒤 닫는다
            mats.Clear();
            EnsureFolder(MatDir);
            EnsureFolder("Assets/Data/Dialogue");
            font = EnsureFont();
            var table = EnsureDialogue();

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
            BuildRoom(env);
            var table_ = BuildTable(env);
            BuildBlockouts(env);

            // ---- 프롤로그 ----
            var prologue = BuildPrologue(table_);

            // ---- 곰 인형 ----
            var bearSet = new GameObject("곰 인형 세트");
            var repair = BuildBear(bearSet.transform, table_, out var bearSpawn);

            // ---- 반환 선반 ----
            var shelf = BuildReturnShelf(env);

            // ---- 도깨비 자리 (임시) ----
            var dok = Prim(PrimitiveType.Capsule, "도깨비 자리 (임시 기둥, 3주차에 모델)", env, new Vector3(0f, 0.85f, 1.12f), new Vector3(0.45f, 0.8f, 0.35f), Mat("도깨비_임시", new Color(0.35f, 0.55f, 0.45f)), false);
            var talk = new GameObject("도깨비 대사 자리").transform;
            talk.SetParent(env, false);
            talk.position = new Vector3(0f, H + 0.45f, 1.0f);

            // ---- 흐름·대사 ----
            var systems = new GameObject("게임 흐름");
            var dialogue = new GameObject("대사 표시").AddComponent<DialogueDisplay>();
            dialogue.transform.SetParent(systems.transform, false);
            dialogue.table = table;
            dialogue.font = font;
            var flow = systems.AddComponent<GameFlow>();
            flow.tableHeight = H;
            flow.prologue = prologue;
            flow.bear = repair;
            flow.bearSet = bearSet;
            flow.returnShelf = shelf;
            flow.dialogue = dialogue;
            flow.dokkaebiStandIn = dok;
            flow.dokkaebiTalkAnchor = talk;
            flow.bearSpawn = bearSpawn;
            systems.AddComponent<DebugKeys>().flow = flow;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            if (previous.IsValid() && previous != scene)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[씬 만들기] {ScenePath} 저장 완료 (Project 창에서 열기). 플레이 후 N=다음, B=현재 단계 억지 손상, R=수선 기록");
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

        static Transform BuildTable(Transform env)
        {
            var wood = Mat("작업대", new Color(0.45f, 0.32f, 0.2f));
            var root = new GameObject("작업대 (H 0.90m)").transform;
            root.SetParent(env, false);
            Prim(PrimitiveType.Cube, "상판", root, new Vector3(0f, H - 0.02f, 0.6f), new Vector3(1.2f, 0.04f, 0.6f), wood, true);
            foreach (var x in new[] { -0.56f, 0.56f })
                foreach (var z in new[] { 0.34f, 0.86f })
                    Prim(PrimitiveType.Cube, "다리", root, new Vector3(x, (H - 0.04f) / 2f, z), new Vector3(0.05f, H - 0.04f, 0.05f), wood, true);
            Prim(PrimitiveType.Cube, "수선 매트", root, new Vector3(0f, H + 0.002f, 0.6f), new Vector3(0.5f, 0.004f, 0.4f), Mat("매트", new Color(0.2f, 0.35f, 0.3f)), false);
            Prim(PrimitiveType.Cube, "사진 자리 (셋째 밤)", root, new Vector3(0.5f, H + 0.002f, 0.8f), new Vector3(0.15f, 0.004f, 0.2f), Mat("표시", new Color(0.3f, 0.3f, 0.3f)), false);
            Prim(PrimitiveType.Cube, "탁상 달력", root, new Vector3(-0.42f, H + 0.075f, 0.86f), new Vector3(0.2f, 0.15f, 0.04f), Mat("종이", new Color(0.88f, 0.85f, 0.76f)), false);

            // 스탠드 (조명의 중심, 레벨 명세 7장). 점등 연출(EP0-01)은 3주차
            var metal = Mat("스탠드", new Color(0.2f, 0.2f, 0.22f));
            Prim(PrimitiveType.Cylinder, "스탠드 받침", root, new Vector3(-0.55f, H + 0.01f, 0.82f), new Vector3(0.12f, 0.01f, 0.12f), metal, false);
            Prim(PrimitiveType.Cylinder, "스탠드 기둥", root, new Vector3(-0.55f, H + 0.22f, 0.82f), new Vector3(0.015f, 0.21f, 0.015f), metal, false);
            Prim(PrimitiveType.Sphere, "스탠드 갓", root, new Vector3(-0.5f, H + 0.43f, 0.76f), new Vector3(0.12f, 0.07f, 0.12f), Mat("갓", new Color(0.85f, 0.7f, 0.4f)), false);
            var lamp = new GameObject("스탠드 빛 (기본색)").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = new Vector3(-0.45f, H + 0.38f, 0.7f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.82f, 0.6f);
            lamp.range = 2.2f;
            lamp.intensity = 2.5f;
            return root;
        }

        static void BuildBlockouts(Transform env)
        {
            var gray = Mat("블록아웃", new Color(0.5f, 0.5f, 0.52f));
            Prim(PrimitiveType.Cube, "할머니 서랍 (블록아웃)", env, new Vector3(-0.8f, H / 2f, 0.45f), new Vector3(0.4f, H, 0.4f), gray, true);
            var mem = new GameObject("기억 선반 (블록아웃, 격자 책장은 레벨 명세 v0.3)").transform;
            mem.SetParent(env, false);
            Prim(PrimitiveType.Cube, "틀", mem, new Vector3(0.9f, (H + 0.45f) / 2f, 0.4f), new Vector3(0.5f, H + 0.45f, 0.25f), gray, true);
            Prim(PrimitiveType.Cube, "봉인함 (블록아웃)", env, new Vector3(0.85f, (H - 0.1f) / 2f, -0.2f), new Vector3(0.4f, H - 0.1f, 0.3f), Mat("봉인함", new Color(0.3f, 0.25f, 0.22f)), true);
        }

        static PlaceZone BuildReturnShelf(Transform env)
        {
            var root = new GameObject("반환 선반 (뒤쪽 작은 창 앞)").transform;
            root.SetParent(env, false);
            float top = H - 0.15f;
            var wood = Mat("선반", new Color(0.42f, 0.3f, 0.2f));
            Prim(PrimitiveType.Cube, "선반 판", root, new Vector3(0f, top - 0.01f, -0.70f), new Vector3(0.5f, 0.02f, 0.25f), wood, true);
            Prim(PrimitiveType.Cube, "받침", root, new Vector3(0f, (top - 0.02f) / 2f, -0.70f), new Vector3(0.44f, top - 0.02f, 0.2f), wood, true);

            var zoneGo = new GameObject("반환 판정 (PRC-01)");
            zoneGo.transform.SetParent(root, false);
            zoneGo.transform.position = new Vector3(0f, top + 0.1f, -0.70f);
            var seat = new GameObject("앉는 자리").transform;
            seat.SetParent(zoneGo.transform, false);
            seat.position = new Vector3(0f, top, -0.70f);
            var zone = zoneGo.AddComponent<PlaceZone>();
            zone.seat = seat;
            var outline = Prim(PrimitiveType.Capsule, "곰 인형 빛 윤곽 (임시)", zoneGo.transform, new Vector3(0f, top + 0.15f, -0.70f), new Vector3(0.17f, 0.15f, 0.13f), TransparentMat("빛 윤곽", new Color(1f, 0.92f, 0.6f, 0.35f)), false);
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
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(slotX[i], H + 0.03f, 0.42f);
                Prim(PrimitiveType.Cube, $"홈 - {names[i]}", root, pos - new Vector3(0f, 0.0085f, 0f), footprint[i], groove, false);
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
                        NoCol(Child(PrimitiveType.Cylinder, "손잡이", t, new Vector3(0f, 0f, -0.03f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.025f, 0.035f, 0.025f), handle));
                        NoCol(Child(PrimitiveType.Cylinder, "축", t, new Vector3(0f, 0f, 0.035f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.006f, 0.035f, 0.006f), steel));
                        AddBox(t, new Vector3(0.03f, 0.03f, 0.14f));
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
            NoCol(Child(PrimitiveType.Quad, "종이", memoGo.transform, Vector3.zero, Quaternion.identity, new Vector3(0.18f, 0.09f, 1f), Mat("메모 종이", new Color(0.92f, 0.88f, 0.78f))));
            var memoText = Text("메모 글씨 (임시, UI-04 손글씨는 신채연)", memoGo.transform, new Vector3(0f, 0f, -0.001f), "물건은 망가져 제 모습을 잃어도,\n그 안에 담긴 기억까지\n사라지는 건 아니란다.", 0.012f, new Color(0.2f, 0.15f, 0.1f));
            memoText.rectTransform.sizeDelta = new Vector2(0.17f, 0.08f);
            var glow = NoCol(Child(PrimitiveType.Quad, "메모 은은한 빛 (10초 안 볼 때)", memoGo.transform, new Vector3(0f, 0f, 0.001f), Quaternion.identity, new Vector3(0.2f, 0.11f, 1f), TransparentMat("메모 빛", new Color(1f, 0.9f, 0.5f, 0.35f))));
            var gaze = memoGo.AddComponent<GazeTarget>();

            var pc = root.gameObject.AddComponent<PrologueController>();
            pc.toolSlots = slots;
            pc.lid = hinge;
            pc.lidOpenEuler = new Vector3(100f, 0f, 0f); // 앞쪽 가장자리가 위·뒤로 들린다
            pc.memo = gaze;
            pc.memoGlow = glow;
            return pc;
        }

        // ================= 곰 인형 =================

        static BearRepair BuildBear(Transform set, Transform table, out Transform spawn)
        {
            var fur = Mat("곰 인형 천", new Color(0.62f, 0.45f, 0.3f));
            var furLight = Mat("곰 인형 배", new Color(0.78f, 0.62f, 0.45f));
            var dark = Mat("어두운 표시", new Color(0.08f, 0.06f, 0.05f));
            var tearMat = Mat("찢어진 곳", new Color(0.25f, 0.12f, 0.08f));
            var tearBig = Mat("찢어진 곳 (손상 A)", new Color(0.5f, 0.08f, 0.06f));
            var cotton = Mat("솜", new Color(0.96f, 0.96f, 0.94f));
            var thread = Mat("실", new Color(0.85f, 0.2f, 0.2f));

            // 배가 위로 오게 눕힌다 (레벨 명세 4장). 기준점은 발 쪽 아래, 머리는 플레이어 쪽
            spawn = new GameObject("곰 인형 놓이는 자리 (매트)").transform;
            spawn.SetParent(table, false);
            spawn.SetPositionAndRotation(new Vector3(0f, H + 0.05f, 0.74f), Quaternion.Euler(-90f, 0f, 0f));

            var bearT = NewGrabbable("곰 인형 (EP1-01)", set, spawn.position, spawn.rotation, "bear", GrabCategory.Item, 1.5f);
            var body = bearT.GetComponent<Rigidbody>();
            body.linearDamping = 1f;
            body.angularDamping = 2f;
            // 아래 위치는 곰 인형을 세웠을 때 기준(로컬). 발 = 원점, +y 머리, +z 배
            var bodyGo = Child(PrimitiveType.Sphere, "몸통", bearT, new Vector3(0f, 0.1f, 0f), Quaternion.identity, new Vector3(0.16f, 0.18f, 0.09f), fur);
            Object.DestroyImmediate(bodyGo.GetComponent<Collider>());
            var bodyCol = bodyGo.AddComponent<BoxCollider>();
            bodyCol.size = new Vector3(0.9f, 0.9f, 1f);
            var head = Child(PrimitiveType.Sphere, "머리", bearT, new Vector3(0f, 0.25f, 0f), Quaternion.identity, Vector3.one * 0.13f, fur);
            head.GetComponent<Collider>().enabled = true;
            foreach (var x in new[] { -0.05f, 0.05f })
            {
                NoCol(Child(PrimitiveType.Sphere, "귀", bearT, new Vector3(x, 0.31f, 0f), Quaternion.identity, Vector3.one * 0.045f, fur));
                NoCol(Child(PrimitiveType.Sphere, "팔", bearT, new Vector3(x * 1.9f, 0.13f, 0.01f), Quaternion.identity, new Vector3(0.05f, 0.09f, 0.05f), fur));
                NoCol(Child(PrimitiveType.Sphere, "다리", bearT, new Vector3(x, 0.02f, 0.03f), Quaternion.identity, new Vector3(0.06f, 0.05f, 0.07f), fur));
            }
            NoCol(Child(PrimitiveType.Cylinder, "남은 눈", bearT, new Vector3(-0.03f, 0.265f, 0.062f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.022f, 0.003f, 0.022f), dark));

            // 빈 눈 자리 (INP-04 맞추기, 1.5cm·기울기 ±20°)
            var eyeRoot = new GameObject("눈 자리 표시").transform;
            eyeRoot.SetParent(bearT, false);
            eyeRoot.localPosition = new Vector3(0.03f, 0.265f, 0.063f);
            eyeRoot.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var emptyEye = NoCol(Child(PrimitiveType.Cylinder, "빈 눈 자국", eyeRoot, Vector3.zero, Quaternion.identity, new Vector3(0.026f, 0.001f, 0.026f), Mat("빈 눈", new Color(0.35f, 0.25f, 0.18f))));
            var eye = new GameObject("눈 단추 자리 (4단계)").AddComponent<FitSocket>();
            eye.transform.SetParent(bearT, false);
            eye.transform.localPosition = new Vector3(0.03f, 0.265f, 0.066f);
            eye.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 위축이 얼굴 바깥쪽
            eye.acceptKey = "button";
            eye.positionTolerance = 0.015f;
            eye.angleMode = AngleMode.Tilt;
            eye.angleTolerance = 20f;
            eye.detectRadius = 0.05f;
            eye.itemId = "bear";
            eye.step = 4;
            eye.forceJudge = true;
            eye.push = new PushForceJudge { warnDepth = 0.01f, damageDepth = 0.02f, warnTime = 0.5f, damageTime = 1f };
            eye.active = false;

            // 배: 천(들썩임), 솜(납작→둥글게), 찢어진 곳, 바늘구멍, 실, 봉합선
            var cloth = NoCol(Child(PrimitiveType.Cylinder, "배 천", bearT, new Vector3(0f, 0.1f, 0.043f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.13f, 0.003f, 0.13f), furLight));
            var fill = NoCol(Child(PrimitiveType.Sphere, "솜 채움", bearT, new Vector3(0f, 0.1f, 0.04f), Quaternion.identity, new Vector3(0.12f, 0.12f, 0.01f), furLight));
            var tearS = NoCol(Child(PrimitiveType.Quad, "찢어진 곳", bearT, new Vector3(0f, 0.1f, 0.067f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.06f, 0.08f, 1f), tearMat));
            var tearL = NoCol(Child(PrimitiveType.Quad, "찢어진 곳 - 더 벌어짐 (손상 A)", bearT, new Vector3(0f, 0.1f, 0.0675f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.09f, 0.11f, 1f), tearBig));
            Vector3[] holePos = { new Vector3(-0.03f, 0.13f, 0.069f), new Vector3(0.03f, 0.11f, 0.069f), new Vector3(-0.03f, 0.09f, 0.069f), new Vector3(0.03f, 0.07f, 0.069f) };
            var holes = new Transform[4];
            for (int i = 0; i < 4; i++)
                holes[i] = NoCol(Child(PrimitiveType.Sphere, $"바늘구멍 {i + 1}", bearT, holePos[i], Quaternion.identity, Vector3.one * 0.009f, dark)).transform;
            var threads = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = holePos[i];
                Vector3 b = i < 3 ? holePos[i + 1] : holePos[i] + new Vector3(-0.02f, -0.01f, 0f);
                Vector3 mid = (a + b) / 2f + new Vector3(0f, 0f, 0.001f);
                float len = Vector3.Distance(a, b);
                var rot = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                threads[i] = NoCol(Child(PrimitiveType.Cylinder, $"실 {i + 1}", bearT, mid, rot, new Vector3(0.003f, len / 2f, 0.003f), thread));
            }
            var seam = NoCol(Child(PrimitiveType.Quad, "봉합선 (완성)", bearT, new Vector3(0f, 0.1f, 0.0685f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.006f, 0.08f, 1f), dark));

            // 배 속 부품 자리 (단추·쪽지)
            var buttonOrigin = new GameObject("배 속 단추 자리").transform;
            buttonOrigin.SetParent(bearT, false);
            buttonOrigin.localPosition = new Vector3(-0.018f, 0.1f, 0.05f);
            buttonOrigin.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var noteOrigin = new GameObject("배 속 쪽지 자리").transform;
            noteOrigin.SetParent(bearT, false);
            noteOrigin.localPosition = new Vector3(0.02f, 0.1f, 0.05f);
            noteOrigin.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // 솜 넣는 영역 (3단계)
            var bellyZone = new GameObject("솜 넣는 자리 (3단계)").AddComponent<InsertZone>();
            bellyZone.transform.SetParent(bearT, false);
            bellyZone.transform.localPosition = new Vector3(0f, 0.1f, 0.065f);
            bellyZone.halfExtents = new Vector3(0.06f, 0.06f, 0.04f);
            bellyZone.required = 3;
            bellyZone.itemId = "bear";
            bellyZone.step = 3;
            bellyZone.active = false;

            // ---- 부품 (곰 인형의 자식이 아니라 형제로 둔다: XRI 잡기 콜라이더가 섞이지 않게) ----
            var button = NewGrabbable("눈 단추", set, buttonOrigin.position, buttonOrigin.rotation, "button", GrabCategory.Part, 0.02f);
            var buttonVis = Child(PrimitiveType.Cylinder, "단추", button, Vector3.zero, Quaternion.identity, new Vector3(0.035f, 0.0075f, 0.035f), Mat("단추", new Color(0.18f, 0.1f, 0.06f)));
            Object.DestroyImmediate(buttonVis.GetComponent<Collider>());
            AddBox(button, new Vector3(0.035f, 0.015f, 0.035f));
            var crack = NoCol(Child(PrimitiveType.Cube, "금 (손상 B)", button, new Vector3(0f, 0.0078f, 0f), Quaternion.Euler(0f, 35f, 0f), new Vector3(0.033f, 0.0008f, 0.003f), Mat("금", new Color(0.9f, 0.85f, 0.7f))));
            var buttonPull = button.gameObject.AddComponent<PullOutJudge>();
            buttonPull.origin = buttonOrigin;
            buttonPull.step = 2;
            buttonPull.ignoreWhileInside = new Collider[] { bodyCol };

            var note = NewGrabbable("접힌 쪽지", set, noteOrigin.position, noteOrigin.rotation, "note", GrabCategory.Part, 0.01f);
            Child(PrimitiveType.Cube, "쪽지", note, Vector3.zero, Quaternion.identity, new Vector3(0.045f, 0.006f, 0.035f), Mat("메모 종이", new Color(0.92f, 0.88f, 0.78f)));
            Object.DestroyImmediate(note.GetChild(0).GetComponent<Collider>());
            AddBox(note, new Vector3(0.045f, 0.012f, 0.035f));
            var noteText = Text("쪽지 글씨 N01 (임시, 펼침 EP1-02는 3주차)", note, new Vector3(0f, 0.0035f, 0f), "조금 늦어도\n꼭 데리러 갈게.", 0.006f, new Color(0.2f, 0.15f, 0.1f));
            noteText.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            noteText.rectTransform.sizeDelta = new Vector2(0.044f, 0.034f);
            var notePull = note.gameObject.AddComponent<PullOutJudge>();
            notePull.origin = noteOrigin;
            notePull.step = 2;
            notePull.ignoreWhileInside = new Collider[] { bodyCol };

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

            // 솜 상자 (3단계)
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
            cottonSpawn.position = new Vector3(-0.47f, H + 0.055f, 0.6f);
            var cottonT = NewGrabbable("솜 덩어리 (원본)", boxRoot, cottonSpawn.position, Quaternion.identity, "cotton", GrabCategory.Tool, 0.01f);
            var cv = Child(PrimitiveType.Sphere, "솜", cottonT, Vector3.zero, Quaternion.identity, Vector3.one * 0.045f, cotton);
            Object.DestroyImmediate(cv.GetComponent<Collider>());
            var sc = cottonT.gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.0225f;
            var cbox = boxRoot.gameObject.AddComponent<CottonBox>();
            cbox.template = cottonT.GetComponent<Grabbable>();
            cbox.spawnPoint = cottonSpawn;

            // 부품 칸 - 오래된 필름 한 통 (셋째 밤, 프롤로그부터 보임)
            Prim(PrimitiveType.Cylinder, "오래된 필름 (셋째 밤)", table, new Vector3(-0.54f, H + 0.04f, 0.6f), new Vector3(0.03f, 0.02f, 0.03f), Mat("필름", new Color(0.15f, 0.15f, 0.15f)), false);

            // ---- 상태 표현·수선 진행 ----
            var view = bearT.gameObject.AddComponent<BearView>();
            view.tearSmall = tearS;
            view.tearLarge = tearL;
            view.bellyFill = fill.transform;
            view.bellyPatch = cloth.transform;
            view.threads = threads;
            view.seamClosed = seam;
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
            return repair;
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

        static void AddBox(Transform t, Vector3 size)
        {
            var b = t.gameObject.AddComponent<BoxCollider>();
            b.size = size;
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
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<DialogueTable>();
            // 곰 인형 파트 대본 v0 (신주연, 2026-11-05). D01·D02는 수정 초안, 11/13 v1로 교체
            t.lines.Add(new DialogueLine { id = "D01", speaker = Speaker.Dokkaebi, text = "버려진 지 오래야. 그런데 이상하게… 기다리는 마음만은 낡질 않더라.", next = "대사 끝 → 곰 인형으로 변신" });
            t.lines.Add(new DialogueLine { id = "H01", speaker = Speaker.Dokkaebi, text = "배 속에서 뭔가 달그락거려…", next = "힌트 2단계 (4주차)" });
            t.lines.Add(new DialogueLine { id = "N01", speaker = Speaker.System, text = "조금 늦어도 꼭 데리러 갈게.", next = "쪽지 손글씨 (대사창 없음)" });
            t.lines.Add(new DialogueLine { id = "H02", speaker = Speaker.Dokkaebi, text = "한 땀씩… 차례대로.", next = "힌트 2단계 (4주차)" });
            t.lines.Add(new DialogueLine { id = "P01", speaker = Speaker.Protagonist, text = "누군가를 기다린다는 게 이런 느낌이었나?", next = "기억 종료 뒤" });
            t.lines.Add(new DialogueLine { id = "D02", speaker = Speaker.Dokkaebi, text = "……그 아이였구나. 할머니가 오실 때까지 나를 꼭 안고 있던 아이. 그 작은 품이 아직도 기억나. 나, 다시 그 아이에게 돌아가고 싶어.", next = "대사 끝 → 곰 인형으로 돌아감 → 반환 윤곽" });
            AssetDatabase.CreateAsset(t, DialoguePath);
            return t;
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
