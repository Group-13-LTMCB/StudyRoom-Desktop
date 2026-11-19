using CozyFocus.Core;
using UnityEngine;

public class ChairInteractable : MonoBehaviour
{
    [Header("Điểm ngồi trên ghế")]
    [SerializeField] private Transform seatPoint;

    public Transform SeatPoint => seatPoint != null ? seatPoint : transform;

    public void Interact()
    {
        if (FocusSessionManager.Instance != null && FocusSessionManager.Instance.IsSessionActive)
            return;

        var setupController = FindAnyObjectByType<SessionSetupController>();
        if (setupController != null)
        {
            setupController.Show();
        }
        else
        {
            Debug.LogWarning("[ChairInteractable] Không tìm thấy SessionSetupController trên UIRoot.");
        }
    }
}