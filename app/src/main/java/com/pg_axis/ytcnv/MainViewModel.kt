package com.pg_axis.ytcnv

import android.app.Application
import android.content.Context
import android.content.Intent
import android.util.Log
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.arthenica.ffmpegkit.FFmpegKit
import com.arthenica.ffmpegkit.ReturnCode
import com.pg_axis.ytcnv.ui.theme.TextSecondary
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.schabi.newpipe.extractor.ServiceList
import org.schabi.newpipe.extractor.stream.AudioStream
import org.schabi.newpipe.extractor.stream.AudioTrackType
import org.schabi.newpipe.extractor.stream.StreamInfo
import org.schabi.newpipe.extractor.stream.VideoStream
import java.io.File
import java.net.URL
import kotlin.math.abs

data class QualityOption(
    val key: Double,
    val displayName: String
)

class MainViewModel(application: Application) : AndroidViewModel(application) {

    val settings: ISettings = try {
        SettingsSave.getInstance(application)
    } catch (_: Exception) {
        PreviewSettings()
    }

    private val isQuick get() = settings.quickDwnld
    private val context get() = getApplication<Application>()

    // ─── URL entry ───
    var urlEntryText by mutableStateOf("")

    // ─── Download options ───
    var downloadOptionsIsVisible by mutableStateOf(true)
    var formatPickerIsEnabled by mutableStateOf(true)
    var qualityPickerIsVisible by mutableStateOf(!isQuick)
    var qualityPickerIsEnabled by mutableStateOf(true)
    var qualityPickerSelectedIndex by mutableIntStateOf(0)
    var qualityPickerItemsSource by mutableStateOf<List<QualityOption>>(emptyList())
    var loadButtonIsVisible by mutableStateOf(!isQuick)
    var loadButtonIsEnabled by mutableStateOf(true)
    var downloadButtonIsVisible by mutableStateOf(isQuick)
    var cancelButtonIsVisible by mutableStateOf(false)

    // ─── Progress / status ───
    var dwnldProgressIsVisible by mutableStateOf(false)
    var downloadProgress by mutableFloatStateOf(0f)
    var downloadIndicatorIsVisible by mutableStateOf(false)
    var statusLabelIsVisible by mutableStateOf(false)
    var statusLabelText by mutableStateOf(AnnotatedString(""))

    // ─── Popup ───
    var popupIsVisible by mutableStateOf(false)
    var popupBackground by mutableStateOf(Color(0xFF2D2D2D))
    var popupTitle by mutableStateOf("")
    var popupMessage by mutableStateOf("")
    var popupButtonText by mutableStateOf("OK")

    // ─── Title/Author dialog ───
    var showTitleAuthorDialog by mutableStateOf(false)
    var dialogTitle by mutableStateOf("")
    var dialogAuthor by mutableStateOf("")

    // ─── Internal state ───
    private var cleanedUrl = ""
    private var audioOptions = mapOf<Double, AudioStream>()
    private var videoOptions = mapOf<Int, VideoStream>()
    private var downloadJob: Job? = null
    private var pendingOnConfirm: ((String, String) -> Unit)? = null
    var updateInfo by mutableStateOf<UpdateInfo?>(null)

    // ─── Actions ───
    suspend fun checkForUpdates(context: Context) {
        val info = UpdateChecker.checkForUpdates(context, settings)
        if (info != null) {
            withContext(Dispatchers.Main) {
                updateInfo = info
            }
        }
    }

    fun onUpdateDialogDismissed(dontShowAgain: Boolean) {
        updateInfo = null
        settings.dontShowUpdate = dontShowAgain
        (settings as? SettingsSave)?.saveSettings()
    }

    fun onUrlChanged(value: String) { urlEntryText = value }
    fun onFormatChanged(index: Int) {
        println("Index: $index, audioOptions: ${audioOptions.size}, videoOptions: ${videoOptions.size}")
        formatPickerSelectedIndex = index
        if (audioOptions.isNotEmpty() && videoOptions.isNotEmpty()) {
            qualityPickerItemsSource = when (index) {
                0 -> audioOptions.entries.map { QualityOption(it.key, "${it.key.toInt()} kbps") }
                1 -> videoOptions.entries.map { QualityOption(it.key.toDouble(), "${it.key}p") }
                else -> emptyList()
            }
            qualityPickerSelectedIndex = 0
        }
    }
    fun onQualityChanged(index: Int) { qualityPickerSelectedIndex = index }
    fun onClosePopupClicked() { popupIsVisible = false }
    fun onHistoryItemTapped(urlOrId: String) { urlEntryText = urlOrId }

