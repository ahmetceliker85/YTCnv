package com.pg_axis.ytcnv

interface ISettings {
    var quickDwnld: Boolean
    var downloadHistory: List<SettingsSave.HistoryItem>
    var searchHistory: List<String>
    var mainFolder: String
    var finalFolder: String
    var fileUri: String
    var isDownloadRunning: Boolean
    var iHaveId: Boolean
    var id: String
    var dontShowUpdate : Boolean
    var alreadyShown : Boolean
    var notifyOnFinish: Boolean
}