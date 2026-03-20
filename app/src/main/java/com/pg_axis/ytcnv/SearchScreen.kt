package com.pg_axis.ytcnv

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.pg_axis.ytcnv.ui.theme.*

@Composable
fun SearchScreen(
    onBack: () -> Unit,
    onResultSelected: (String) -> Unit,
    viewModel: SearchViewModel
) {
    val focusManager = LocalFocusManager.current
    val context = LocalContext.current
    var isSearchFocused by remember { mutableStateOf(false) }

    // Animate history panel height
    val historyHeightFraction by animateFloatAsState(
        targetValue = if (isSearchFocused && viewModel.settings.searchHistory.isNotEmpty()) 1f else 0f,
        animationSpec = tween(durationMillis = 200),
        label = "historyHeight"
    )

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BackgroundDark)
            .windowInsetsPadding(WindowInsets.systemBars)
            .clickable(indication = null, interactionSource = remember {
                androidx.compose.foundation.interaction.MutableInteractionSource()
            }) { focusManager.clearFocus() }
    ) {
        // ─── Search bar ───
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) {
                Icon(
                    painter = painterResource(id = R.drawable.back),
                    contentDescription = "Back",
                    tint = CyanLight
                )
            }
            OutlinedTextField(
                value = viewModel.searchQuery,
                onValueChange = { viewModel.onQueryChanged(it) },
                placeholder = { Text("Search YouTube...", color = TextSecondary) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                keyboardActions = KeyboardActions(onSearch = {
                    focusManager.clearFocus()
                    viewModel.onSearch()
                }),
                modifier = Modifier
                    .weight(1f)
                    .onFocusChanged { isSearchFocused = it.isFocused }
            )
        }

        // ─── Search history panel ───
        if (historyHeightFraction > 0f) {
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp)
                    .heightIn(max = (300 * historyHeightFraction).dp),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = CardDark)
            ) {
                LazyColumn (contentPadding = PaddingValues(0.dp), modifier = Modifier.fillMaxWidth()) {
                    items(viewModel.settings.searchHistory) { term ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clickable { viewModel.onHistoryItemTapped(term) }
                                .padding(horizontal = 16.dp, vertical = 12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = term,
                                color = TextPrimary,
                                modifier = Modifier.weight(1f),
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis
                            )
                            IconButton(
                                onClick = { viewModel.onRemoveHistoryItem(term) },
                                modifier = Modifier.size(24.dp)
                            ) {
                                Icon(
                                    painter = painterResource(id = R.drawable.cross),
                                    contentDescription = "Remove",
                                    tint = TextSecondary,
                                    modifier = Modifier.size(16.dp)
                                )
                            }
                        }
                        HorizontalDivider(color = BorderColor)
                    }
                }
            }
            Spacer(modifier = Modifier.height(8.dp))
        }

        // ─── Results / states ───
        Box(modifier = Modifier.fillMaxSize()) {
            when {
                viewModel.isLoading -> {
                    CircularProgressIndicator(
                        modifier = Modifier.align(Alignment.Center),
                        color = CyanPrimary
                    )
                }
                viewModel.errorMessage != null -> {
                    Text(
                        text = viewModel.errorMessage!!,
                        color = TextSecondary,
                        modifier = Modifier
                            .align(Alignment.Center)
                            .padding(24.dp),
                        textAlign = TextAlign.Center
                    )
                }
                viewModel.results.isEmpty() -> {
                    Text(
                        text = "Search for a YouTube video",
                        color = TextSecondary,
                        modifier = Modifier.align(Alignment.Center)
                    )
                }
                else -> {
                    LazyColumn {
                        items(viewModel.results) { item ->
                            SearchResultRow(
                                item = item,
                                onDownload = {
                                    onResultSelected("https://www.youtube.com/watch?v=${item.videoId}")
                                    onBack()
                                },
                                onCopyUrl = {
                                    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                    val clip = ClipData.newPlainText("YouTube URL", "https://www.youtube.com/watch?v=${item.videoId}")
                                    clipboard.setPrimaryClip(clip)
                                }
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun SearchResultRow(
    item: SearchResultItem,
    onDownload: () -> Unit,
    onCopyUrl: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.Top
    ) {
        // Thumbnail
        AsyncImage(
            model = item.thumbnailUrl,
            contentDescription = null,
            modifier = Modifier
                .width(120.dp)
                .height(68.dp)
                .clip(RoundedCornerShape(8.dp))
                .background(CardDark)
        )

        Spacer(modifier = Modifier.width(12.dp))

        // Info + buttons
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.title,
                color = TextPrimary,
                fontWeight = FontWeight.Bold,
                fontSize = 14.sp,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = item.uploader,
                color = TextSecondary,
                fontSize = 12.sp,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                text = item.duration,
                color = CyanLight,
                fontSize = 12.sp
            )
            Spacer(modifier = Modifier.height(6.dp))
            Row {
                Button(
                    onClick = onDownload,
                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp),
                    modifier = Modifier.height(32.dp)
                ) {
                    Text("Download", fontSize = 12.sp)
                }
                Spacer(modifier = Modifier.width(8.dp))
                OutlinedButton(
                    onClick = onCopyUrl,
                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp),
                    modifier = Modifier.height(32.dp)
                ) {
                    Text("Copy URL", fontSize = 12.sp, color = CyanLight)
                }
            }
        }
    }
    HorizontalDivider(color = BorderColor, modifier = Modifier.padding(horizontal = 16.dp))
}