    var formatPickerSelectedIndex by mutableIntStateOf(0)

    // ─── Load metadata ───
    fun onLoadClicked() {
        viewModelScope.launch {
            loadVideoMetadata()
        }
    }

    private suspend fun loadVideoMetadata() = withContext(Dispatchers.IO) {
        settings.isDownloadRunning = true
        withContext(Dispatchers.Main) {
            loadButtonIsEnabled = false
            statusLabelIsVisible = false
            downloadIndicatorIsVisible = true
            statusLabelIsVisible = true
            statusLabelText = AnnotatedString("Retrieving video metadata")
        }

        cleanedUrl = StringUtils.cleanUrl(urlEntryText)

        if (cleanedUrl.isBlank()) {
            withContext(Dispatchers.Main) {
                showPopup("No URL", "Please enter a YouTube URL", 2)
                applyQuickDownloadState()
            }
            settings.isDownloadRunning = false
            return@withContext
        }

        try {
            val streamInfo = StreamInfo.getInfo(
                ServiceList.YouTube,
                "https://www.youtube.com/watch?v=$cleanedUrl"
            )

            streamInfo.audioStreams.forEach { stream ->
                println("=== AUDIO STREAM: format=${stream.format?.name}, averageBitrate=${stream.averageBitrate}")
            }

            streamInfo.videoStreams.forEach { stream ->
                println("=== MUXED STREAM: format=${stream.format?.name}, height=${stream.height}")
            }

            streamInfo.videoOnlyStreams.forEach { stream ->
                println("=== VIDEO STREAM: format=${stream.format?.name}, height=${stream.height}")
            }

            // Build audio options
            var audioStreams = streamInfo.audioStreams
                .filter { it.format?.name?.contains("m4a", ignoreCase = true) == true && it.averageBitrate > 0 && (it.audioTrackType == AudioTrackType.ORIGINAL || it.audioTrackType == null) }
                .sortedByDescending { it.averageBitrate }
                .distinctBy { it.averageBitrate }

            if (audioStreams.isEmpty()) {
                audioStreams = streamInfo.audioStreams
                    .filter { it.averageBitrate > 0 && (it.audioTrackType == AudioTrackType.ORIGINAL || it.audioTrackType == null) }
                    .sortedByDescending { it.averageBitrate }
                    .distinctBy { it.averageBitrate }
            }

            audioOptions = audioStreams.associateBy { it.averageBitrate.toDouble() }

            // Build video options
            val use4K = settings.use4K
            val videoStreams = streamInfo.videoOnlyStreams
                .filter { stream ->
                    if (use4K) true
                    else stream.height in 1..1080 && stream.format?.name?.contains("mpeg-4", ignoreCase = true) == true
                }
                .sortedByDescending { it.height }
                .distinctBy { it.height }

            videoOptions = videoStreams.associateBy { it.height }

            withContext(Dispatchers.Main) {
                loadButtonIsVisible = false
                loadButtonIsEnabled = true
                downloadIndicatorIsVisible = false
                statusLabelIsVisible = false
                downloadOptionsIsVisible = true
                qualityPickerIsVisible = true
                formatPickerSelectedIndex = 0
                qualityPickerItemsSource = audioOptions.entries.map { QualityOption(it.key, "${it.key.toInt()} kbps") }
                qualityPickerSelectedIndex = 0
                downloadButtonIsVisible = true
                cancelButtonIsVisible = false
            }
        } catch (e: Exception) {
            withContext(Dispatchers.Main) {
                applyQuickDownloadState()
                when {
                    e.message?.contains("403") == true || e.message?.contains("404") == true ->
                        showPopup("Video unavailable", "The video is private, age-restricted, or does not exist.", 2)
                    else -> showPopup("Error", e.message ?: "Unknown error", 2)
                }
            }
        } finally {
            settings.isDownloadRunning = false
        }
    }

    // ─── Download ───
    fun onDownloadClicked() {
        downloadJob = viewModelScope.launch {
            startDownload()
        }
    }

