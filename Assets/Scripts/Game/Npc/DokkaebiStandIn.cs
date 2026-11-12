using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 도깨비 임시 연출 (NPC-01 등장, NPC-02 도깨비 ↔ 물건 변신, NPC-04 고유음 자리). 기획서 10장, 대본, 17장.
    //  - 등장: 출입문 종소리 뒤 빛·연기와 함께 작업대 너머에 나타난다 (1.5초)
    //  - 변신: 대사가 끝나면 빛·연기와 모델 교체로 물건이 되어 작업대에 놓인다 / 기억 뒤 다시 도깨비로 (1.5초)
    //  - 고유음: 대사 글자가 뜨는 동안 물건을 닮은 소리 (곰 인형은 천이 스치는 소리). 파일은 신주연(NPC-04)
    //  효과·모델·idle은 신주연 확정본(11/20, NPC-03은 11/27 Animator 연결)으로 바꾼다. 지금은 모델(없으면 기둥)을 키우고 줄인다.
    public class DokkaebiStandIn : MonoBehaviour
    {
        [Tooltip("도깨비 모습 (도깨비 베이스 모델 또는 임시 기둥)")]
        public Transform visual;
        public PuffEffect puff;
        [Tooltip("연기가 피어오르는 높이 (도깨비 가슴께)")]
        public Transform puffPoint;
        public AudioSource voice;
        public float appearTime = 1.5f;
        public float transformTime = 1.5f;

        Vector3 baseScale = Vector3.one;

        void Awake()
        {
            if (visual != null) baseScale = visual.localScale;
            if (voice != null) { voice.loop = true; voice.playOnAwake = false; voice.spatialBlend = 1f; }
        }

        Vector3 PuffAt => puffPoint != null ? puffPoint.position : transform.position + Vector3.up * 1.1f;

        public IEnumerator Appear()
        {
            AlphaLog.BeginSection("도깨비 등장");
            gameObject.SetActive(true);
            Sfx.Play(SfxKey.Appear, PuffAt);
            if (puff != null) puff.Play(PuffAt);
            yield return Scale(0f, 1f, appearTime);
            AlphaLog.EndSection();
            Debug.Log("[도깨비] 등장 (빛·연기, 임시 연출)");
        }

        // 도깨비 → 물건: 연기가 가장 짙을 때 물건이 나타난다
        public IEnumerator BecomeItem(GameObject item)
        {
            AlphaLog.BeginSection("도깨비 변신");
            Sfx.Play(SfxKey.Transform, PuffAt);
            if (puff != null) puff.Play(PuffAt);
            yield return Scale(1f, 0f, transformTime * 0.4f);
            if (item != null) item.SetActive(true);
            gameObject.SetActive(false);
            yield return new WaitForSeconds(transformTime * 0.6f);
            AlphaLog.EndSection();
            Debug.Log("[도깨비] 물건으로 변신 (임시 연출)");
        }

        // 물건 → 도깨비 (기억 뒤 다시 나타나기)
        public IEnumerator BecomeDokkaebi(GameObject item)
        {
            AlphaLog.BeginSection("도깨비 변신");
            Sfx.Play(SfxKey.Transform, PuffAt);
            if (puff != null) puff.Play(item != null ? item.transform.position : PuffAt);
            yield return new WaitForSeconds(transformTime * 0.3f);
            if (item != null) item.SetActive(false);
            gameObject.SetActive(true);
            yield return Scale(0f, 1f, transformTime * 0.7f);
            AlphaLog.EndSection();
            Debug.Log("[도깨비] 다시 도깨비 모습으로 (임시 연출)");
        }

        public void StartVoice() { if (voice != null && voice.clip != null && !voice.isPlaying) voice.Play(); }
        public void StopVoice() { if (voice != null) voice.Stop(); }

        IEnumerator Scale(float from, float to, float time)
        {
            if (visual == null) { yield return new WaitForSeconds(time); yield break; }
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(from, to, t / time);
                visual.localScale = baseScale * Mathf.Max(0.001f, k);
                yield return null;
            }
            visual.localScale = baseScale * Mathf.Max(0.001f, to);
        }
    }
}
