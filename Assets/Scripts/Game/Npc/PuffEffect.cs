using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 빛·연기 (NPC-01 등장, NPC-02 변신의 임시 연출). 신주연 효과 확정본(11/20)으로 바꾼다.
    // 파티클 설정에 기대지 않으려고 반투명 공 몇 개가 퍼지며 사라지게 만들었다 (회색 박스용).
    public class PuffEffect : MonoBehaviour
    {
        public Material smokeMaterial;
        public int puffs = 10;
        public float radius = 0.35f;
        public float duration = 0.9f;
        public Color smokeColor = new Color(0.85f, 0.85f, 0.9f, 0.55f);
        [Header("빛")]
        public Light flash;
        public float flashIntensity = 3f;

        Transform[] balls;
        Renderer[] rends;
        Vector3[] dirs;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            balls = new Transform[puffs];
            rends = new Renderer[puffs];
            dirs = new Vector3[puffs];
            for (int i = 0; i < puffs; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"연기 {i + 1}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                rends[i] = go.GetComponent<Renderer>();
                if (smokeMaterial != null) rends[i].sharedMaterial = smokeMaterial;
                rends[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                balls[i] = go.transform;
                go.SetActive(false);
            }
            if (flash != null) { flash.intensity = 0f; flash.enabled = false; }
        }

        public void Play(Vector3 at)
        {
            transform.position = at;
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            for (int i = 0; i < puffs; i++)
            {
                dirs[i] = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(0.5f, 1f);
                balls[i].localPosition = Random.insideUnitSphere * 0.05f;
                balls[i].gameObject.SetActive(true);
            }
            if (flash != null) flash.enabled = true;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                for (int i = 0; i < puffs; i++)
                {
                    balls[i].localPosition = dirs[i] * radius * Mathf.Sqrt(k);
                    balls[i].localScale = Vector3.one * Mathf.Lerp(0.08f, 0.3f, k);
                    var c = smokeColor;
                    c.a *= 1f - k;
                    block.SetColor(BaseColorId, c);
                    rends[i].SetPropertyBlock(block);
                }
                if (flash != null) flash.intensity = flashIntensity * Mathf.Sin(Mathf.Clamp01(k * 2f) * Mathf.PI);
                yield return null;
            }
            for (int i = 0; i < puffs; i++) balls[i].gameObject.SetActive(false);
            if (flash != null) { flash.intensity = 0f; flash.enabled = false; }
        }
    }
}
