using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;

namespace CampusTour
{
    public class TourInputController : MonoBehaviour
    {
        [Header("Rig & Camera Setup")]
        [SerializeField] private Transform rigTransform;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private Transform leftHandAnchor;

        [Header("Manager References")]
        [SerializeField] private CampusTourManager tourManager;
        [SerializeField] private CampusTourUI tourUI;
        [SerializeField] private PanoramaVideoPlayer videoPlayer;

        [Header("VR Turning")]
        [SerializeField] private bool snapTurn = false;
        [SerializeField] private float snapAngle = 45f;
        [SerializeField] private float smoothTurnSpeed = 100f;
        [SerializeField] private float stickThreshold = 0.5f;
        [SerializeField] private float stickDeadzone = 0.15f;

        [Header("Editor Mouse Look (New Input System)")]
        [SerializeField] private float mouseSensitivity = 0.15f;

        [Header("Ray Pointer")]
        [SerializeField] private LineRenderer rayLineRenderer;
        [SerializeField] private float rayDistance = 25f;
        [SerializeField] private LayerMask raycastMask = ~0;

        private float editorYaw = 0f;
        private float editorPitch = 0f;
        private bool isMouseDragging = false;

        private bool leftStickLatched = false;
        private bool rightStickLatched = false;

        // Button state latches to avoid rapid re-triggering
        private bool lastLeftX = false;
        private bool lastLeftY = false;
        private bool lastLeftMenu = false;
        private bool lastRightA = false;
        private bool lastRightB = false;
        private bool lastRightTrigger = false;
        private bool lastLeftTrigger = false;
        private bool lastLeftStickClick = false;
        private bool lastRightStickClick = false;

        private readonly List<XRInputDevice> leftControllers = new List<XRInputDevice>();
        private readonly List<XRInputDevice> rightControllers = new List<XRInputDevice>();
        private static readonly List<XRDisplaySubsystem> displaySubsystems = new List<XRDisplaySubsystem>();

        private void Awake()
        {
            if (tourManager == null) tourManager = FindFirstObjectByType<CampusTourManager>();
            if (tourUI == null) tourUI = FindFirstObjectByType<CampusTourUI>();
            if (videoPlayer == null) videoPlayer = FindFirstObjectByType<PanoramaVideoPlayer>();
            if (mainCamera == null) mainCamera = Camera.main;

            if (rigTransform == null)
            {
                if (transform.parent != null) rigTransform = transform.parent;
                else rigTransform = transform;
            }

            SetupRayVisualizer();
        }

        private void SetupRayVisualizer()
        {
            if (rayLineRenderer == null && rightHandAnchor != null)
            {
                rayLineRenderer = rightHandAnchor.GetComponent<LineRenderer>();
                if (rayLineRenderer == null)
                {
                    rayLineRenderer = rightHandAnchor.gameObject.AddComponent<LineRenderer>();
                    rayLineRenderer.startWidth = 0.005f;
                    rayLineRenderer.endWidth = 0.002f;
                    rayLineRenderer.positionCount = 2;
                    rayLineRenderer.useWorldSpace = true;

                    Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
                    lineMat.color = new Color(0.2f, 0.7f, 1f, 0.75f);
                    rayLineRenderer.material = lineMat;
                }
            }
        }

        private void Start()
        {
            if (mainCamera != null)
            {
                Vector3 rot = mainCamera.transform.localEulerAngles;
                editorYaw = rot.y;
                editorPitch = (rot.x > 180f) ? rot.x - 360f : rot.x;
            }
        }

        private void Update()
        {
            bool headTracked = TryUpdateHeadTracking();
            PollQuestControllers();
            UpdateVRRayPointer();

            if (!headTracked)
            {
                HandleEditorLook();
            }
            HandleEditorKeyboardShortcuts();
        }

        private void LateUpdate()
        {
            // Re-apply latest headset orientation right before rendering
            TryUpdateHeadTracking();
        }

        private bool TryUpdateHeadTracking()
        {
            if (mainCamera == null) return false;

            // 1. CenterEye via UnityEngine.XR.InputDevices (Meta Quest OpenXR)
            var centerEye = XRInputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (centerEye.isValid && centerEye.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRot))
            {
                mainCamera.transform.localRotation = headRot;
                if (centerEye.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPos))
                {
                    mainCamera.transform.localPosition = headPos;
                }
                return true;
            }

