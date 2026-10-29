using UnityEngine;

// 눈 자리. 공용 자리(PartSocket) 동작 + 눈이 채워졌는지 표시
public class EyeSocket : PartSocket
{
    public bool eyeFilled;

    protected override void OnFilled(Transform item)
    {
        eyeFilled = true;
        Debug.Log($"{item.name} 눈 자리에 맞춤");
    }

    protected override void OnEmptied(Transform item)
    {
        eyeFilled = false;
    }
}
