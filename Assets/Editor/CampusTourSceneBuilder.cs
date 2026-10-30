using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

namespace CampusTour
{
    public static class CampusTourSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CampusTour.unity";
        private const string MaterialsDir = "Assets/CampusTour";
        private const string SkyboxMatPath = "Assets/CampusTour/CampusTourSkyboxMat.mat";
        private const string RenderTexturePath = "Assets/CampusTour/PanoramaRenderTexture.renderTexture";

        [MenuItem("Tools/Campus VR Tour/Build Scene")]
        public static void BuildScene()
        {
            Debug.Log("[CampusTour] Starting Campus VR Tour Scene Build (Clean Immersive UI)...");

            EnsureDirectoryExists("Assets/Scenes");
            EnsureDirectoryExists(MaterialsDir);

            // 1. Create a brand new clean scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Prepare Materials and RenderTexture
            RenderTexture renderTex = GetOrCreateRenderTexture();
            Material skyboxMat = GetOrCreateSkyboxMaterial(renderTex);

            // Configure Lighting / Skybox
            RenderSettings.skybox = skyboxMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();

            // 3. Create XR Origin / Camera Rig
            GameObject xrOriginGo = new GameObject("XR Origin");
            xrOriginGo.transform.position = Vector3.zero;
            xrOriginGo.transform.rotation = Quaternion.identity;

            GameObject cameraOffsetGo = new GameObject("Camera Offset");
            cameraOffsetGo.transform.SetParent(xrOriginGo.transform, false);
            cameraOffsetGo.transform.localPosition = Vector3.zero;

            GameObject mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.transform.SetParent(cameraOffsetGo.transform, false);
            mainCamGo.transform.localPosition = Vector3.zero;

            Camera cam = mainCamGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 80f;

            // Audio listener for 360 sound
            mainCamGo.AddComponent<AudioListener>();

            // Head Tracking is driven directly by TourInputController (XRNode.CenterEye & OpenXR gyro)
            // for instant, natural 1:1 VR head rotation with zero joystick interference.

            // Hand anchors for OpenXR controller tracking and ray pointer
            GameObject leftHandGo = new GameObject("LeftHandAnchor");
            leftHandGo.transform.SetParent(cameraOffsetGo.transform, false);
            leftHandGo.transform.localPosition = new Vector3(-0.25f, -0.1f, 0.4f);

            GameObject rightHandGo = new GameObject("RightHandAnchor");
            rightHandGo.transform.SetParent(cameraOffsetGo.transform, false);
            rightHandGo.transform.localPosition = new Vector3(0.25f, -0.1f, 0.4f);

            // 4. Create Video Screen / Panoramic Video Player
            GameObject videoGo = new GameObject("VideoPlayerSystem");
            videoGo.transform.position = Vector3.zero;

            VideoPlayer unityVideoPlayer = videoGo.AddComponent<VideoPlayer>();
            unityVideoPlayer.playOnAwake = false;
            unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            unityVideoPlayer.targetTexture = renderTex;
            unityVideoPlayer.isLooping = true;
            unityVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            unityVideoPlayer.source = VideoSource.Url;

            AudioSource audioSource = videoGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f; // 2D stereo panoramic audio

            unityVideoPlayer.SetTargetAudioSource(0, audioSource);

            PanoramaVideoPlayer panoPlayer = videoGo.AddComponent<PanoramaVideoPlayer>();
            SetSerializedRef(panoPlayer, "videoPlayer", unityVideoPlayer);
            SetSerializedRef(panoPlayer, "audioSource", audioSource);
            SetSerializedRef(panoPlayer, "skyboxMaterial", skyboxMat);
            SetSerializedRef(panoPlayer, "renderTexture", renderTex);

            // 5. Create World-Space VR UI Canvas (Clean & Immersive - Zero Permanent Clutter)
            GameObject canvasGo = new GameObject("CampusTourCanvas");
            canvasGo.transform.position = new Vector3(0f, 0f, 1.8f);
            canvasGo.transform.rotation = Quaternion.identity;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(1000f, 1000f);
            canvasRt.localScale = Vector3.one * 0.0028f; // Crisp, perfectly sized in VR view

            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var uiCg = canvasGo.AddComponent<CanvasGroup>();

            // EventSystem
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // UI Component
            CampusTourUI tourUI = canvasGo.AddComponent<CampusTourUI>();
            SetSerializedRef(tourUI, "uiCanvasGroup", uiCg);

            // 6. Create Manager & Input Controllers
            GameObject managerGo = new GameObject("CampusTourManager");
            CampusTourManager tourManager = managerGo.AddComponent<CampusTourManager>();

            GameObject hotspotsGo = new GameObject("HotspotsContainer");
            hotspotsGo.transform.position = Vector3.zero;

            SetSerializedRef(tourManager, "videoPlayer", panoPlayer);
            SetSerializedRef(tourManager, "tourUI", tourUI);
            SetSerializedRef(tourManager, "hotspotsContainer", hotspotsGo.transform);

            // TourInputController
            TourInputController inputCtrl = xrOriginGo.AddComponent<TourInputController>();
            SetSerializedRef(inputCtrl, "rigTransform", xrOriginGo.transform);
            SetSerializedRef(inputCtrl, "mainCamera", cam);
            SetSerializedRef(inputCtrl, "rightHandAnchor", rightHandGo.transform);
            SetSerializedRef(inputCtrl, "leftHandAnchor", leftHandGo.transform);
            SetSerializedRef(inputCtrl, "tourManager", tourManager);
            SetSerializedRef(inputCtrl, "tourUI", tourUI);
            SetSerializedRef(inputCtrl, "videoPlayer", panoPlayer);

            // 7. Build UI Hierarchy (Completely Clean & Immersive)
            BuildCleanCanvasElements(canvasGo.transform, tourUI, tourManager, panoPlayer);

            // 8. Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"<color=green>[CampusTour] Campus VR Tour Scene successfully created and saved at: {ScenePath}</color>");

            // 9. Add scene to Build Settings as Scene 0
            AddSceneToBuildSettings(ScenePath);
        }

