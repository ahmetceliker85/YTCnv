package com.pg_axis.ytcnv

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
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
        settings.mainFolder = folderName
        settings.finalFolder = ""
        (settings as? SettingsSave)?.saveSettings()
    }
}