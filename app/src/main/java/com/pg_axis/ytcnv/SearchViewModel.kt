package com.pg_axis.ytcnv

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.TextFieldValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import org.schabi.newpipe.extractor.ServiceList
import org.schabi.newpipe.extractor.stream.StreamInfoItem

class SearchViewModel(val settings: ISettings) : ViewModel() {

    var searchQuery by mutableStateOf(TextFieldValue(""))
    var results by mutableStateOf<List<SearchResultItem>>(emptyList())
    var isLoading by mutableStateOf(false)
    var errorMessage by mutableStateOf<String?>(null)

    fun onQueryChanged(value: TextFieldValue) { searchQuery = value }

    fun onSearch() {
        if (searchQuery.text.isBlank()) return
        updateSearchHistory(searchQuery.text.trim())
        viewModelScope.launch(Dispatchers.IO) {
            isLoading = true
            errorMessage = null
            results = emptyList()
            try {
                val extractor = ServiceList.YouTube.getSearchExtractor(searchQuery.text.trim())
                extractor.fetchPage()
                results = extractor.initialPage.items
                    .filterIsInstance<StreamInfoItem>()
                    .take(10)
                    .map { item ->
                        SearchResultItem(
                            title = item.name,
                            videoId = extractVideoId(item.url),
                            uploader = item.uploaderName ?: "",
                            duration = formatDuration(item.duration),
                            thumbnailUrl = item.thumbnails.firstOrNull()?.url ?: "",
                            url = item.url
                        )
                    }
            } catch (e: Exception) {
                errorMessage = e.message
            } finally {
                isLoading = false
            }
        }
    }

    fun onHistoryItemTapped(query: String) {
        searchQuery = TextFieldValue(
            text = query,
            selection = TextRange(query.length)
        )
    }

    fun onRemoveHistoryItem(query: String) {
        val updated = settings.searchHistory.toMutableList()
        updated.remove(query)
        settings.searchHistory = updated
        (settings as? SettingsSave)?.saveExtraData()
    }

    private fun updateSearchHistory(query: String) {
        val updated = settings.searchHistory.toMutableList()
        updated.remove(query)
        updated.add(0, query)
        settings.searchHistory = updated
        (settings as? SettingsSave)?.saveExtraData()
    }

    private fun extractVideoId(url: String): String {
        return url.substringAfter("v=").substringBefore("&")
    }

    private fun formatDuration(seconds: Long): String {
        if (seconds <= 0) return "Live"
        val h = seconds / 3600
        val m = (seconds % 3600) / 60
        val s = seconds % 60
        return if (h > 0) "%d:%02d:%02d".format(h, m, s)
        else "%d:%02d".format(m, s)
    }
}

data class SearchResultItem(
    val title: String,
    val videoId: String,
    val uploader: String,
    val duration: String,
    val thumbnailUrl: String,
    val url: String
)
