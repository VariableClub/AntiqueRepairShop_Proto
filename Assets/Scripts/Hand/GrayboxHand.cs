using UnityEngine;

// 회색 박스용 손 모양. 기본 도형(손바닥 + 손가락 5개)으로 만들고
// grip 값(0~1)에 따라 손가락을 접는다. 콜라이더는 만들지 않는다 (잡기 판정에 끼어들지 않게).
public class GrayboxHand : MonoBehaviour
{
    public bool isLeft;
    public Color skinColor = new Color(0.93f, 0.78f, 0.66f);

    [Range(0f, 1f)] public float grip;
    public float curlSpeed = 12f;

    Transform[] fingerRoots;
    float currentGrip;

    // 손가락: 이름, 손바닥 기준 위치(x=좌우, z=앞), 길이, 굵기
    static readonly (string name, float x, float z, float len, float width)[] Fingers =
    {
        ("Index",  0.026f, 0.045f, 0.075f, 0.017f),
        ("Middle", 0.008f, 0.048f, 0.082f, 0.018f),
        ("Ring",  -0.010f, 0.045f, 0.075f, 0.017f),
        ("Pinky", -0.027f, 0.038f, 0.060f, 0.015f),
    };

    public static GrayboxHand Create(Transform parent, bool isLeft)
    {
        var root = new GameObject(isLeft ? "Graybox Hand L" : "Graybox Hand R");
        root.transform.SetParent(parent, false);
        // 컨트롤러를 쥔 자세: 손바닥이 몸 안쪽, 엄지가 위로 오도록 굴림
        root.transform.localRotation = Quaternion.Euler(0f, 0f, isLeft ? 90f : -90f);
        var hand = root.AddComponent<GrayboxHand>();
        hand.isLeft = isLeft;
        hand.Build();
        return hand;
    }

    void Build()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = skinColor;

        // 컨트롤러를 쥔 위치 기준으로 손바닥이 살짝 뒤·아래에 오도록
        var palm = Part("Palm", PrimitiveType.Cube, transform, mat);
        palm.localPosition = new Vector3(0f, -0.01f, 0f);
        palm.localScale = new Vector3(0.075f, 0.025f, 0.085f);

        // 손바닥이 아래를 보는 상태로 만들 때 오른손 엄지는 -X, 왼손 엄지는 +X 쪽
        float side = isLeft ? 1f : -1f;

        fingerRoots = new Transform[5];

        for (int i = 0; i < Fingers.Length; i++)
        {
            var f = Fingers[i];
            var pivot = new GameObject(f.name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(f.x * side, -0.01f, f.z);
            var seg = Part("Bone", PrimitiveType.Capsule, pivot, mat);
            seg.localRotation = Quaternion.Euler(90f, 0f, 0f);
            seg.localPosition = new Vector3(0f, 0f, f.len * 0.5f);
            seg.localScale = new Vector3(f.width, f.len * 0.5f, f.width);
            fingerRoots[i] = pivot;
        }

        // 엄지: 손바닥 옆에서 비스듬히
        var thumb = new GameObject("Thumb").transform;
        thumb.SetParent(transform, false);
        thumb.localPosition = new Vector3(0.035f * side, -0.005f, 0.0f);
        thumb.localRotation = Quaternion.Euler(0f, 40f * side, 0f);
        var tseg = Part("Bone", PrimitiveType.Capsule, thumb, mat);
        tseg.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tseg.localPosition = new Vector3(0f, 0f, 0.03f);
        tseg.localScale = new Vector3(0.02f, 0.03f, 0.02f);
        fingerRoots[4] = thumb;
    }

    static Transform Part(string name, PrimitiveType type, Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    void Update()
    {
        if (fingerRoots == null) return;
        currentGrip = Mathf.MoveTowards(currentGrip, grip, curlSpeed * Time.deltaTime);
        float side = isLeft ? 1f : -1f;

        for (int i = 0; i < 4; i++)
            fingerRoots[i].localRotation = Quaternion.Euler(Mathf.Lerp(0f, 95f, currentGrip), 0f, 0f);

        fingerRoots[4].localRotation = Quaternion.Euler(Mathf.Lerp(0f, 35f, currentGrip), Mathf.Lerp(40f, 10f, currentGrip) * side, 0f);
    }
}
