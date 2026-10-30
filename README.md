# Campus VR Tour — Meta Quest Standalone VR (Unity 6.3)

A fully automated, production-ready 360-degree immersive Virtual Reality Campus Tour application built for **Meta Quest standalone headsets** (Meta Quest 2, Meta Quest 3, Meta Quest 3S, and Meta Quest Pro) using **Unity 6.3 (6000.3.21f1)**, **Universal Render Pipeline (URP)**, **OpenXR**, and the **Unity Input System**.

---

## 1. Project Overview

The Campus VR Tour immerses students, visitors, and faculty inside high-resolution 360-degree equirectangular panoramic video captures of four primary campus locations:
1. **Main Gate** (`Gate.mp4`)
2. **Library** (`Library.mp4`)
3. **Canteen** (`Canteen.mp4`)
4. **Lab Block** (`Lab.mp4`)

### Key Features
- **Standalone Android APK**: Self-contained installation running natively on Quest hardware via OpenXR and IL2CPP ARM64.
- **Full 6-DoF / 3-DoF Head Tracking**: OpenXR-driven panoramic viewing where the viewer looks naturally in every direction.
- **Meta Quest Touch Controller Input**: Complete joystick and button navigation (snap turn, location switching, ray interaction, play/pause, mute/unmute).
- **Interactive Spherical Hotspots**: 3D spatial markers positioned by yaw and pitch angles around the viewer for instant teleportation to other locations or displaying informative popup panels.
- **Dual-Mode Editor Preview**: Non-VR testing in the Unity Editor using mouse drag to look around and mouse clicks to interact—no VR headset required for testing and editing.
- **Smooth 0.5s Transitions**: Fade-to-black scene transitions prevent motion sickness and avoid audio/video overlap.
- **Robust Error Recovery**: Automatically catches missing or unsupported video formats, keeps the UI fully functional, and directs the user to place clips in `Assets/StreamingAssets/Videos/`.
- **One-Click Automated Scene Builder & APK Builder**: Idempotently generates the entire scene, configures materials, wires all serialized references, and compiles the APK.

---

## 2. Requirements & Compatibility

| Component | Specification |
| :--- | :--- |
| **Unity Version** | `6000.3.21f1` (Unity 6.3 LTS) |
| **Target Platform** | Android (ARM64, IL2CPP) |
| **Minimum Android SDK** | API Level 29 (Android 10.0) |
| **Target Android SDK** | API Level 34+ (Automatic) |
| **Graphics API** | Vulkan / OpenGLES3, Linear Color Space |
| **Active Render Pipeline**| Universal Render Pipeline (URP 17.3.0) |
| **XR Runtime** | OpenXR (`com.unity.xr.openxr` 1.16.1) |
| **XR Plugin Management** | `com.unity.xr.management` 4.5.1 |
| **Input System** | Unity Input System 1.20.0 (New Input System handler) |
| **Target Headsets** | Meta Quest 2, Meta Quest 3, Meta Quest 3S, Meta Quest Pro |

---

## 3. Project Structure

```text
Building_Blocks/
├── Assets/
│   ├── CampusTour/
│   │   ├── CampusTourSkybox.mat             # Panoramic equirectangular skybox material
│   │   └── CampusTourVideo.renderTexture     # 3840x1920 360-degree render target
│   ├── Editor/
│   │   ├── CampusTourSceneBuilder.cs        # One-click scene generator (Tools > Campus VR Tour > Build Scene)
│   │   └── QuestBuildHelper.cs              # Project validator & APK builder
│   ├── Plugins/
│   │   └── Android/
│   │       └── AndroidManifest.xml          # Meta Quest immersive VR manifest & device flags
│   ├── Scenes/
│   │   ├── CampusTour.unity                 # Main interactive VR tour scene
│   │   └── SampleScene.unity                # Original default scene
│   ├── Scripts/
│   │   ├── CampusTourManager.cs             # Tour state machine, transitions, hotspot lifecycle
│   │   ├── CampusTourUI.cs                  # World-space VR canvas controller
│   │   ├── HotspotController.cs             # Spherical coordinate positioning & billboard ray interaction
│   │   ├── LocationData.cs                  # Serializable data models for locations & hotspots
│   │   ├── PanoramaVideoPlayer.cs           # URP 360 VideoPlayer, audio, StreamingAssets URL resolver
│   │   └── TourInputController.cs           # Meta Quest Touch controller & Editor mouse input
│   ├── StreamingAssets/
│   │   └── Videos/
│   │       ├── Gate.mp4                     # View 1: Main Gate 360 video
│   │       ├── Library.mp4                  # View 2: Library 360 video
│   │       ├── Canteen.mp4                  # View 3: Canteen 360 video
│   │       └── Lab.mp4                      # View 4: Lab Block 360 video
│   └── XR/                                  # OpenXR and XRGeneralSettings configuration assets
├── Builds/
│   └── Android/
│       └── CampusVRTour.apk                 # Generated standalone Meta Quest APK
├── Packages/
│   └── manifest.json                        # URP, OpenXR, XR Management, and Input System dependencies
├── ProjectSettings/                         # Project settings (PlayerSettings, Quality, Tags)
└── README.md                                # This document
```

---

## 4. How to Add or Replace 360-Degree Videos

1. Place your equirectangular 360° `.mp4` video files into:
   ```text
   Assets/StreamingAssets/Videos/
   ```
2. The four default filenames mapped in the Inspector are:
   - `Gate.mp4` (Main Gate)
   - `Library.mp4` (Library)
   - `Canteen.mp4` (Canteen)
   - `Lab.mp4` (Lab Block)
