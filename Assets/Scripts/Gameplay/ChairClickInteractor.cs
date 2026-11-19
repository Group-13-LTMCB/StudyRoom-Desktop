using UnityEngine;
using UnityEngine.InputSystem;

public class ChairClickInteractor : MonoBehaviour
{
    [SerializeField] private LayerMask interactableLayers = ~0;
    [SerializeField] private float maxInteractDistance = 50f;

    private Camera _mainCamera;
    private SessionSetupController _setupController;

    private void Awake()
    {
        _mainCamera = GetComponent<Camera>();
        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }

    private void Start()
    {
        _setupController = FindAnyObjectByType<SessionSetupController>();
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (_setupController != null && _setupController.IsOpen)
            return;

        if (_mainCamera == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, interactableLayers))
        {
            var chair = hit.collider.GetComponentInParent<ChairInteractable>();
            if (chair != null)
            {
                chair.Interact();
            }
        }
    }
}