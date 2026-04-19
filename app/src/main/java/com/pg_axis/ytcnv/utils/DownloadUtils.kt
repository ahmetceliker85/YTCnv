package com.pg_axis.ytcnv.utils

import com.arthenica.ffmpegkit.FFmpegKit
import com.arthenica.ffmpegkit.ReturnCode
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.joinAll
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.IOException
import java.io.RandomAccessFile
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.atomic.AtomicLong
import kotlin.concurrent.atomics.ExperimentalAtomicApi

object DownloadUtils {
    @OptIn(ExperimentalAtomicApi::class)
    suspend fun downloadStream(
        url: String,
        outputPath: String,
        onProgress: (Float) -> Unit,
        urlRefresher: (suspend () -> String)? = null
    ) = withContext(Dispatchers.IO) {
        val chunkCount = 4
        val maxRetries = 6

        fun fetchTotalBytes(streamUrl: String): Long {
            val conn = URL(streamUrl).openConnection() as HttpURLConnection
            conn.setRequestProperty("Range", "bytes=0-0")
            conn.connect()
            val contentRange = conn.getHeaderField("Content-Range")
            conn.disconnect()
            return contentRange?.substringAfterLast('/')?.trim()?.toLongOrNull()
                ?: conn.contentLengthLong
        }

        suspend fun downloadChunk(
            streamUrl: String,
            file: RandomAccessFile,
            start: Long,
            end: Long,
            downloaded: AtomicLong,
            totalBytes: Long
        ) {
            var retries = 0
            var currentUrl = streamUrl
            while (true) {
                try {
                    val conn = URL(currentUrl).openConnection() as HttpURLConnection
                    conn.setRequestProperty("Range", "bytes=$start-$end")
                    conn.connect()
                    val buffer = ByteArray(32768)
                    var position = start
                    conn.inputStream.use { input ->
                        var bytes = input.read(buffer)
                        while (bytes >= 0) {
                            synchronized(file) {
                                file.seek(position)
                                file.write(buffer, 0, bytes)
                            }
                            position += bytes
                            val total = downloaded.addAndGet(bytes.toLong())
                            withContext(Dispatchers.Main) {
                                onProgress(total.toFloat() / totalBytes.toFloat())
                            }
                            bytes = input.read(buffer)
                        }
                    }
                    return
                } catch (e: IOException) {
                    if (++retries > maxRetries) throw e
                    delay(1000L * retries)
                    currentUrl = urlRefresher?.invoke() ?: streamUrl
                }
            }
        }

        val totalBytes = fetchTotalBytes(url)
        val chunkSize = totalBytes / chunkCount
        val raf = RandomAccessFile(outputPath, "rw").also { it.setLength(totalBytes) }
        val downloaded = AtomicLong(0)

        raf.use { f ->
            (0 until chunkCount).map { i ->
                val start = i * chunkSize
                val end = if (i == chunkCount - 1) totalBytes - 1 else start + chunkSize - 1
                launch { downloadChunk(url, f, start, end, downloaded, totalBytes) }
            }.joinAll()
        }
    }

    suspend fun runFFmpeg(command: String): Boolean =
        kotlinx.coroutines.suspendCancellableCoroutine { cont ->
            val session = FFmpegKit.executeAsync(command) { session ->
                @Suppress("DEPRECATION")
                cont.resume(ReturnCode.isSuccess(session.returnCode)) {}
            }
            cont.invokeOnCancellation { session.cancel() }
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
}