            // 2. Head fallback via UnityEngine.XR.InputDevices
            var head = XRInputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.isValid && head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headOnlyRot))
            {
                mainCamera.transform.localRotation = headOnlyRot;
                if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headOnlyPos))
                {
                    mainCamera.transform.localPosition = headOnlyPos;
                }
                return true;
            }

            // 3. New Input System TrackedDevice / XRHMD
            try
            {
                var hmd = InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRHMD>();
                if (hmd != null && hmd.added)
                {
                    Quaternion rot = hmd.deviceRotation.ReadValue();
                    if (rot != Quaternion.identity)
                    {
                        mainCamera.transform.localRotation = rot;
                        mainCamera.transform.localPosition = hmd.devicePosition.ReadValue();
                        return true;
                    }
                }
            }
            catch {}

            return false;
        }

        public static bool IsVRActive()
        {
            if (XRSettings.isDeviceActive) return true;

            SubsystemManager.GetSubsystems(displaySubsystems);
            for (int i = 0; i < displaySubsystems.Count; i++)
            {
                if (displaySubsystems[i].running) return true;
            }
            return false;
        }

        // --------------------------------------------------------- Meta Quest Controller Logic
        private void PollQuestControllers()
        {
            XRInputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftControllers);
            XRInputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightControllers);

            // =========================================================================
            // 1. LEFT CONTROLLER
            // - X Button (primaryButton): SELECTING
            // - Y Button (secondaryButton): PLAY and PAUSE control
            // - Menu Button (menuButton): TOGGLE MENU
            // - Joystick: Switch campus views
            // =========================================================================
            if (leftControllers.Count > 0)
            {
                var leftDev = leftControllers[0];

                // Update physical left hand pose if tracking data is available
                if (leftHandAnchor != null)
                {
                    if (leftDev.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 lPos))
                        leftHandAnchor.localPosition = lPos;
                    if (leftDev.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion lRot))
                        leftHandAnchor.localRotation = lRot;
                }

                // [X BUTTON] -> Switch View
                if (leftDev.TryGetFeatureValue(CommonUsages.primaryButton, out bool xPressed))
                {
                    if (xPressed && !lastLeftX)
                    {
                        Debug.Log("[CampusTour] Left Controller (X) pressed: Next View");
                        if (tourManager != null) tourManager.NextLocation();
                        ExecuteRayClick();
                    }
                    lastLeftX = xPressed;
                }

                // [Y BUTTON] -> PLAY / PAUSE
                if (leftDev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool yPressed))
                {
                    if (yPressed && !lastLeftY)
                    {
                        Debug.Log("[CampusTour] Left Controller (Y) pressed: PLAY / PAUSE");
                        if (tourManager != null) tourManager.TogglePlayPause();
                    }
                    lastLeftY = yPressed;
                }

                // [LEFT MENU BUTTON] -> MENU
                if (leftDev.TryGetFeatureValue(CommonUsages.menuButton, out bool menuPressed))
                {
                    if (menuPressed && !lastLeftMenu)
                    {
                        Debug.Log("[CampusTour] Left Menu button pressed: MENU");
                        ToggleMenu();
                    }
                    lastLeftMenu = menuPressed;
                }

                // [LEFT STICK CLICK] -> Switch View
                if (leftDev.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool lStickClick))
                {
                    if (lStickClick && !lastLeftStickClick)
                    {
                        Debug.Log("[CampusTour] Left Stick Click: Next View");
                        if (tourManager != null) tourManager.NextLocation();
                    }
                    lastLeftStickClick = lStickClick;
                }

                // Left Trigger -> Select
                if (leftDev.TryGetFeatureValue(CommonUsages.triggerButton, out bool leftTrigger))
                {
                    if (leftTrigger && !lastLeftTrigger)
                    {
                        ExecuteRayClick();
                    }
                    lastLeftTrigger = leftTrigger;
                }
            }

            // =========================================================================
            // 2. RIGHT CONTROLLER
            // - A Button (primaryButton): SELECTING ONLY
            // - B Button (secondaryButton): MENU
            // - Trigger: SELECT (Ray click)
            // - Joystick: Snap Turn 45°
            // =========================================================================
            if (rightControllers.Count > 0)
            {
                var rightDev = rightControllers[0];

                // Update physical right hand pose if tracking data is available
                if (rightHandAnchor != null)
                {
                    if (rightDev.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rPos))
                        rightHandAnchor.localPosition = rPos;
                    if (rightDev.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rRot))
                        rightHandAnchor.localRotation = rRot;
                }

                // [A BUTTON] -> Switch View
                if (rightDev.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed))
                {
                    if (aPressed && !lastRightA)
                    {
                        Debug.Log("[CampusTour] Right Controller (A) pressed: Next View");
                        if (tourManager != null) tourManager.NextLocation();
                        ExecuteRayClick();
                    }
                    lastRightA = aPressed;
                }

                // [B BUTTON] -> PREVIOUS VIEW
                if (rightDev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bPressed))
                {
                    if (bPressed && !lastRightB)
                    {
                        Debug.Log("[CampusTour] Right Controller (B) pressed: Previous View");
                        if (tourManager != null) tourManager.PreviousLocation();
                    }
                    lastRightB = bPressed;
                }

                // [RIGHT STICK CLICK] -> Switch View
                if (rightDev.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool rStickClick))
                {
                    if (rStickClick && !lastRightStickClick)
                    {
                        Debug.Log("[CampusTour] Right Stick Click: Next View");
                        if (tourManager != null) tourManager.NextLocation();
                    }
                    lastRightStickClick = rStickClick;
                }

                // Right Trigger -> SELECT (Ray click)
                if (rightDev.TryGetFeatureValue(CommonUsages.triggerButton, out bool rightTrigger))
                {
                    if (rightTrigger && !lastRightTrigger)
                    {
                        ExecuteRayClick();
                    }
                    lastRightTrigger = rightTrigger;
                }
            }
        }

        private void ToggleMenu()
        {
            if (tourUI != null)
            {
                tourUI.ToggleSideDock();
            }
            else if (tourManager != null)
            {
                tourManager.ToggleUI();
            }
        }

        private void UpdateVRRayPointer()
        {
            if (rayLineRenderer != null && rayLineRenderer.enabled)
            {
                rayLineRenderer.enabled = false;
            }
        }

        private void ExecuteRayClick()
        {
            // 1. In non-VR Editor mode, first check EventSystem UI Raycast under the mouse cursor
            if (!IsVRActive() && EventSystem.current != null)
            {
                Mouse mouse = Mouse.current;
                Vector2 screenPos = (mouse != null) ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, hits);
                foreach (var h in hits)
                {
                    var btn = h.gameObject.GetComponentInParent<UnityEngine.UI.Button>();
                    if (btn != null && btn.interactable)
                    {
                        Debug.Log($"[CampusTour] Clicked UI Button: {btn.gameObject.name}");
                        btn.onClick.Invoke();
                        return;
                    }
                }
            }

            // 2. VR Ray or 3D Physics Raycast
            Ray ray;
            if (IsVRActive() && rightHandAnchor != null)
            {
                ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);
            }
            else if (mainCamera != null)
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
                }
                else
                {
                    ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                }
            }
            else
            {
                return;
            }

            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, raycastMask))
            {
                // Check if hit a UI Button on world-space canvas
                var btn = hit.collider.GetComponentInParent<UnityEngine.UI.Button>();
                if (btn != null && btn.interactable)
                {
                    Debug.Log($"[CampusTour] Ray hit UI Button: {btn.gameObject.name}");
                    btn.onClick.Invoke();
                    return;
                }

                // Check if hit a Hotspot
                var hotspot = hit.collider.GetComponentInParent<HotspotController>();
                if (hotspot != null)
                {
                    hotspot.TriggerActivation();
                    return;
                }
            }
        }

        public void ApplyYawTurn(float degrees)
        {
            if (rigTransform != null)
            {
                rigTransform.Rotate(0f, degrees, 0f, Space.World);
            }
            else if (transform.parent != null)
            {
                transform.parent.Rotate(0f, degrees, 0f, Space.World);
            }
        }

        // --------------------------------------------------------- Editor Preview Look & Shortcuts
        private void HandleEditorLook()
        {
            float yawDelta = 0f;
            float pitchDelta = 0f;

            // Mouse Look (Left or Right Click Drag for PC/Editor preview only)
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                bool isPressed = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ExecuteRayClick();
                    if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    {
                        isMouseDragging = true;
                    }
                }
                else if (mouse.rightButton.wasPressedThisFrame)
                {
                    isMouseDragging = true;
                }

                if (!isPressed)
                {
                    isMouseDragging = false;
                }

                if (isMouseDragging)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yawDelta += delta.x * mouseSensitivity * 10f;
                    pitchDelta -= delta.y * mouseSensitivity * 10f;
                }
            }

            // Apply rotation to mainCamera
            if (yawDelta != 0f || pitchDelta != 0f)
            {
                editorYaw += yawDelta;
                editorPitch += pitchDelta;
                editorPitch = Mathf.Clamp(editorPitch, -85f, 85f);

                if (mainCamera != null)
                {
                    mainCamera.transform.localRotation = Quaternion.Euler(editorPitch, editorYaw, 0f);
                }
            }
        }

        private void HandleEditorKeyboardShortcuts()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // X / Enter key -> Next View
            if (kb.xKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            {
                if (tourManager != null) tourManager.NextLocation();
            }

            // Y key / Spacebar -> PLAY / PAUSE
            if (kb.yKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                if (tourManager != null) tourManager.TogglePlayPause();
            }

            // B key / M key / Tab / Escape -> TOGGLE MENU
            if (kb.bKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                ToggleMenu();
            }

            // 1-3 keys -> Direct view jump
            if (kb.digit1Key.wasPressedThisFrame && tourManager != null) tourManager.LoadLocation(0);
            if (kb.digit2Key.wasPressedThisFrame && tourManager != null) tourManager.LoadLocation(1);
            if (kb.digit3Key.wasPressedThisFrame && tourManager != null) tourManager.LoadLocation(2);
        }
    }
}
