using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Dokkaebi
{
    // 진동 패턴 하나: 세기(0~1)·길이(초)·반복 횟수·간격
    [System.Serializable]
    public struct HapticPattern
    {
        public string name;
        public float amplitude;
        public float duration;
        public int pulses;
        public float gap;

        public HapticPattern(string name, float amplitude, float duration, int pulses = 1, float gap = 0f)
        {
            this.name = name; this.amplitude = amplitude; this.duration = duration; this.pulses = pulses; this.gap = gap;
        }
    }

    // 컨트롤러 진동 (SND-02 햅틱 패턴 표). 기획서 11장·18장 '같은 결과에는 같은 빛·진동도 함께 낸다'.
    // 표의 값은 모두 시작값(가안)이다. 11/20 플레이테스트에서 손맛을 보고 조정한다.
    //
    //  사건           | 패턴                              | 근거
    //  맞는 조작      | 0.35 × 0.08초, 1번                | 11장 '맑은 소리와 햅틱'
    //  틀린 조작      | 0.5 × 0.06초, 2번 (0.05초 간격)   | 11장 '튕김·둔탁한 소리'. 맞는 조작과 횟수로 구분
    //  억지 경고      | 0.15→0.8 (위험도에 비례) × 0.05초, 경고 동안 0.05초마다 | 11장 '진동이 점점 세짐'
    //  억지 손상      | 1.0 × 0.12초, 3번 (0.04초 간격)   | 11장 '금 가거나 찢어짐'. 가장 세고 길게
    //  흔들 때 달그락 | 0.25 × 0.05초, 1번                | 12장 살피기
    //  단계 완료      | 0.45 × 0.1초, 1번                 | 맞는 조작보다 조금 길게 (21-2 단계 완료)
    //  처리(올려 둠)  | 0.5 × 0.15초, 1번                 | 15장 처리
    //  잘못된 처리 자리 | 0.5 × 0.06초, 2번               | 틀린 조작과 같은 패턴 (15장 튕김)
    //  수선 완료(마지막 실) | 0.6 × 0.2초, 1번           | 13장 '숨 쉬듯 움직임'
    public static class Haptics
    {
        // 2주차 코드 호환용 (값은 아래 패턴과 같다)
        public const float FitAmp = 0.35f, FitDur = 0.08f;
        public const float WrongAmp = 0.5f, WrongDur = 0.06f;
        public const float DamageAmp = 1f, DamageDur = 0.12f;
        public const float RattleAmp = 0.25f, RattleDur = 0.05f;

        public static readonly HapticPattern Fit = new HapticPattern("맞는 조작", FitAmp, FitDur);
        public static readonly HapticPattern Wrong = new HapticPattern("틀린 조작", WrongAmp, WrongDur, 2, 0.05f);
        public static readonly HapticPattern Damage = new HapticPattern("억지 손상", DamageAmp, DamageDur, 3, 0.04f);
        public static readonly HapticPattern Rattle = new HapticPattern("달그락", RattleAmp, RattleDur);
        public static readonly HapticPattern StepDone = new HapticPattern("단계 완료", 0.45f, 0.1f);
        public static readonly HapticPattern Place = new HapticPattern("처리", 0.5f, 0.15f);
        public static readonly HapticPattern WrongPlace = new HapticPattern("잘못된 처리 자리", WrongAmp, WrongDur, 2, 0.05f);
        public static readonly HapticPattern RepairDone = new HapticPattern("수선 완료", 0.6f, 0.2f);

        public const float WarnMinAmp = 0.15f, WarnMaxAmp = 0.8f, WarnDur = 0.05f, WarnInterval = 0.05f;
        public static float WarnAmplitude(float ratio) { return Mathf.Lerp(WarnMinAmp, WarnMaxAmp, Mathf.Clamp01(ratio)); }

        public static void Send(Transform interactor, float amplitude, float duration)
        {
            if (interactor == null) return;
            var player = interactor.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
        }

        public static void Play(Transform interactor, HapticPattern p)
        {
            if (interactor == null) return;
            if (p.pulses <= 1) { Send(interactor, p.amplitude, p.duration); return; }
            Runner.Instance.StartCoroutine(Pulses(interactor, p));
        }

        static IEnumerator Pulses(Transform interactor, HapticPattern p)
        {
            for (int i = 0; i < p.pulses; i++)
            {
                if (interactor == null) yield break;
                Send(interactor, p.amplitude, p.duration);
                yield return new WaitForSeconds(p.duration + p.gap);
            }
        }

        // 여러 번 울리는 패턴을 돌릴 숨은 오브젝트
        class Runner : MonoBehaviour
        {
            static Runner instance;
            public static Runner Instance
            {
                get
                {
                    if (instance == null)
                    {
                        var go = new GameObject("[햅틱 패턴]") { hideFlags = HideFlags.HideInHierarchy };
                        Object.DontDestroyOnLoad(go);
                        instance = go.AddComponent<Runner>();
                    }
                    return instance;
                }
            }
        }
    }
}