    private suspend fun startDownload() = withContext(Dispatchers.IO) {
        settings.isDownloadRunning = true

        withContext(Dispatchers.Main) {
            formatPickerIsEnabled = false
            qualityPickerIsEnabled = false
            downloadButtonIsVisible = false
            cancelButtonIsVisible = true
            downloadIndicatorIsVisible = true
            statusLabelIsVisible = true
            statusLabelText = AnnotatedString("Retrieving video metadata")
        }

        if (isQuick) cleanedUrl = StringUtils.cleanUrl(urlEntryText)

        if (cleanedUrl.isBlank()) {
            withContext(Dispatchers.Main) {
                applyQuickDownloadState()
                showPopup("No URL", "Please enter a YouTube URL", 2)
            }
            settings.isDownloadRunning = false
            return@withContext
        }

        val m4aPath = File(context.cacheDir, "audio.m4a").absolutePath
        val mp4Path = File(context.cacheDir, "video.mp4").absolutePath
        val semiOutput = File(context.filesDir, "semi-outputVideo.mp4").absolutePath
        val semiOutputAudio = File(context.filesDir, "semi-outputAudio.mp3").absolutePath
        val imagePath = File(context.cacheDir, "thumbnail.jpg").absolutePath

        fun deleteFiles() {
            listOf(m4aPath, mp4Path, semiOutput, semiOutputAudio, imagePath)
                .forEach { if (File(it).exists()) File(it).delete() }
        }

        try {
            // Start foreground service
            DownloadNotificationService.setProgressType(false)
            withContext(Dispatchers.Main) {
                context.startForegroundService(
                    Intent(context, DownloadNotificationService::class.java)
                )
            }

            val streamInfo = StreamInfo.getInfo(
                ServiceList.YouTube,
                "https://www.youtube.com/watch?v=$cleanedUrl"
            )

            if (streamInfo.duration <= 0) {
                withContext(Dispatchers.Main) {
                    applyQuickDownloadState()
                    showPopup("Live stream", "Live streams can't be downloaded.")
                }
                stopService()
                settings.isDownloadRunning = false
                DownloadNotificationService.setProgressType(false)
                return@withContext
            }

            var author = StringUtils.cleanAuthor(streamInfo.uploaderName ?: "")
            val (cleanedTitle, cleanedAuthor) = StringUtils.cleanTitle(streamInfo.name ?: "YouTube_Video", author)
            author = cleanedAuthor
            var title = cleanedTitle
            val unalteredTitle = streamInfo.name?.trim() ?: title

            // Update history
            withContext(Dispatchers.Main) {
                val existing = settings.downloadHistory.find { it.urlOrId == cleanedUrl }
                val newItem = SettingsSave.HistoryItem(unalteredTitle, cleanedUrl)
                val updated = settings.downloadHistory.toMutableList()
                updated.remove(existing)
                updated.add(0, newItem)
                settings.downloadHistory = updated
                (settings as? SettingsSave)?.saveExtraData()
            }

            // Download thumbnail
            val thumbnailUrl = streamInfo.thumbnails.maxByOrNull { it.height }?.url
            var hasThumbnail = false
            if (thumbnailUrl != null) {
                val bytes = URL(thumbnailUrl).readBytes()
                val ext = detectImageExtension(bytes)
                val tempThumbnail = File(context.cacheDir, "tempThumbnail.$ext").absolutePath
                File(tempThumbnail).writeBytes(bytes)

                val ffmpegComd = "-y -i \"$tempThumbnail\" -frames:v 1 \"$imagePath\""
                Log.d("FFmpegCommand", ffmpegComd)
                val result = runFFmpeg(ffmpegComd)

                hasThumbnail = result && File(imagePath).exists()

                if (File(tempThumbnail).exists()) File(tempThumbnail).delete()
            }

            // Show title/author dialog and wait for result
            val (confirmedTitle, confirmedAuthor) = withContext(Dispatchers.Main) {
                kotlinx.coroutines.suspendCancellableCoroutine { cont ->
                    dialogTitle = title
                    dialogAuthor = author
                    showTitleAuthorDialog = true
                    pendingOnConfirm = { t, a ->
                        showTitleAuthorDialog = false
                        cont.resume(Pair(t, a)) {}
                    }
                    cont.invokeOnCancellation {
                        showTitleAuthorDialog = false
                    }
                }
            }
            title = StringUtils.cleanTitle(confirmedTitle, confirmedAuthor).first
            author = StringUtils.cleanAuthor(confirmedAuthor)

            withContext(Dispatchers.Main) {
                statusLabelText = buildAnnotatedString {
                    append("Downloading ")
                    withStyle(SpanStyle(fontWeight = FontWeight.Bold, color = TextSecondary)) {
                        append(title)
                    }
                }
                downloadIndicatorIsVisible = false
                dwnldProgressIsVisible = true
                DownloadNotificationService.setProgressType(true)
            }

            val selectedFormat = formatPickerSelectedIndex
            var lastNotifiedPercent = -1

            if (selectedFormat == 0) {
                // ─── MP3 ───
                val audioStream = if (isQuick) {
                    streamInfo.audioStreams
                        .filter { it.format?.name?.contains("m4a", ignoreCase = true) == true && (it.audioTrackType == AudioTrackType.ORIGINAL || it.audioTrackType == null) }
                        .maxByOrNull { it.averageBitrate }
                        ?: streamInfo.audioStreams.maxByOrNull { it.averageBitrate }
                } else {
                    val selectedBitrate = audioOptions.entries.elementAtOrNull(qualityPickerSelectedIndex)?.key
                    if (selectedBitrate != null) {
                        streamInfo.audioStreams
                            .filter { it.format?.name?.contains("m4a", ignoreCase = true) == true && (it.audioTrackType == AudioTrackType.ORIGINAL || it.audioTrackType == null) }
                            .minByOrNull { abs(it.averageBitrate - selectedBitrate.toInt()) }
                    } else null
                } ?: streamInfo.audioStreams.maxByOrNull { it.averageBitrate }!!

                downloadStream(audioStream.content, m4aPath) { progress ->
                    downloadProgress = progress
                    val percent = (progress * 100).toInt()
                    if (percent != lastNotifiedPercent) {
                        lastNotifiedPercent = percent
                        DownloadNotificationService.updateProgress(
                            context,
                            percent
                        )
                    }
                }

                withContext(Dispatchers.Main) {
                    dwnldProgressIsVisible = false
                    DownloadNotificationService.setProgressType(false)
                    downloadIndicatorIsVisible = true
                    statusLabelText = AnnotatedString("Adding metadata")
                }

                //FileSaver.saveM4a(context, "$title.m4a", m4aPath, settings.fileUri.ifBlank { null })  YTmI9rP5YUw

                val ffmpegCmd = buildString {
                    append("-y -i \"$m4aPath\" ")
                    if (hasThumbnail) {
                        append("-i \"$imagePath\" ")
                    }
                    append("-map 0:a ")
                    if (hasThumbnail) {
                        append("-map 1:v ")
                    }
                    append("-c:a libmp3lame -b:a 128k -ac 2 -af anull ")
                    if (hasThumbnail) {
                        append("-c:v mjpeg -disposition:v attached_pic ")
                        append("-metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover\" ")
                    }
                    append("-metadata title=\"$title\" -metadata artist=\"$author\" ")
                    append("-threads 1 \"$semiOutputAudio\"")
                }

                Log.d("FFmpegCommand", ffmpegCmd)
                val ffmpegResult = runFFmpeg(ffmpegCmd)

                if (ffmpegResult) {
                    FileSaver.saveAudio(context, "$title.mp3", semiOutputAudio, settings.fileUri.ifBlank { null })
                    if (settings.notifyOnFinish) {
                        DownloadNotificationService.showFinishNotification(context, "$title.mp3")
                    }
                    withContext(Dispatchers.Main) {
                        applyQuickDownloadState()
                        showPopup("Finished", "The download has completed successfully.", 1)
                    }
                } else {
                    withContext(Dispatchers.Main) {
                        applyQuickDownloadState()
                        showPopup("Failed", "The app failed to add metadata and save the file.", 2)
                        if (settings.notifyOnFail) {
                            DownloadNotificationService.showFailedNotification(context, "The app failed to add metadata and save the file.")
                        }
                    }
                }

            } else {
                // ─── MP4 ───
                val audioStream = streamInfo.audioStreams.filter{ it.audioTrackType == AudioTrackType.ORIGINAL || it.audioTrackType == null }.maxByOrNull{ it.averageBitrate } ?: streamInfo.audioStreams.maxByOrNull { it.averageBitrate }!!
                val videoStream = if (isQuick) {
                    if (settings.use4K)
                        streamInfo.videoOnlyStreams.maxByOrNull { it.height }
                    else
                        streamInfo.videoOnlyStreams
                            .filter { it.height <= 1080 &&
                                    it.format?.name?.contains("mpeg-4", ignoreCase = true) == true }
                            .maxByOrNull { it.height }
                } else {
                    val selectedHeight = videoOptions.entries.elementAtOrNull(qualityPickerSelectedIndex)?.key
                    if (selectedHeight != null) {
                        streamInfo.videoOnlyStreams
                            .filter { it.format?.name?.contains("mpeg-4", ignoreCase = true) == true || settings.use4K }
                            .firstOrNull { it.height == selectedHeight }
                    } else null
                } ?: streamInfo.videoOnlyStreams.maxByOrNull { it.height }!!

                val isMoreThan1080p = videoStream.height > 1080

                // Download audio and video in parallel
                withContext(Dispatchers.IO) {
                    val audioJob = launch { downloadStream(audioStream.content, m4aPath) {} }
                    val videoJob = launch {
                        downloadStream(videoStream.content, mp4Path) { progress ->
                            downloadProgress = progress
                            val percent = (progress * 100).toInt()
                            if (percent != lastNotifiedPercent) {
                                lastNotifiedPercent = percent
                                DownloadNotificationService.updateProgress(
                                    context,
                                    percent
                                )
                            }
                        }
                    }
                    audioJob.join()
                    videoJob.join()
                }

                withContext(Dispatchers.Main) {
                    dwnldProgressIsVisible = false
                    DownloadNotificationService.setProgressType(false)
                    downloadIndicatorIsVisible = true
                    statusLabelText = AnnotatedString("Joining audio and video")
                }

                //FileSaver.saveM4a(context, "$title.m4a", m4aPath, settings.fileUri.ifBlank { null })

                val ffmpegArgs = if (settings.use4K && isMoreThan1080p) {
                    "-y -i \"$mp4Path\" -i \"$m4aPath\" -c:v libx264 -pix_fmt yuv420p -preset superfast -crf 23 " +
                            "-c:a copy -map 0:v:0 -map 1:a:0 -shortest " +
                            "-metadata title=\"$title\" -metadata artist=\"$author\" \"$semiOutput\""
                } else {
                    "-y -i \"$mp4Path\" -i \"$m4aPath\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest " +
                            "-metadata title=\"$title\" -metadata artist=\"$author\" \"$semiOutput\""
                }

                Log.d("FFmpegCommand", ffmpegArgs)
                val ffmpegResult = runFFmpeg(ffmpegArgs)

                if (ffmpegResult) {
                    FileSaver.saveVideo(context, "$title.mp4", semiOutput, settings.fileUri.ifBlank { null })
                    if (settings.notifyOnFinish) {
                        DownloadNotificationService.showFinishNotification(context, "$title.mp4")
                    }
                    withContext(Dispatchers.Main) {
                        applyQuickDownloadState()
                        showPopup("Finished", "The download has completed successfully.", 1)
                    }
                } else {
                    withContext(Dispatchers.Main) {
                        applyQuickDownloadState()
                        showPopup("Failed", "The app failed to process and save the file.", 2)
                        if (settings.notifyOnFail) {
                            DownloadNotificationService.showFailedNotification(context, "The app failed to add metadata and save the file.")
                        }
                    }
                }
            }

        } catch (_: kotlinx.coroutines.CancellationException) {
            FFmpegKit.cancel()
            deleteFiles()
            stopService()
            DownloadNotificationService.setProgressType(false)
            withContext(Dispatchers.Main) {
                applyQuickDownloadState()
                showPopup("Cancelled", "The download was cancelled.", 0)
            }
        } catch (e: Exception) {
            FFmpegKit.cancel()
            deleteFiles()
            stopService()
            DownloadNotificationService.setProgressType(false)
            withContext(Dispatchers.Main) {
                applyQuickDownloadState()
                when {
                    e.message?.contains("403") == true || e.message?.contains("404") == true -> {
                        showPopup("Video unavailable", "The video is private, age-restricted, or does not exist.", 2)
                        if (settings.notifyOnFail) {
                            DownloadNotificationService.showFailedNotification(context, "The video is private, age-restricted, or does not exist.")
                        }
                    }
                    e.message?.contains("ID or URL") == true -> {
                        showPopup("Invalid URL", "Please enter a valid YouTube URL", 2)
                        if (settings.notifyOnFail) {
                            DownloadNotificationService.showFailedNotification(context, "Please enter a valid YouTube URL")
                        }
                    }
                    e.message?.contains("Software caused connection abort") == true -> {
                        showPopup("Network error", "The device disconnected from the internet", 2)
                        if (settings.notifyOnFail) {
                            DownloadNotificationService.showFailedNotification(context, "The device disconnected from the internet")
                        }
                    }
                    else -> {
                        showPopup("Error", e.message ?: "Unknown error", 2)
                        if (settings.notifyOnFail) {
                            DownloadNotificationService.showFailedNotification(context, "Unknown error")
                        }
                    }
                }
            }
        } finally {
            deleteFiles()
            stopService()
            DownloadNotificationService.setProgressType(false)
            settings.isDownloadRunning = false
        }
    }

