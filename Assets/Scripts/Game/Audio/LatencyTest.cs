using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dokkaebi
{
    // 에어팟 지연 확인 (SND-04). 기획서 20장: 블루투스는 소리가 늦게 들릴 수 있으므로 기억 영상 소리와 억지 경고 소리의 지연을 확인하고,
    // 거슬리면 3.5mm 유선 이어폰으로 바꾼다.
    //  - 왼손 메뉴 버튼(≡)을 누르면 1초 간격으로 10번: 클릭 소리 + 양손 진동 + 작업대 위 빛이 '같은 순간'에 난다
    //  - 에어팟과 유선(또는 헤드셋 스피커)으로 각각 해 보고, 빛·진동보다 소리가 늦게 느껴지는지 적는다
    //  - 더 정확히 재려면 휴대폰 슬로모션으로 헤드셋 렌즈와 소리를 함께 찍어 빛과 소리 사이 프레임 수를 센다
    //  게임 조작에 쓰지 않는 버튼이라 다른 조작과 겹치지 않는다 (진행자 초기화는 X·Y·A·B, SAV-06)
    public class LatencyTest : MonoBehaviour
    {
        public Renderer flashObject;
        public int pulses = 10;
        public float interval = 1f;
        public Transform leftHand, rightHand;

        InputAction toggle;
        bool running;

        void Awake()
        {
            toggle = new InputAction("에어팟 지연 확인", InputActionType.Button);
            toggle.AddBinding("<XRController>{LeftHand}/menu");
            toggle.AddBinding("<XRController>{LeftHand}/menuButton");
            toggle.AddBinding("<Keyboard>/l");
            if (flashObject != null) flashObject.enabled = false;
        }

        void OnEnable() { toggle.Enable(); toggle.performed += OnToggle; }
        void OnDisable() { toggle.performed -= OnToggle; toggle.Disable(); }
        void OnDestroy() { toggle.Dispose(); }

        void OnToggle(InputAction.CallbackContext _) { if (!running) StartCoroutine(Run()); }

        IEnumerator Run()
        {
            running = true;
            Debug.Log($"[지연 확인] 시작: 클릭 소리·진동·빛을 {interval}초 간격으로 {pulses}번. 소리 출력 장치({AudioSettings.driverCapabilities}, DSP 버퍼 {Buffer()})");
            Vector3 at = flashObject != null ? flashObject.transform.position : transform.position;
            for (int i = 0; i < pulses; i++)
            {
                Sfx.Play(SfxKey.LatencyClick, at);
                Haptics.Send(leftHand, 0.8f, 0.05f);
                Haptics.Send(rightHand, 0.8f, 0.05f);
                if (flashObject != null) flashObject.enabled = true;
                yield return new WaitForSeconds(0.08f);
                if (flashObject != null) flashObject.enabled = false;
                yield return new WaitForSeconds(interval - 0.08f);
            }
            Debug.Log("[지연 확인] 끝. 에어팟/유선 각각의 느낌을 SND-04 메모에 적는다");
            running = false;
        }

        static string Buffer()
        {
            AudioSettings.GetDSPBufferSize(out int len, out int num);
            return $"{len}×{num}, 출력 {AudioSettings.outputSampleRate}Hz";
        }
    }
}
