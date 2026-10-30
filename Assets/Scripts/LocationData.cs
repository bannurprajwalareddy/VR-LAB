using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public enum HotspotType
    {
        Navigation,
        Information
    }

    [Serializable]
    public class HotspotData
    {
        [Tooltip("Display title or label for this hotspot")]
        public string label = "New Hotspot";

        [Tooltip("Horizontal angle in degrees around the viewer (-180 to 180)")]
        [Range(-180f, 180f)]
        public float yaw = 0f;

        [Tooltip("Vertical angle in degrees above/below horizon (-60 to 60)")]
        [Range(-60f, 60f)]
        public float pitch = 0f;

        [Tooltip("Distance in meters from the viewer")]
        [Range(1.5f, 10f)]
        public float distance = 3.5f;

        [Tooltip("Action performed when this hotspot is activated")]
        public HotspotType hotspotType = HotspotType.Navigation;

        [Tooltip("Target index in CampusTourManager.locations to navigate to (if Navigation)")]
        public int targetLocationIndex = 0;

        [Tooltip("Descriptive text displayed in the info popup (if Information)")]
        [TextArea(2, 5)]
        public string description = "Information about this campus landmark.";
    }

    [Serializable]
    public class LocationData
    {
        [Tooltip("Human-readable name of the campus location")]
        public string locationName = "Campus View";

        [Tooltip("MP4 filename located inside Assets/StreamingAssets/Videos/ (e.g. Gate.mp4)")]
        public string videoFileName = "Gate.mp4";

        [Tooltip("Brief description of what is visible in this 360-degree view")]
        [TextArea(2, 5)]
        public string description = "Welcome to this section of the campus.";

        [Tooltip("Interactive hotspots placed within this 360 view")]
        public List<HotspotData> hotspots = new List<HotspotData>();

        [Tooltip("Optional preview thumbnail for 2D UI lists or menus")]
        public Sprite thumbnail;
    }
}