3. **Adding Custom Locations**:
   - In the Unity Inspector, select `CampusTourManager` in the `CampusTour.unity` scene.
   - Expand the **Locations** list.
   - Increase the size or add a new entry (e.g. `Playground`, video file: `Playground.mp4`).
   - The UI navigation menu automatically creates buttons for any added locations!

---

## 5. Meta Quest Touch Controller & Keyboard Bindings

### Left Touch Controller
| Input | Action |
| :--- | :--- |
| **Left Joystick (Up / Down)** | Move through the campus location menu / previous & next location |
| **Left Joystick (Left / Right)** | Cycle through campus locations |
| **Left Primary Button (X)** | Confirm / Recenter orientation |

### Right Touch Controller
| Input | Action |
| :--- | :--- |
| **Right Joystick (Left / Right)**| 45° Snap Turn (or smooth rotation) |
| **Right Controller Ray + Trigger**| Aim laser pointer and click UI buttons or spatial hotspots |
| **Right Primary Button (A)** | Play / Pause 360 video playback |
| **Right Secondary Button (B)** | Mute / Unmute video audio |

### Unity Editor Non-VR Preview Controls
| Input | Action |
| :--- | :--- |
| **Mouse Left Drag** | Look around 360° (pitch and yaw) |
| **Mouse Left Click** | Click UI buttons and spatial hotspots |
| **Spacebar** | Toggle Play / Pause |
| **M Key** | Toggle Mute / Unmute |
| **Tab / H Key** | Toggle Navigation Menu visibility |
| **1 / 2 / 3 / 4 Keys** | Jump directly to View 1, 2, 3, or 4 |
| **Left / Right Arrow (or A / D)**| Previous / Next location |

---

## 6. How to Build the Scene and APK

### Inside Unity Editor:
1. **Build or Rebuild Scene**:
   Go to menu: **Tools > Campus VR Tour > Build Scene**
   *(Creates or repairs `Assets/Scenes/CampusTour.unity`, connects all components, and configures the Build Settings).*
2. **Validate Project Configuration**:
   Go to menu: **Tools > Campus VR Tour > Validate Quest Build**
   *(Verifies Android platform, IL2CPP, ARM64, API 29+, scene index 0, and video files).*
3. **Build APK**:
   Go to menu: **Tools > Campus VR Tour > Build Quest APK**
   *(Compiles IL2CPP ARM64 binary and outputs to `Builds/Android/CampusVRTour.apk`).*

### From Command Line / Terminal:
To trigger a clean automated build from PowerShell:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -projectPath "C:\Users\INDU\Building_Blocks" -quit -batchmode -executeMethod CampusTour.QuestBuildHelper.BuildQuestApkCommandLine -logFile "Builds/Android/build.log"
```

---

## 7. Installing the APK on Meta Quest

### Prerequisites:
1. **Enable Developer Mode** on your Meta Quest:
   - Open the **Meta Quest Mobile App** on your smartphone.
   - Go to **Menu > Devices > Headset Settings > Developer Mode** and toggle it **ON**.
2. **Connect Quest to PC via USB-C**:
   - Put on the headset and select **"Allow USB Debugging"** and check **"Always allow from this computer"**.

### Installing via ADB (Android Debug Bridge):
Run this command from your terminal:
```powershell
# Using Unity's bundled ADB:
& "C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r "Builds/Android/CampusVRTour.apk"
```
*(Or use standard `adb install -r Builds/Android/CampusVRTour.apk` if ADB is in your PATH).*

### Launching the Application on Quest:
1. Inside your Quest headset, open the **App Library**.
2. In the top-right search/filter dropdown, switch from *All* to **Unknown Sources**.
3. Select **Campus VR Tour** to launch into the 360° campus experience!

---

## 8. Troubleshooting Guide

### 1. "Video not found" or Red Error Banner
- **Cause**: The MP4 video file specified for the location does not match the filename in `Assets/StreamingAssets/Videos/`.
- **Fix**: Check that the filename in `Assets/StreamingAssets/Videos/` matches exactly (e.g. `Gate.mp4`, case-sensitive on Android). The on-screen banner will display the exact name being searched.

### 2. Black Screen on Headset
- **Cause**: Camera clear flags or video texture hasn't prepared yet.
- **Fix**: The application includes a 0.5s fade overlay. If preparation times out after 12 seconds, it automatically fades back in and shows an error banner so the user is never stranded on a black screen. Ensure the skybox material uses `Skybox/Panoramic`.

### 3. Video Playback Stuttering or Frame Drops on Quest
- **Cause**: High-bitrate 5.7K / 8K raw video files overload the mobile Snapdragon XR2 decoder.
- **Optimization**: Re-encode equirectangular clips using HandBrake or `ffmpeg` with H.264 or H.265:
  ```bash
  # Recommended 4K 60fps Quest encode:
  ffmpeg -i input_360.mp4 -vf scale=3840:1920 -c:v libx264 -b:v 25M -maxrate 30M -bufsize 50M -pix_fmt yuv420p -c:a aac -b:a 192k Gate.mp4
  ```

### 4. Controller Ray Not Interacting with UI
- **Cause**: The EventSystem requires `InputSystemUIInputModule` with pointer support enabled.
- **Fix**: Run **Tools > Campus VR Tour > Build Scene**; this automatically replaces legacy `StandaloneInputModule` with `InputSystemUIInputModule` and adds world-space raycasting colliders.

### 5. XR Initialization Failure
- **Cause**: OpenXR loader not active for Android build target.
- **Fix**: Verify that `Assets/XR/Loaders/OpenXRLoader.asset` exists and the Meta Quest support feature group is enabled in OpenXR settings. Run **Tools > Campus VR Tour > Configure Quest Settings**.

---

*Project prepared and automated for Meta Quest Standalone VR.*
