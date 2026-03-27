package com.pg_axis.ytcnv

import android.content.Context
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.google.gson.Gson

class SettingsSave private constructor(context: Context) : ISettings {

    companion object {
        @Volatile
        private var instance: SettingsSave? = null

        fun getInstance(context: Context): SettingsSave =
            instance ?: synchronized(this) {
                instance ?: SettingsSave(context.applicationContext).also { instance = it }
            }
    }

    private val settingsPath = context.filesDir.resolve("settings.json")
    private val extraDataPath = context.filesDir.resolve("extra_data.json")
    private val gson = Gson()

    // ─── Settings ───
    override var quickDwnld by mutableStateOf(true)

    override var dontShowUpdate by mutableStateOf(false)

    override var fileUri by mutableStateOf("")

    override var mainFolder by mutableStateOf("Internal storage")

    override var finalFolder by mutableStateOf(" - Downloads")

    override var notifyOnFinish by mutableStateOf(true)

    // ─── Extra data ───
    override var searchHistory by mutableStateOf<List<String>>(emptyList())

    override var downloadHistory by mutableStateOf<List<HistoryItem>>(emptyList())

    // ─── Singleton variables ───
    override var isDownloadRunning = false
    override var alreadyShown = false
    override var iHaveId = false
    override var id = ""

    // ─── Save/Load ───
    fun saveSettings() {
        val settings = SettingsClass(
            quickDownload = quickDwnld,
            dontShowUpdatePopup = dontShowUpdate,
            savedFileUri = fileUri,
            mainFolderName = mainFolder,
            finalFolderName = finalFolder,
            notifyOnFinish = notifyOnFinish
        )
        settingsPath.writeText(gson.toJson(settings))
    }

    fun saveExtraData() {
        val extraData = ExtraData(
            searchHistory = searchHistory,
            downloadHistory = downloadHistory
        )
        extraDataPath.writeText(gson.toJson(extraData))
    }

    fun loadSettings() {
        if (settingsPath.exists()) {
            gson.fromJson(settingsPath.readText(), SettingsClass::class.java)?.let {
                quickDwnld = it.quickDownload
                dontShowUpdate = it.dontShowUpdatePopup
                fileUri = it.savedFileUri ?: ""
                mainFolder = it.mainFolderName ?: "Internal storage"
                finalFolder = it.finalFolderName ?: " - Downloads"
                notifyOnFinish = it.notifyOnFinish
            }
        }
        if (extraDataPath.exists()) {
            try {
                gson.fromJson(extraDataPath.readText(), ExtraData::class.java)?.let {
                    searchHistory = it.searchHistory
                    downloadHistory = it.downloadHistory
                }
            } catch (_: Exception) {
                searchHistory = emptyList()
                downloadHistory = emptyList()
            }
        }
    }

    // ─── Data classes ───
    data class SettingsClass(
        val quickDownload: Boolean = true,
        val dontShowUpdatePopup: Boolean = false,
        val savedFileUri: String? = null,
        val mainFolderName: String? = null,
        val finalFolderName: String? = null,
        val notifyOnFinish: Boolean = true
    )

    data class ExtraData(
        val searchHistory: List<String> = emptyList(),
        val downloadHistory: List<HistoryItem> = emptyList()
    )

    data class HistoryItem(
        val title: String,
        val urlOrId: String
    )

    init {
        loadSettings()
    }
}