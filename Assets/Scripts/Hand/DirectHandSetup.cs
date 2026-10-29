using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// 레이저(원거리) 잡기를 끄고, 컨트롤러 대신 손 모양을 보여 주며
// 물건에 손을 가까이 가져가 직접 잡고 놓는 방식으로 바꾼다.
// XR Origin에 붙여 두면 실행 시 자동 적용된다.
[DefaultExecutionOrder(-50)]
public class DirectHandSetup : MonoBehaviour
{
    [Tooltip("원거리(레이저) 잡기 끄기")]
    public bool disableFarGrab = true;
    [Tooltip("컨트롤러 모델 숨기고 손 모양 표시")]
    public bool showHands = true;
    [Tooltip("이 이름이 들어간 오브젝트의 렌더러를 숨김 (컨트롤러 모델)")]
    public string[] controllerModelKeywords = { "Controller Visual", "UniversalController", "Controller Model" };
    [Tooltip("손 위치 보정 (컨트롤러 기준, m)")]
    public Vector3 handOffset = new Vector3(0f, -0.01f, -0.03f);

    readonly List<(NearFarInteractor interactor, GrayboxHand hand)> hands = new List<(NearFarInteractor, GrayboxHand)>();

    void Awake()
    {
        var interactors = GetComponentsInChildren<NearFarInteractor>(true);
        if (interactors.Length == 0)
            Debug.LogWarning("[손 잡기] NearFarInteractor를 찾지 못함");

        foreach (var interactor in interactors)
        {
            if (disableFarGrab) DisableFar(interactor);

            if (showHands)
            {
                Transform controller = interactor.transform.parent != null ? interactor.transform.parent : interactor.transform;
                bool isLeft = IsLeft(interactor);
                HideControllerModel(controller);
                var hand = GrayboxHand.Create(controller, isLeft);
                hand.transform.localPosition = handOffset;
                hands.Add((interactor, hand));
            }

            Debug.Log($"[손 잡기] {interactor.name} ({(IsLeft(interactor) ? "왼손" : "오른손")}) 직접 잡기로 설정");
        }
    }

    void DisableFar(NearFarInteractor interactor)
    {
        interactor.enableFarCasting = false;
        interactor.enableNearCasting = true;

        // 레이저 선·끝점 표시 끄기 (텔레포트 레이는 다른 오브젝트라 영향 없음)
        foreach (var lr in interactor.GetComponentsInChildren<LineRenderer>(true))
            lr.enabled = false;
        foreach (var mb in interactor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string n = mb.GetType().Name;
            if (n == "CurveVisualController" || n == "XRInteractorLineVisual" || n == "XRInteractorReticleVisual")
                mb.enabled = false;
        }
    }

    void HideControllerModel(Transform controller)
    {
        foreach (var r in controller.GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponentInParent<XRBaseInteractor>(true) != null) continue;
            if (r.GetComponentInParent<GrayboxHand>(true) != null) continue;
            if (HasKeywordInPath(r.transform, controller))
                r.enabled = false;
        }
    }

    bool HasKeywordInPath(Transform t, Transform stop)
    {
        for (var cur = t; cur != null && cur != stop; cur = cur.parent)
            foreach (var k in controllerModelKeywords)
                if (cur.name.Contains(k)) return true;
        return false;
    }

    static bool IsLeft(NearFarInteractor interactor)
    {
        if (interactor.handedness == InteractorHandedness.Left) return true;
        if (interactor.handedness == InteractorHandedness.Right) return false;
        for (var t = interactor.transform; t != null; t = t.parent)
            if (t.name.Contains("Left")) return true;
        return false;
    }

    void Update()
    {
        foreach (var (interactor, hand) in hands)
        {
            if (interactor == null || hand == null) continue;
            float g = 0f;
            if (interactor.selectInput != null) g = interactor.selectInput.ReadValue();
            if (interactor.hasSelection) g = 1f;
            hand.grip = g;
        }
    }
}
