using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Dokkaebi
{
    // 컨트롤러 진동. 패턴 표(SND-02)는 3주차에 만들고, 지금은 세기·길이만 정해 둔다.
    public static class Haptics
    {
        public const float FitAmp = 0.35f, FitDur = 0.08f;       // 맞는 조작
        public const float WrongAmp = 0.5f, WrongDur = 0.12f;    // 틀린 조작(튕김)
        public const float DamageAmp = 1f, DamageDur = 0.3f;     // 억지 손상
        public const float RattleAmp = 0.25f, RattleDur = 0.05f; // 흔들 때 달그락

        public static void Send(Transform interactor, float amplitude, float duration)
        {
            if (interactor == null) return;
            var player = interactor.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
        }
    }
}
