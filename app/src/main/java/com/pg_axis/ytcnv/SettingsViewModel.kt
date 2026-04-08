package com.pg_axis.ytcnv

import android.provider.DocumentsContract
import androidx.core.net.toUri
import androidx.lifecycle.ViewModel

class SettingsViewModel(val settings: ISettings, val mainViewModel: MainViewModel) : ViewModel() {
    fun onUse4KChanged(value: Boolean) {
        settings.use4K = value
        (settings as? SettingsSave)?.saveSettings()
    }
    fun onQuickDwnldChanged(value: Boolean) {
        settings.quickDwnld = value
        (settings as? SettingsSave)?.saveSettings()
        mainViewModel.applyQuickDownloadState()
    }
    fun onDontShowUpdateChanged(value: Boolean) {
        settings.dontShowUpdate = value
        (settings as? SettingsSave)?.saveSettings()
    }
    fun onFolderPicked(uri: String, folderName: String) {
        settings.fileUri = uri
        settings.mainFolder = getMainFolder(uri)
        settings.finalFolder = " - $folderName"
        (settings as? SettingsSave)?.saveSettings()
    }
    fun onNotifyOnFinishChanged(value: Boolean) {
        settings.notifyOnFinish = value
        (settings as? SettingsSave)?.saveSettings()
    }
    fun onNotifyOnFailChanged(value: Boolean) {
        settings.notifyOnFail = value
        (settings as? SettingsSave)?.saveSettings()
    }

    private fun getMainFolder(uri: String): String {
        val docId = DocumentsContract.getTreeDocumentId(uri.toUri())
        val parts = docId.split(':')

        return if (parts[0].equals("primary", true)) "Internal storage"
        else "SD card"
    }
}