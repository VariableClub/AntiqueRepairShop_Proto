using System.Collections;
using UnityEngine;

namespace Dokkaebi
{
    // 검은 화면 (앱 시작 SYS-01·EP0-01, 밤 전환 암전 SYS-06).
    // 카메라 앞 0.25m에 검은 판을 붙여 다른 모든 것 위에 그린다 (셰이더 Dokkaebi/ScreenFade: ZTest Always, Overlay).
    // 화면에 고정된 UI를 두지 않는 원칙(18장)과 별개로, 암전 그 자체를 만드는 장치다.
    public class ScreenFader : MonoBehaviour
    {
        public Material fadeMaterial;
        public float distance = 0.25f;

        Renderer quad;
        float alpha = 1f;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public float Alpha => alpha;

        void Start() { Attach(); Set(alpha); }

        void Attach()
        {
            var cam = Camera.main;
            if (cam == null || quad != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "암전 판 (ScreenFader)";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, distance);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(3f, 3f, 1f); // 시야를 넉넉히 덮는다
            quad = go.GetComponent<Renderer>();
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.receiveShadows = false;
            if (fadeMaterial != null) quad.sharedMaterial = new Material(fadeMaterial);
        }

        public void Set(float a)
        {
            alpha = Mathf.Clamp01(a);
            if (quad == null) Attach();
            if (quad == null) return;
            quad.enabled = alpha > 0.001f;
            if (quad.sharedMaterial != null) quad.sharedMaterial.SetColor(ColorId, new Color(0f, 0f, 0f, alpha));
        }

        public IEnumerator FadeTo(float to, float time)
        {
            float from = alpha;
            if (time <= 0f) { Set(to); yield break; }
            for (float t = 0f; t < time; t += Time.deltaTime) { Set(Mathf.Lerp(from, to, t / time)); yield return null; }
            Set(to);
        }
    }
}
