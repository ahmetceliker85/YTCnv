package com.pg_axis.ytcnv

import android.content.Context
import android.os.Build
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.URL

object UpdateChecker {

    private const val GITHUB_API = "https://api.github.com/repos/PGAxis/YTCnv/releases/latest"

    suspend fun checkForUpdates(context: Context, settings: ISettings): UpdateInfo? =
        withContext(Dispatchers.IO) {
            try {
                if (settings.dontShowUpdate || settings.alreadyShown) return@withContext null
                settings.alreadyShown = true

                val json = URL(GITHUB_API).openConnection().apply {
                    setRequestProperty("User-Agent", "YTCnv-App")
                }.getInputStream().bufferedReader().readText()

                val release = JSONObject(json)
                val tagName = release.getString("tag_name").trimStart('v')
                val body = release.optString("body", "")

                val packageInfo = context.packageManager.getPackageInfo(context.packageName, 0)
                val currentVersion = packageInfo.versionName!!
                    .split(".")
                    .take(3)
                    .joinToString(".")

                val latest = parseVersion(tagName)
                val current = parseVersion(currentVersion)

                if (latest != null && current != null && latest > current) {
                    UpdateInfo(tagName, body)
                } else null

            } catch (e: Exception) {
                println("Update check failed: ${e.message}")
                null
            }
        }

    private fun parseVersion(version: String): Triple<Int, Int, Int>? {
        val parts = version.split(".").mapNotNull { it.toIntOrNull() }
        return if (parts.size >= 3) Triple(parts[0], parts[1], parts[2]) else null
    }

    private fun isInstalledFromStore(context: Context): Boolean {
        val installer = context.packageManager
            .getInstallSourceInfo(context.packageName)
            .installingPackageName

        return installer == "org.fdroid.fdroid" ||
                installer == "com.aurora.store" ||
                installer == "org.gdroid.gdroid"
    }

    private operator fun Triple<Int, Int, Int>.compareTo(other: Triple<Int, Int, Int>): Int {
        if (first != other.first) return first - other.first
        if (second != other.second) return second - other.second
        return third - other.third
    }
}

data class UpdateInfo(val version: String, val changelog: String)