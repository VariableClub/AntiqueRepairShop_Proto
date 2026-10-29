using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class EyeSocket : MonoBehaviour
{
    public bool eyeFilled;
    XRSocketInteractor socket;

    void OnEnable()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectEntered.AddListener(OnSelectEntered);
    }

    void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnSelectEntered);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        eyeFilled = true;
        Debug.Log($"{args.interactableObject.transform.name} 눈 자리에 맞춤");
    }
}