        public static void BuildSceneCommandLine() => BuildScene();

        private static void BuildCleanCanvasElements(Transform canvasTransform, CampusTourUI tourUI, CampusTourManager manager, PanoramaVideoPlayer player)
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.Load<Font>("Arial");

            // 1. Fade Overlay (covers screen during 0.5s scene transitions)
            RectTransform fadeRt = CreateRect("FadeOverlay", canvasTransform);
            StretchFull(fadeRt);
            var fadeImg = fadeRt.gameObject.AddComponent<Image>();
            fadeImg.color = Color.black;
            fadeImg.raycastTarget = false;
            var fadeCg = fadeRt.gameObject.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            fadeRt.gameObject.SetActive(false);

            // 2. Subtle Location Toast Banner (Appears for 2s on location change, then disappears!)
            RectTransform toastRt = CreateRect("LocationToast", canvasTransform);
            SetAnchor(toastRt, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), new Vector2(600f, 85f), Vector2.zero);
            var toastBg = toastRt.gameObject.AddComponent<Image>();
            toastBg.color = new Color(0.05f, 0.08f, 0.12f, 0.90f);
            var toastCg = toastRt.gameObject.AddComponent<CanvasGroup>();
            toastCg.alpha = 0f;

            RectTransform toastTxtRt = CreateRect("ToastText", toastRt);
            StretchFull(toastTxtRt);
            var toastTxt = toastTxtRt.gameObject.AddComponent<Text>();
            toastTxt.text = "Lab Experience — View 1";
            toastTxt.font = defaultFont;
            toastTxt.fontSize = 38;
            toastTxt.fontStyle = FontStyle.Bold;
            toastTxt.alignment = TextAnchor.MiddleCenter;
            toastTxt.color = new Color(0.35f, 0.88f, 1f);
            toastRt.gameObject.SetActive(false);

            // 3. Lab Experience Control Panel (Centered directly in front of the user's VR view)
            RectTransform sideDockRt = CreateRect("SideDockPanel", canvasTransform);
            SetAnchor(sideDockRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, 570f), Vector2.zero);
            var sideDockBg = sideDockRt.gameObject.AddComponent<Image>();
            sideDockBg.color = new Color(0.05f, 0.08f, 0.14f, 0.94f);

            // Header: LAB EXPERIENCE
            RectTransform sideHeaderRt = CreateRect("Header", sideDockRt);
            SetAnchor(sideHeaderRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(380f, 48f), new Vector2(0f, -24f));
            var sideHeaderTxt = sideHeaderRt.gameObject.AddComponent<Text>();
            sideHeaderTxt.text = "LAB EXPERIENCE";
            sideHeaderTxt.font = defaultFont;
            sideHeaderTxt.fontSize = 32;
            sideHeaderTxt.fontStyle = FontStyle.Bold;
            sideHeaderTxt.alignment = TextAnchor.MiddleCenter;
            sideHeaderTxt.color = new Color(0.35f, 0.88f, 1f);

            // Subtitle: Select View:
            RectTransform sideSubRt = CreateRect("Subtitle", sideDockRt);
            SetAnchor(sideSubRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(380f, 32f), new Vector2(0f, -68f));
            var sideSubTxt = sideSubRt.gameObject.AddComponent<Text>();
            sideSubTxt.text = "Select View:";
            sideSubTxt.font = defaultFont;
            sideSubTxt.fontSize = 20;
            sideSubTxt.fontStyle = FontStyle.Bold;
            sideSubTxt.alignment = TextAnchor.MiddleCenter;
            sideSubTxt.color = new Color(0.75f, 0.85f, 0.95f);

            // View 1, View 2, View 3 Buttons
            Button view1Btn = CreateActionButton("ViewBtn_0", sideDockRt, "VIEW 1", new Vector2(360f, 54f), new Vector2(0f, 126f), defaultFont, 26, out Text _, new Color(0.12f, 0.55f, 0.95f, 1f));
            Button view2Btn = CreateActionButton("ViewBtn_1", sideDockRt, "VIEW 2", new Vector2(360f, 54f), new Vector2(0f, 60f), defaultFont, 26, out Text _, new Color(0.16f, 0.22f, 0.32f, 0.95f));
            Button view3Btn = CreateActionButton("ViewBtn_2", sideDockRt, "VIEW 3", new Vector2(360f, 54f), new Vector2(0f, -6f), defaultFont, 26, out Text _, new Color(0.16f, 0.22f, 0.32f, 0.95f));

            // Pause / Play Button
            Button playPauseBtn = CreateActionButton("PlayPauseBtn", sideDockRt, "||  PAUSE", new Vector2(360f, 58f), new Vector2(0f, -86f), defaultFont, 26, out Text playPauseTxt, new Color(0.12f, 0.45f, 0.8f, 0.95f));

            // Audio / Mute Button
            Button muteBtn = CreateActionButton("MuteBtn", sideDockRt, "AUDIO: ON", new Vector2(360f, 50f), new Vector2(0f, -152f), defaultFont, 24, out Text muteTxt, new Color(0.16f, 0.22f, 0.32f, 0.95f));

            // Hint Text
            RectTransform hintRt = CreateRect("HintText", sideDockRt);
            SetAnchor(hintRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(380f, 32f), new Vector2(0f, 16f));
            var hintTxt = hintRt.gameObject.AddComponent<Text>();
            hintTxt.text = "Click button or use [A]/[X] switch | [Y] pause";
            hintTxt.font = defaultFont;
            hintTxt.fontSize = 14;
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.color = new Color(0.6f, 0.75f, 0.85f, 0.8f);

            sideDockRt.gameObject.SetActive(true);

            // 4. Loading Indicator Panel (Only appears when video is preparing)
            RectTransform loadRt = CreateRect("LoadingPanel", canvasTransform);
            SetAnchor(loadRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, 80f), Vector2.zero);
            var loadBg = loadRt.gameObject.AddComponent<Image>();
            loadBg.color = new Color(0f, 0f, 0f, 0.85f);

            RectTransform loadTxtRt = CreateRect("LoadingText", loadRt);
            StretchFull(loadTxtRt);
            var loadTxt = loadTxtRt.gameObject.AddComponent<Text>();
            loadTxt.text = "Loading 360 Video...";
            loadTxt.font = defaultFont;
            loadTxt.fontSize = 24;
            loadTxt.alignment = TextAnchor.MiddleCenter;
            loadTxt.color = new Color(0.3f, 0.85f, 1f);
            loadRt.gameObject.SetActive(false);

            // 5. Error Dialog Panel
            RectTransform errRt = CreateRect("ErrorPanel", canvasTransform);
            SetAnchor(errRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(650f, 180f), Vector2.zero);
            var errBg = errRt.gameObject.AddComponent<Image>();
            errBg.color = new Color(0.2f, 0.05f, 0.05f, 0.92f);

            RectTransform errTxtRt = CreateRect("ErrorText", errRt);
            SetAnchor(errTxtRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, 100f), new Vector2(0f, -15f));
            var errTxt = errTxtRt.gameObject.AddComponent<Text>();
            errTxt.text = "Video not found.\nPlease place MP4 files in Assets/StreamingAssets/Videos/";
            errTxt.font = defaultFont;
            errTxt.fontSize = 18;
            errTxt.alignment = TextAnchor.MiddleCenter;
            errTxt.color = new Color(1f, 0.6f, 0.6f);

            Button errDismissBtn = CreateActionButton("ErrorDismissBtn", errRt, "Dismiss", new Vector2(200f, 44f), new Vector2(0f, 18f), defaultFont, 20, out Text _);
            errRt.gameObject.SetActive(false);

            // Wire serialized fields to CampusTourUI
            SetSerializedRef(tourUI, "tourManager", manager);
            SetSerializedRef(tourUI, "videoPlayer", player);
            SetSerializedRef(tourUI, "sideDockPanel", sideDockRt.gameObject);
            SetSerializedRef(tourUI, "view1Button", view1Btn);
            SetSerializedRef(tourUI, "view2Button", view2Btn);
            SetSerializedRef(tourUI, "view3Button", view3Btn);
            SetSerializedRef(tourUI, "playPauseButton", playPauseBtn);
            SetSerializedRef(tourUI, "playPauseText", playPauseTxt);
            SetSerializedRef(tourUI, "muteButton", muteBtn);
            SetSerializedRef(tourUI, "muteText", muteTxt);
            SetSerializedRef(tourUI, "locationToastGroup", toastCg);
            SetSerializedRef(tourUI, "locationToastText", toastTxt);
            SetSerializedRef(tourUI, "loadingPanel", loadRt.gameObject);
            SetSerializedRef(tourUI, "loadingText", loadTxt);
            SetSerializedRef(tourUI, "errorPanel", errRt.gameObject);
            SetSerializedRef(tourUI, "errorText", errTxt);
            SetSerializedRef(tourUI, "errorDismissButton", errDismissBtn);
            SetSerializedRef(tourUI, "fadeOverlay", fadeCg);
        }

        private static RenderTexture GetOrCreateRenderTexture()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                rt = new RenderTexture(3840, 1920, 0, RenderTextureFormat.ARGB32)
                {
                    name = "PanoramaRenderTexture",
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    autoGenerateMips = false
                };
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
                AssetDatabase.SaveAssets();
            }
            return rt;
        }

        private static Material GetOrCreateSkyboxMaterial(RenderTexture rt)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
            Shader shader = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");

            if (mat == null)
            {
                mat = new Material(shader) { name = "CampusTourSkyboxMat" };
                mat.SetTexture("_Tex", rt);
                mat.SetTexture("_MainTex", rt);
                if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0); // 360 Degrees
                if (mat.HasProperty("_Layout")) mat.SetFloat("_Layout", 0); // None / 2D
                if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1); // Latitude Longitude Layout
                AssetDatabase.CreateAsset(mat, SkyboxMatPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                mat.shader = shader;
                mat.SetTexture("_Tex", rt);
                mat.SetTexture("_MainTex", rt);
                if (mat.HasProperty("_ImageType")) mat.SetFloat("_ImageType", 0);
                if (mat.HasProperty("_Layout")) mat.SetFloat("_Layout", 0);
                if (mat.HasProperty("_Mapping")) mat.SetFloat("_Mapping", 1);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetAnchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static Button CreateActionButton(string name, RectTransform parent, string label, Vector2 size, Vector2 pos, Font font, int fontSize, out Text labelText, Color? bgColor = null)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            Image img = btnGo.GetComponent<Image>();
            img.color = bgColor ?? new Color(0.16f, 0.22f, 0.32f, 0.95f);

            Button btn = btnGo.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.25f, 0.68f, 1f, 1f);
            cb.pressedColor = new Color(0.08f, 0.42f, 0.78f, 1f);
            cb.selectedColor = new Color(0.12f, 0.55f, 0.95f, 1f);
            btn.colors = cb;

            BoxCollider box = btnGo.AddComponent<BoxCollider>();
            box.size = new Vector3(size.x, size.y, 2f);

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtGo.transform.SetParent(btnGo.transform, false);

            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            Text txt = txtGo.GetComponent<Text>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            labelText = txt;
            return btn;
        }

        private static void SetSerializedRef(UnityEngine.Object target, string propName, UnityEngine.Object value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int idx = scenes.FindIndex(s => s.path == scenePath);
            if (idx < 0)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            }
            else
            {
                scenes[idx].enabled = true;
                if (idx != 0)
                {
                    var sc = scenes[idx];
                    scenes.RemoveAt(idx);
                    scenes.Insert(0, sc);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
