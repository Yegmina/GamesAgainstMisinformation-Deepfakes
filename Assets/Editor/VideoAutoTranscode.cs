using UnityEditor;
using UnityEngine;

// Ensures every video imported into the project is transcoded to H.264 so it
// plays reliably on Windows (Media Foundation). Without this, raw mp4 files
// from online cutters/exporters often "play" (isPlaying == true) but stay
// frozen on a black first frame. This runs automatically on first import of
// any new video file — just drag a video into the project and it will work.
internal sealed class VideoAutoTranscode : AssetPostprocessor
{
    void OnPreprocessAsset()
    {
        var vci = assetImporter as VideoClipImporter;
        if (vci == null) return;

        // Only adjust on first import (when no meta/import settings exist yet),
        // so we never override choices a user deliberately made later.
        if (!vci.importSettingsMissing) return;

        var def = vci.defaultTargetSettings;
        def.enableTranscoding = true;
        def.codec = VideoCodec.H264;
        def.spatialQuality = VideoSpatialQuality.HighSpatialQuality;
        def.bitrateMode = VideoBitrateMode.High;
        vci.defaultTargetSettings = def;

        Debug.Log("[VideoAutoTranscode] Enabled H.264 transcoding for " + assetPath);
    }
}
