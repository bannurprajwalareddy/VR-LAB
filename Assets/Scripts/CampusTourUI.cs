using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CampusTour
{
    public class CampusTourUI : MonoBehaviour
    {
        [Header("Manager & Player")]
        [SerializeField] private CampusTourManager tourManager;
        [SerializeField] private PanoramaVideoPlayer videoPlayer;

        [Header("Lab Experience Panel")]
        [SerializeField] private GameObject sideDockPanel;
        [SerializeField] private Button view1Button;
        [SerializeField] private Button view2Button;
        [SerializeField] private Button view3Button;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Text playPauseText;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteText;

        [Header("Location Toast (Fades out automatically)")]
        [SerializeField] private CanvasGroup locationToastGroup;
        [SerializeField] private Text locationToastText;

        [Header("Feedback & Overlays")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Text loadingText;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private Text errorText;
        [SerializeField] private Button errorDismissButton;
        [SerializeField] private CanvasGroup fadeOverlay;

        private Coroutine toastCoroutine;

        public bool IsSideDockOpen => sideDockPanel != null && sideDockPanel.activeSelf;
        public CanvasGroup FadeOverlay => fadeOverlay;

        private void Awake()
        {
            if (tourManager == null) tourManager = FindFirstObjectByType<CampusTourManager>();
            if (videoPlayer == null) videoPlayer = FindFirstObjectByType<PanoramaVideoPlayer>();

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.gameObject.SetActive(false);
            }
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            PurgeLegacyUIClutter();
            SetupLabExperienceUI();
        }

        private void PurgeLegacyUIClutter()
        {
            string[] legacyNames = {
                "HeaderPanel", "TopHeader", "BottomBar", "ControlsPanel",
                "HintsPanel", "ControllerHints", "LocationsPanel", "LocationList",
                "InfoPopupPanel", "MenuModal"
            };

            foreach (var n in legacyNames)
            {
                var tr = transform.Find(n);
                if (tr != null && tr.gameObject != sideDockPanel)
                {
                    tr.gameObject.SetActive(false);
                    Destroy(tr.gameObject);
                }
            }

            var oldHotspots = FindObjectsByType<HotspotController>(FindObjectsSortMode.None);
            foreach (var h in oldHotspots)
            {
                if (h != null) Destroy(h.gameObject);
            }
        }

        public void PositionCanvasInFrontOfCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();

                transform.position = cam.transform.position + fwd * 2.0f;
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }

        public void SetupLabExperienceUI()
        {
            if (sideDockPanel != null)
            {
                sideDockPanel.SetActive(true);

                if (view1Button == null)
                {
                    var t = sideDockPanel.transform.Find("ViewBtn_0") ?? sideDockPanel.transform.Find("ViewsContainer/ViewBtn_0");
                    if (t != null) view1Button = t.GetComponent<Button>();
                }
                if (view2Button == null)
                {
                    var t = sideDockPanel.transform.Find("ViewBtn_1") ?? sideDockPanel.transform.Find("ViewsContainer/ViewBtn_1");
                    if (t != null) view2Button = t.GetComponent<Button>();
                }
                if (view3Button == null)
                {
                    var t = sideDockPanel.transform.Find("ViewBtn_2") ?? sideDockPanel.transform.Find("ViewsContainer/ViewBtn_2");
                    if (t != null) view3Button = t.GetComponent<Button>();
                }
                if (playPauseButton == null)
                {
                    var t = sideDockPanel.transform.Find("PlayPauseBtn");
                    if (t != null) playPauseButton = t.GetComponent<Button>();
                }
                if (playPauseText == null && playPauseButton != null)
                {
                    playPauseText = playPauseButton.GetComponentInChildren<Text>();
                }
                if (muteButton == null)
                {
                    var t = sideDockPanel.transform.Find("MuteBtn");
                    if (t != null) muteButton = t.GetComponent<Button>();
                }
                if (muteText == null && muteButton != null)
                {
                    muteText = muteButton.GetComponentInChildren<Text>();
                }
            }

            if (view1Button != null)
            {
                view1Button.onClick.RemoveAllListeners();
                view1Button.onClick.AddListener(() => LoadLocationDirect(0));
            }
            if (view2Button != null)
            {
                view2Button.onClick.RemoveAllListeners();
                view2Button.onClick.AddListener(() => LoadLocationDirect(1));
            }
            if (view3Button != null)
            {
                view3Button.onClick.RemoveAllListeners();
                view3Button.onClick.AddListener(() => LoadLocationDirect(2));
            }
            if (playPauseButton != null)
            {
                playPauseButton.onClick.RemoveAllListeners();
                playPauseButton.onClick.AddListener(TogglePlayPause);
            }
            if (muteButton != null)
            {
                muteButton.onClick.RemoveAllListeners();
                muteButton.onClick.AddListener(ToggleMute);
            }
            if (errorDismissButton != null)
            {
                errorDismissButton.onClick.RemoveAllListeners();
                errorDismissButton.onClick.AddListener(DismissError);
            }

            HighlightCurrentView(tourManager != null ? tourManager.CurrentLocationIndex : 0);
        }

        private void Start()
        {
            if (videoPlayer != null)
            {
                videoPlayer.OnPrepareStarted += HandlePrepareStarted;
                videoPlayer.OnPrepareCompleted += HandlePrepareCompleted;
                videoPlayer.OnPlaybackError += HandlePlaybackError;
                videoPlayer.OnPlayStateChanged += HandlePlayStateChanged;
                videoPlayer.OnMuteStateChanged += HandleMuteStateChanged;
            }

            if (errorPanel != null) errorPanel.SetActive(false);
            if (loadingPanel != null) loadingPanel.SetActive(false);

            if (sideDockPanel != null) sideDockPanel.SetActive(true);

            PositionCanvasInFrontOfCamera();
            SetupLabExperienceUI();
            UpdatePlayPauseUI(videoPlayer != null && videoPlayer.IsPlaying);
            UpdateMuteUI(videoPlayer != null && videoPlayer.IsMuted);
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.OnPrepareStarted -= HandlePrepareStarted;
                videoPlayer.OnPrepareCompleted -= HandlePrepareCompleted;
                videoPlayer.OnPlaybackError -= HandlePlaybackError;
                videoPlayer.OnPlayStateChanged -= HandlePlayStateChanged;
                videoPlayer.OnMuteStateChanged -= HandleMuteStateChanged;
            }
        }

        public void LoadLocationDirect(int index)
        {
            Debug.Log($"[CampusTour] View Button clicked: View {index + 1}");
            if (tourManager != null)
            {
                tourManager.LoadLocation(index);
            }
            HighlightCurrentView(index);
        }

        public void PopulateLocationMenu(List<LocationData> locations, int currentIndex)
        {
            SetupLabExperienceUI();
            HighlightCurrentView(currentIndex);
        }

        public void HighlightCurrentView(int currentIndex)
        {
            Button[] buttons = { view1Button, view2Button, view3Button };
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    var img = buttons[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = (i == currentIndex)
                            ? new Color(0.12f, 0.55f, 0.95f, 1f)
                            : new Color(0.16f, 0.22f, 0.32f, 0.95f);
                    }
                }
            }
        }

        public void SetCurrentLocationInfo(string name, string description, int index)
        {
            HighlightCurrentView(index);
            ShowQuickToast(name);
        }

        public void ShowQuickToast(string message)
        {
            if (locationToastGroup != null && locationToastText != null)
            {
                locationToastText.text = message;
                locationToastText.fontSize = 32;
                locationToastText.fontStyle = FontStyle.Bold;
                if (toastCoroutine != null) StopCoroutine(toastCoroutine);
                toastCoroutine = StartCoroutine(ShowAndFadeToast());
            }
        }

        private IEnumerator ShowAndFadeToast()
        {
            locationToastGroup.alpha = 1f;
            locationToastGroup.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.0f);

            float elapsed = 0f;
            float duration = 0.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                locationToastGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }

            locationToastGroup.alpha = 0f;
            locationToastGroup.gameObject.SetActive(false);
            toastCoroutine = null;
        }

        public void ToggleSideDock()
        {
            if (sideDockPanel != null)
            {
                bool newState = !sideDockPanel.activeSelf;
                sideDockPanel.SetActive(newState);
                if (newState)
                {
                    PositionCanvasInFrontOfCamera();
                }
            }
        }

        public void ToggleMenu() => ToggleSideDock();
        public void ToggleUI() => ToggleSideDock();
        public void OpenMenu()
        {
            if (sideDockPanel != null)
            {
                sideDockPanel.SetActive(true);
                PositionCanvasInFrontOfCamera();
            }
        }
        public void CloseMenu()
        {
            if (sideDockPanel != null)
            {
                sideDockPanel.SetActive(false);
            }
        }

        public void TogglePlayPause()
        {
            if (videoPlayer != null)
            {
                videoPlayer.TogglePlayPause();
                UpdatePlayPauseUI(videoPlayer.IsPlaying);
            }
            else if (tourManager != null)
            {
                tourManager.TogglePlayPause();
            }
        }

        public void ToggleMute()
        {
            if (videoPlayer != null)
            {
                videoPlayer.ToggleMute();
                UpdateMuteUI(videoPlayer.IsMuted);
            }
            else if (tourManager != null)
            {
                tourManager.ToggleMute();
            }
        }

        private void HandlePlayStateChanged(bool isPlaying)
        {
            UpdatePlayPauseUI(isPlaying);
        }

        private void HandleMuteStateChanged(bool isMuted)
        {
            UpdateMuteUI(isMuted);
        }

        public void UpdatePlayPauseUI(bool isPlaying)
        {
            if (playPauseText != null)
            {
                playPauseText.text = isPlaying ? "||  PAUSE" : ">  PLAY";
            }

            if (playPauseButton != null)
            {
                var img = playPauseButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isPlaying
                        ? new Color(0.12f, 0.45f, 0.8f, 0.95f)
                        : new Color(0.85f, 0.45f, 0.1f, 0.98f);
                }
            }
        }

        public void UpdateMuteUI(bool isMuted)
        {
            if (muteText != null)
            {
                muteText.text = isMuted ? "AUDIO: MUTED" : "AUDIO: ON";
            }

            if (muteButton != null)
            {
                var img = muteButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isMuted
                        ? new Color(0.65f, 0.2f, 0.2f, 0.95f)
                        : new Color(0.16f, 0.22f, 0.32f, 0.95f);
                }
            }
        }

        private void HandlePrepareStarted(string fileName)
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        private void HandlePrepareCompleted()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (errorPanel != null) errorPanel.SetActive(false);
            UpdatePlayPauseUI(videoPlayer != null && videoPlayer.IsPlaying);
        }

        private void HandlePlaybackError(string message)
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (errorPanel != null)
            {
                if (errorText != null) errorText.text = message;
                errorPanel.SetActive(true);
            }
        }

        public void DismissError()
        {
            if (errorPanel != null) errorPanel.SetActive(false);
        }

        public void ShowInfoPopup(string title, string content)
        {
            ShowQuickToast(title);
        }
    }
}