    // ─── Cancel ───
    fun onCancelClicked() {
        FFmpegKit.cancel()
        downloadJob?.cancel()
        applyQuickDownloadState()
        settings.isDownloadRunning = false
    }

    // ─── Dialog confirm ───
    fun onTitleAuthorConfirmed(title: String, author: String) {
        pendingOnConfirm?.invoke(title, author)
        pendingOnConfirm = null
    }

    fun onTitleAuthorDismissed() {
        showTitleAuthorDialog = false
        downloadJob?.cancel()
        applyQuickDownloadState()
    }

    // ─── Helpers ───
    fun applyQuickDownloadState() {
        val quick = settings.quickDwnld
        downloadOptionsIsVisible = true
        formatPickerIsEnabled = true
        qualityPickerIsVisible = !quick
        qualityPickerIsEnabled = true
        qualityPickerSelectedIndex = 0
        loadButtonIsVisible = !quick
        loadButtonIsEnabled = true
        downloadButtonIsVisible = quick
        cancelButtonIsVisible = false
        dwnldProgressIsVisible = false
        downloadIndicatorIsVisible = false
        statusLabelIsVisible = false
    }

    fun showPopup(title: String, message: String, type: Int = 0, buttonText: String = "OK") {
        popupBackground = when (type) {
            1 -> Color(0xFF1B5E20)  // success green
            2 -> Color(0xFF7F0000)  // error red
            else -> Color(0xFF1E3A4A) // default
        }
        popupTitle = title
        popupMessage = message
        popupButtonText = buttonText
        popupIsVisible = true
    }

