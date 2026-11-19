using CozyFocus.Core;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class SupabaseSessionConnector : MonoBehaviour
{
    private void Awake()
    {
        FocusSessionService.RegisterClientProvider(() =>
            SupabaseManager.Instance != null ? SupabaseManager.Instance.Client : null);
    }
}