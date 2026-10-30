using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CampusTour
{
    public class HotspotController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Hotspot Configuration")]
        [SerializeField] private HotspotData data;
        [SerializeField] private Text labelText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundRing;

        private CampusTourManager tourManager;
        private Camera targetCamera;
        private Vector3 initialScale;
        private bool isHovered = false;

        public HotspotData Data => data;

        public void Initialize(HotspotData hotspotData, CampusTourManager manager)
        {
            data = hotspotData;
            tourManager = manager;
            targetCamera = Camera.main;
            initialScale = transform.localScale;

            UpdatePosition();
            UpdateVisuals();
        }

        public void UpdatePosition()
        {
            if (data == null) return;

            // Convert yaw and pitch (in degrees) to spherical 3D direction vector
            // Yaw rotates around Y axis (0 is Forward +Z, 90 is Right +X)
            // Pitch rotates up/down (+pitch is Up +Y, -pitch is Down -Y)
            float yawRad = data.yaw * Mathf.Deg2Rad;
            float pitchRad = data.pitch * Mathf.Deg2Rad;

            float x = data.distance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
            float y = data.distance * Mathf.Sin(pitchRad);
            float z = data.distance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

            transform.localPosition = new Vector3(x, y, z);
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                // Face towards camera (billboarding)
                Vector3 dirToCam = targetCamera.transform.position - transform.position;
                if (dirToCam.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(-dirToCam, Vector3.up);
                }
            }

            // Smooth hover scale animation
            Vector3 target = isHovered ? initialScale * 1.25f : initialScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * 10f);
        }

        private void UpdateVisuals()
        {
            if (data == null) return;

            if (labelText != null)
            {
                labelText.text = data.label;
            }

            if (backgroundRing != null)
            {
                // Navigation = vibrant teal/cyan, Information = warm amber/gold
                Color themeColor = data.hotspotType == HotspotType.Navigation
                    ? new Color(0.2f, 0.8f, 1f, 0.85f)
                    : new Color(1f, 0.75f, 0.2f, 0.85f);
                backgroundRing.color = themeColor;
            }
        }

        public void TriggerActivation()
        {
            if (data == null || tourManager == null) return;

            if (data.hotspotType == HotspotType.Navigation)
            {
                Debug.Log($"[CampusTour] Hotspot activated: Navigate to location index {data.targetLocationIndex}");
                tourManager.LoadLocation(data.targetLocationIndex);
            }
            else
            {
                Debug.Log($"[CampusTour] Hotspot activated: Display info '{data.label}'");
                tourManager.ShowInfoPopup(data.label, data.description);
            }
        }

        // Pointer event implementations (Unity EventSystem & TrackedDeviceGraphicRaycaster / PhysicsRaycaster)
        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerActivation();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        // Mouse click support for Editor preview mode if 3D colliders are used
        private void OnMouseDown()
        {
            if (!Application.isMobilePlatform && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                TriggerActivation();
            }
        }

        private void OnMouseEnter()
        {
            isHovered = true;
        }

        private void OnMouseExit()
        {
            isHovered = false;
        }
    }
}
