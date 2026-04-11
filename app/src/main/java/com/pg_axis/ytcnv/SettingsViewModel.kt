package com.pg_axis.ytcnv

import android.app.Application
import android.provider.DocumentsContract
import android.util.Log
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.net.toUri
import androidx.core.os.LocaleListCompat
import androidx.lifecycle.AndroidViewModel
import java.util.Locale

class SettingsViewModel(val settings: ISettings, val mainViewModel: MainViewModel, application: Application) : AndroidViewModel(application) {
    private val context = getApplication<Application>()

    val langOptions = mapOf("en" to "English", "cs" to "Čeština")
    var selectedLang = AppCompatDelegate.getApplicationLocales().toLanguageTags()
        .ifEmpty { Locale.getDefault().language.ifEmpty { langOptions.keys.first() } }


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
    fun onLanguageChange(key: String) {
        selectedLang = langOptions.getValue(key)
        Log.d("Locale Change", key)
        AppCompatDelegate.setApplicationLocales(
            LocaleListCompat.forLanguageTags(key)
        )
    }

    private fun getMainFolder(uri: String): String {
        val docId = DocumentsContract.getTreeDocumentId(uri.toUri())
        val parts = docId.split(':')

        return if (parts[0].equals("primary", true)) context.getString(R.string.internal_storage)
        else context.getString(R.string.sd_card)
    }
}