using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public class CampusTourManager : MonoBehaviour
    {
        [Header("Locations Configuration")]
        [SerializeField] private List<LocationData> locations = new List<LocationData>();
        [SerializeField] private int initialLocationIndex = 0;

        [Header("System References")]
        [SerializeField] private PanoramaVideoPlayer videoPlayer;
        [SerializeField] private CampusTourUI tourUI;
        [SerializeField] private Transform hotspotsContainer;
        [SerializeField] private GameObject hotspotPrefab;

        [Header("Transition Settings")]
        [SerializeField] private float fadeDuration = 0f;

        private int currentLocationIndex = -1;
        private bool isTransitioning = false;
        private readonly List<GameObject> activeHotspots = new List<GameObject>();
        private Coroutine transitionCoroutine;

        public List<LocationData> Locations => locations;
        public int CurrentLocationIndex => currentLocationIndex;
        public bool IsTransitioning => isTransitioning;

        private void Awake()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<PanoramaVideoPlayer>() ?? FindFirstObjectByType<PanoramaVideoPlayer>();
            if (tourUI == null) tourUI = FindFirstObjectByType<CampusTourUI>();

            if (hotspotsContainer == null)
            {
                GameObject hc = new GameObject("HotspotsContainer");
                hc.transform.SetParent(transform, false);
                hotspotsContainer = hc.transform;
            }

            // Always enforce the 4 clean Lab Experience views with zero floating text/hotspots
            EnsureDefaultLocations();

            // Destroy any old hotspots or stray signs
            ClearHotspots();
            var strayHotspots = FindObjectsByType<HotspotController>(FindObjectsSortMode.None);
            foreach (var sh in strayHotspots)
            {
                if (sh != null) Destroy(sh.gameObject);
            }
        }

        private void Start()
        {
            if (tourUI != null)
            {
                tourUI.PopulateLocationMenu(locations, initialLocationIndex);
            }

            if (locations.Count > 0)
            {
                LoadLocation(Mathf.Clamp(initialLocationIndex, 0, locations.Count - 1), instant: true);
            }
        }

        private void EnsureDefaultLocations()
        {
            locations = new List<LocationData>
            {
                new LocationData
                {
                    locationName = "View 1",
                    videoFileName = "Gate.mp4",
                    description = "Lab Experience — View 1",
                    hotspots = new List<HotspotData>()
                },
                new LocationData
                {
                    locationName = "View 2",
                    videoFileName = "Library.mp4",
                    description = "Lab Experience — View 2",
                    hotspots = new List<HotspotData>()
                },
                new LocationData
                {
                    locationName = "View 3",
                    videoFileName = "Lab.mp4",
                    description = "Lab Experience — View 3",
                    hotspots = new List<HotspotData>()
                }
            };
        }

        public void LoadLocation(int index, bool instant = false)
        {
            if (index < 0 || index >= locations.Count)
            {
                Debug.LogWarning($"[CampusTour] Invalid location index: {index}. Available: 0 to {locations.Count - 1}");
                return;
            }

            if (isTransitioning)
            {
                Debug.Log("[CampusTour] Transition currently in progress; ignoring duplicate request.");
                return;
            }

            if (index == currentLocationIndex && !instant)
            {
                Debug.Log($"[CampusTour] Location {index} is already active.");
                return;
            }

            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(TransitionToLocationRoutine(index, instant));
        }

        private IEnumerator TransitionToLocationRoutine(int targetIndex, bool instant)
        {
            isTransitioning = true;
            LocationData targetLocation = locations[targetIndex];

            // Ensure any fade overlay is completely hidden so there is no black screen
            if (tourUI != null && tourUI.FadeOverlay != null)
            {
                tourUI.FadeOverlay.alpha = 0f;
                tourUI.FadeOverlay.gameObject.SetActive(false);
            }

            // 1. Keep previous frame / pause audio while preparing new video
            if (videoPlayer != null)
            {
                videoPlayer.Pause();
            }

            // 2. Clear existing hotspots
            ClearActiveHotspots();

            // 3. Update current index and UI labels
            currentLocationIndex = targetIndex;
            if (tourUI != null)
            {
                tourUI.SetCurrentLocationInfo(targetLocation.locationName, targetLocation.description, currentLocationIndex);
            }

            // 4. Spawn new hotspots
            SpawnHotspotsForLocation(targetLocation);

            // 5. Asynchronously prepare new 360 video (seamless cut straight to new video)
            bool prepareDone = false;

            void OnDone() => prepareDone = true;
            void OnErr(string msg)
            {
                Debug.LogWarning($"[CampusTour] Video preparation notice: {msg}");
                prepareDone = true;
            }

            if (videoPlayer != null)
            {
                videoPlayer.OnPrepareCompleted += OnDone;
                videoPlayer.OnPlaybackError += OnErr;

                videoPlayer.LoadVideo(targetLocation.videoFileName);

                // Wait until preparation succeeds or fails
                while (!prepareDone)
                {
                    yield return null;
                }

                videoPlayer.OnPrepareCompleted -= OnDone;
                videoPlayer.OnPlaybackError -= OnErr;
            }

            // Keep fade overlay hidden
            if (tourUI != null && tourUI.FadeOverlay != null)
            {
                tourUI.FadeOverlay.alpha = 0f;
                tourUI.FadeOverlay.gameObject.SetActive(false);
            }

            isTransitioning = false;
        }

        public void ClearHotspots()
        {
            ClearActiveHotspots();
            if (hotspotsContainer != null)
            {
                for (int i = hotspotsContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(hotspotsContainer.GetChild(i).gameObject);
                }
            }
        }

        private void ClearActiveHotspots()
        {
            for (int i = 0; i < activeHotspots.Count; i++)
            {
                if (activeHotspots[i] != null) Destroy(activeHotspots[i]);
            }
            activeHotspots.Clear();
        }

        private void SpawnHotspotsForLocation(LocationData loc)
        {
            if (loc == null || loc.hotspots == null) return;

            foreach (var hd in loc.hotspots)
            {
                GameObject go = null;
                if (hotspotPrefab != null)
                {
                    go = Instantiate(hotspotPrefab, hotspotsContainer);
                }
                else
                {
                    go = CreateProceduralHotspotObject(hd);
                    go.transform.SetParent(hotspotsContainer, false);
                }

                var ctrl = go.GetComponent<HotspotController>() ?? go.AddComponent<HotspotController>();
                ctrl.Initialize(hd, this);
                activeHotspots.Add(go);
            }
        }

        private GameObject CreateProceduralHotspotObject(HotspotData hd)
        {
            GameObject root = new GameObject($"Hotspot_{hd.label}");
            
            // World space canvas for clean rendering and text
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 80f);
            root.transform.localScale = Vector3.one * 0.005f;

            // Background panel
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bgGo.transform.SetParent(root.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            var bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
            bgImg.color = (hd.hotspotType == HotspotType.Navigation)
                ? new Color(0.1f, 0.6f, 0.95f, 0.9f)
                : new Color(0.95f, 0.65f, 0.1f, 0.9f);

            // Text label
            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            textGo.transform.SetParent(bgGo.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 6f);
            textRt.offsetMax = new Vector2(-10f, -6f);

            var txt = textGo.GetComponent<UnityEngine.UI.Text>();
            txt.text = (hd.hotspotType == HotspotType.Navigation ? "▶ " : "ℹ ") + hd.label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");
            txt.fontSize = 22;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            // BoxCollider so raycasts hit reliably
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(240f, 80f, 2f);

            return root;
        }

        public void NextLocation()
        {
            if (locations.Count == 0) return;
            int next = (currentLocationIndex + 1) % locations.Count;
            LoadLocation(next);
        }

        public void PreviousLocation()
        {
            if (locations.Count == 0) return;
            int prev = (currentLocationIndex - 1 + locations.Count) % locations.Count;
            LoadLocation(prev);
        }

        public void TogglePlayPause()
        {
            if (videoPlayer != null) videoPlayer.TogglePlayPause();
        }

        public void ToggleMute()
        {
            if (videoPlayer != null) videoPlayer.ToggleMute();
        }

        public void ToggleUI()
        {
            if (tourUI != null) tourUI.ToggleMenu();
        }

        public void ToggleLocationMenu()
        {
            ToggleUI();
        }

        public void ShowInfoPopup(string title, string content)
        {
            if (tourUI != null) tourUI.ShowInfoPopup(title, content);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (videoPlayer != null && videoPlayer.IsPlaying)
                {
                    videoPlayer.Pause();
                }
            }
        }
    }
}
