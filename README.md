# YouTube Converter

A simple, open-source YouTube downloader for Android. Download YouTube videos as MP3 or MP4 directly to your device, with metadata and thumbnail embedding.

## Features

- Download YouTube videos as **MP3** or **MP4**
- Embedded metadata (title, artist) and album art for MP3s
- **Quick download** mode — download without manually picking quality
- **Manual quality selection** — pick audio bitrate or video resolution
- YouTube **search** with thumbnails, duration, and channel name
- **Download history** — tap any past download to re-download it
- Search history
- Custom download destination via folder picker
- Share YouTube links directly from the YouTube app
- Update notifications via GitHub Releases
- Dark theme

## Building

### Requirements

- Android Studio
- Android SDK 26+
- JDK 17+

### FFmpeg AAR

This project uses a custom-built FFmpeg AAR for compatibility with devices using 16KB memory page sizes (e.g. newer Samsung devices). The prebuilt AAR is included in `app/libs/`, but if you need to build it yourself:

1. Clone the fork: [JamaisMagic/ffmpeg-kit-16KB](https://github.com/JamaisMagic/ffmpeg-kit-16KB)
2. Follow the build instructions in that repository for the `android` target, make sure to include libmp3lame and libx264
3. Copy the resulting `ffmpeg-kit.aar` and the two `smart-exception` JARs into `app/libs/`

The prebuilt AAR included in this repository was built from that fork with no modifications.

### Steps

1. Clone this repository
2. Open in Android Studio
3. Sync Gradle
4. Build → Generate Signed APK (or run directly on a device)

## Dependencies

- [NewPipe Extractor](https://github.com/TeamNewPipe/NewPipeExtractor) — YouTube stream extraction
- [FFmpegKit](https://github.com/arthenica/ffmpeg-kit) (custom build) — audio/video processing
- [Coil](https://github.com/coil-kt/coil) — image loading
- [Gson](https://github.com/google/gson) — JSON serialization
- [Jetpack Compose](https://developer.android.com/jetpack/compose) — UI

## License

MIT License — see [LICENSE](LICENSE) for details.
