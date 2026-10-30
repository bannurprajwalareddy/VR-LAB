using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace CampusTour
{
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(AudioSource))]
    public class PanoramaVideoPlayer : MonoBehaviour
    {
        [Header("Render Resources")]
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private Material skyboxMaterial;

        [Header("Playback Defaults")]
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnAwake = false;

        private VideoPlayer videoPlayer;
        private AudioSource audioSource;
        private string currentFileName = string.Empty;
        private bool isPreparing = false;
        private bool isPaused = false;
        private bool isMuted = false;
        private float prepareTimeout = 12f;
        private float prepareTimer = 0f;

        public event Action<string> OnPrepareStarted;
        public event Action OnPrepareCompleted;
        public event Action<string> OnPlaybackError;
        public event Action<bool> OnPlayStateChanged; // true = playing, false = paused
        public event Action<bool> OnMuteStateChanged;

        public bool IsPaused => isPaused;
        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying && !isPaused;
        public bool IsMuted => isMuted;
        public bool IsPreparing => isPreparing;
        public bool IsPrepared => videoPlayer != null && videoPlayer.isPrepared;
        public string CurrentFileName => currentFileName;

        public double CurrentTime => videoPlayer != null && videoPlayer.isPrepared ? videoPlayer.time : 0.0;
        public double Duration
        {
            get
            {
                if (videoPlayer == null || !videoPlayer.isPrepared || videoPlayer.frameRate <= 0f) return 0.0;
                return videoPlayer.frameCount / (double)videoPlayer.frameRate;
            }
        }

        public float NormalizedProgress
        {
            get
            {
                double d = Duration;
                if (d <= 0.001) return 0f;
                return Mathf.Clamp01((float)(CurrentTime / d));
            }
        }

        private void Awake()
        {
            InitializePlayer();
        }

        private void InitializePlayer()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D stereo ambient

            videoPlayer.playOnAwake = playOnAwake;
            videoPlayer.isLooping = loop;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, audioSource);

            videoPlayer.prepareCompleted += HandlePrepareCompleted;
            videoPlayer.errorReceived += HandleErrorReceived;
            videoPlayer.loopPointReached += HandleLoopPointReached;

            ApplySkyboxTextures();
        }

        public void ApplySkyboxTextures()
        {
            if (skyboxMaterial == null) skyboxMaterial = RenderSettings.skybox;

            if (renderTexture != null)
            {
                if (skyboxMaterial != null)
                {
                    skyboxMaterial.mainTexture = renderTexture;
                    if (skyboxMaterial.HasProperty("_Tex")) skyboxMaterial.SetTexture("_Tex", renderTexture);
                    if (skyboxMaterial.HasProperty("_MainTex")) skyboxMaterial.SetTexture("_MainTex", renderTexture);
                }
                if (RenderSettings.skybox != null && RenderSettings.skybox != skyboxMaterial)
                {
                    RenderSettings.skybox.mainTexture = renderTexture;
                    if (RenderSettings.skybox.HasProperty("_Tex")) RenderSettings.skybox.SetTexture("_Tex", renderTexture);
                    if (RenderSettings.skybox.HasProperty("_MainTex")) RenderSettings.skybox.SetTexture("_MainTex", renderTexture);
                }
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                videoPlayer.errorReceived -= HandleErrorReceived;
                videoPlayer.loopPointReached -= HandleLoopPointReached;
                videoPlayer.Stop();
            }
        }

        private void Update()
        {
            if (isPreparing)
            {
                prepareTimer += Time.deltaTime;
                if (prepareTimer > prepareTimeout)
                {
                    isPreparing = false;
                    string msg = $"Video preparation timed out for: {currentFileName}";
                    Debug.LogWarning($"[CampusTour] {msg}");
                    OnPlaybackError?.Invoke(msg);
                }
            }
        }

        public static string BuildVideoUrl(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;

            // Normalize path separators
            string sanitized = fileName.Trim().Replace('\\', '/');
            if (sanitized.StartsWith("/")) sanitized = sanitized.Substring(1);

            return Path.Combine(Application.streamingAssetsPath, "Videos", sanitized).Replace('\\', '/');
        }

        public void LoadVideo(string fileName)
        {
            currentFileName = fileName;
            if (videoPlayer == null) InitializePlayer();

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }
            isPaused = false;
            isPreparing = true;
            prepareTimer = 0f;

            if (string.IsNullOrEmpty(fileName))
            {
                isPreparing = false;
                string err = "No video filename specified for this location.";
                Debug.LogWarning($"[CampusTour] {err}");
                OnPlaybackError?.Invoke(err);
                return;
            }

            // Quick desktop check to provide immediate diagnostic warnings
            if (!Application.isMobilePlatform)
            {
                string localPath = Path.Combine(Application.streamingAssetsPath, "Videos", fileName);
                if (!File.Exists(localPath))
                {
                    Debug.LogWarning($"[CampusTour] File does not exist at local path: {localPath}. Attempting VideoPlayer URL playback in case of custom asset package.");
                }
            }

            string url = BuildVideoUrl(fileName);
            Debug.Log($"[CampusTour] Preparing video URL: {url}");

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = url;

            OnPrepareStarted?.Invoke(fileName);
            videoPlayer.Prepare();
        }

        private void HandlePrepareCompleted(VideoPlayer source)
        {
            isPreparing = false;
            Debug.Log($"[CampusTour] Video prepared successfully: {currentFileName} ({source.texture?.width}x{source.texture?.height})");
            ApplySkyboxTextures();
            source.Play();
            if (audioSource != null && !isMuted) audioSource.Play();
            OnPrepareCompleted?.Invoke();
            OnPlayStateChanged?.Invoke(true);
        }

        private void HandleErrorReceived(VideoPlayer source, string message)
        {
            isPreparing = false;
            string formatted = $"Video playback error ({currentFileName}): {message}";
            Debug.LogError($"[CampusTour] {formatted}");
            OnPlaybackError?.Invoke($"Video not found or format unsupported: {currentFileName}");
        }

        private void HandleLoopPointReached(VideoPlayer source)
        {
            if (loop)
            {
                source.Play();
                if (audioSource != null && !isMuted) audioSource.Play();
            }
            else
            {
                OnPlayStateChanged?.Invoke(false);
            }
        }

        public void Play()
        {
            if (videoPlayer != null)
            {
                isPaused = false;
                videoPlayer.Play();
                if (audioSource != null && !isMuted) audioSource.Play();
                OnPlayStateChanged?.Invoke(true);
            }
        }

        public void Pause()
        {
            if (videoPlayer != null)
            {
                isPaused = true;
                videoPlayer.Pause();
                if (audioSource != null) audioSource.Pause();
                OnPlayStateChanged?.Invoke(false);
            }
        }

        public void TogglePlayPause()
        {
            if (isPaused) Play();
            else Pause();
        }

        public void SetMute(bool mute)
        {
            isMuted = mute;
            if (audioSource != null) audioSource.mute = mute;
            if (videoPlayer != null && videoPlayer.audioTrackCount > 0)
            {
                videoPlayer.SetDirectAudioMute(0, mute);
            }
            OnMuteStateChanged?.Invoke(isMuted);
        }

        public void ToggleMute()
        {
            SetMute(!isMuted);
        }

        public void Seek(float normalizedTime)
        {
            if (videoPlayer == null || !videoPlayer.isPrepared) return;
            double target = Mathf.Clamp01(normalizedTime) * Duration;
            videoPlayer.time = target;
        }

        public void SetResources(RenderTexture rt, Material mat)
        {
            renderTexture = rt;
            skyboxMaterial = mat;
            if (videoPlayer != null) videoPlayer.targetTexture = rt;
            if (skyboxMaterial != null && renderTexture != null)
            {
                skyboxMaterial.mainTexture = renderTexture;
                if (skyboxMaterial.HasProperty("_Tex"))
                {
                    skyboxMaterial.SetTexture("_Tex", renderTexture);
                }
            }
        }
    }
}