    private fun stopService() {
        context.stopService(Intent(context, DownloadNotificationService::class.java))
    }

    private suspend fun downloadStream(
        url: String,
        outputPath: String,
        onProgress: (Float) -> Unit
    ) = withContext(Dispatchers.IO) {
        val connection = URL(url).openConnection() as java.net.HttpURLConnection
        connection.connect()
        val totalBytes = connection.contentLengthLong
        var downloadedBytes = 0L
        val buffer = ByteArray(8192)
        File(outputPath).outputStream().use { out ->
            connection.inputStream.use { input ->
                var bytes = input.read(buffer)
                while (bytes >= 0) {
                    out.write(buffer, 0, bytes)
                    downloadedBytes += bytes
                    if (totalBytes > 0) {
                        withContext(Dispatchers.Main) {
                            onProgress(downloadedBytes.toFloat() / totalBytes.toFloat())
                        }
                    }
                    bytes = input.read(buffer)
                }
            }
        }
    }

    fun detectImageExtension(bytes: ByteArray): String {
        return when {
            bytes.size >= 3 &&
                    bytes[0] == 0xFF.toByte() &&
                    bytes[1] == 0xD8.toByte() &&
                    bytes[2] == 0xFF.toByte() -> "jpg"

            bytes.size >= 4 &&
                    bytes[0] == 0x89.toByte() &&
                    bytes[1] == 0x50.toByte() &&  // P
                    bytes[2] == 0x4E.toByte() &&  // N
                    bytes[3] == 0x47.toByte() -> "png" // G

            bytes.size >= 12 &&
                    bytes[0] == 0x52.toByte() &&  // R
                    bytes[1] == 0x49.toByte() &&  // I
                    bytes[2] == 0x46.toByte() &&  // F
                    bytes[3] == 0x46.toByte() &&  // F
                    bytes[8] == 0x57.toByte() &&  // W
                    bytes[9] == 0x45.toByte() &&  // E
                    bytes[10] == 0x42.toByte() && // B
                    bytes[11] == 0x50.toByte() -> "webp" // P

            else -> "jpg" // fallback
        }
    }

    private suspend fun runFFmpeg(command: String): Boolean =
        kotlinx.coroutines.suspendCancellableCoroutine { cont ->
            val session = FFmpegKit.executeAsync(command) { session ->
                cont.resume(ReturnCode.isSuccess(session.returnCode)) {}
            }
            cont.invokeOnCancellation { session.cancel() }
        }
}