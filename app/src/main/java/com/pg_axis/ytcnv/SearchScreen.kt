package com.pg_axis.ytcnv

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CutCornerShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.pg_axis.ytcnv.ui.theme.*

@Composable
@Preview(showBackground = true, showSystemUi = true)
fun SearchPreview() {
    val viewModel = remember {
        SearchViewModel(PreviewSettings()).apply {
            results = listOf(
                SearchResultItem(
                    title = "How to Build Android Apps with Jetpack Compose",
                    videoId = "abc123",
                    uploader = "Android Developers",
                    duration = "15:42",
                    thumbnailUrl = "https://picsum.photos/seed/1/320/180",
                    url = "https://youtube.com/watch?v=abc123"
                ),
                SearchResultItem(
                    title = "Kotlin Coroutines Deep Dive - Full Course",
                    videoId = "def456",
                    uploader = "Tech Academy",
                    duration = "1:23:15",
                    thumbnailUrl = "https://picsum.photos/seed/2/320/180",
                    url = "https://youtube.com/watch?v=def456"
                ),
                SearchResultItem(
                    title = "Material Design 3 Tutorial",
                    videoId = "ghi789",
                    uploader = "UI/UX Masters",
                    duration = "0:00",
                    thumbnailUrl = "https://picsum.photos/seed/3/320/180",
                    url = "https://youtube.com/watch?v=ghi789"
                )
            )
            isLoading = false
            errorMessage = null
        }
    }
    YTCnvTheme {
        SearchScreen({}, {}, viewModel)
    }
}

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
        // ─── Header ───
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 8.dp)
                .height(60.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack, shape = CutCornerShape(0.dp)) {
                Icon(
                    painter = painterResource(id = R.drawable.back),
                    contentDescription = "Back",
                    tint = CyanLight
                )
            }
            Text(
                text = stringResource(R.string.search_title),
                fontSize = 18.sp,
                fontWeight = FontWeight.Bold,
                color = TextPrimary
            )
        }
        // ─── Search bar ───
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 8.dp)
                .height(IntrinsicSize.Max),
            verticalAlignment = Alignment.CenterVertically
        ) {
            OutlinedTextField(
                value = viewModel.searchQuery,
                onValueChange = { viewModel.onQueryChanged(it) },
                placeholder = { Text(stringResource(R.string.search_prompt), color = TextSecondary) },
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
            Spacer(modifier = Modifier.width(5.dp))
            Box {
                Box(
                    modifier = Modifier
                        .size(56.dp)
                        .clip(RoundedCornerShape(5.dp))
                        .background(CyanPrimary)
                        .clickable { viewModel.onSearch() },
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        painter = painterResource(R.drawable.magglass),
                        contentDescription = "Source",
                        tint = BackgroundDark
                    )
                }
            }
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
                        text = stringResource(R.string.search_big_prompt),
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
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 16.dp)
    ) {
        // Thumbnail with duration overlay and buttons
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .weight(1f)
                    .aspectRatio(16f / 9f)
                    .clip(RoundedCornerShape(8.dp))
            ) {
                AsyncImage(
                    model = item.thumbnailUrl,
                    contentDescription = null,
                    contentScale = ContentScale.Crop,
                    modifier = Modifier.fillMaxSize()
                )
                Text(
                    text = item.duration,
                    color = Color.White,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier
                        .align(Alignment.BottomEnd)
                        .padding(6.dp)
                        .background(Color.Black.copy(alpha = 0.6f), RoundedCornerShape(4.dp))
                        .padding(horizontal = 4.dp, vertical = 2.dp)
                )
            }
            Spacer(modifier = Modifier.width(8.dp))
            // Buttons
            Column(
                modifier = Modifier
                    .padding(8.dp)
                    .fillMaxHeight(),
                verticalArrangement = Arrangement.spacedBy(8.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Button(
                    onClick = onDownload,
                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp),
                    modifier = Modifier.height(32.dp)
                ) {
                    Text(stringResource(R.string.download), fontSize = 12.sp)
                }
                Spacer(modifier = Modifier.width(8.dp))
                OutlinedButton(
                    onClick = onCopyUrl,
                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp),
                    modifier = Modifier.height(32.dp)
                ) {
                    Text(stringResource(R.string.copy_url), fontSize = 12.sp, color = CyanLight)
                }
            }
        }
        Spacer(modifier = Modifier.height(6.dp))
        // Title
        Text(
            text = item.title,
            color = TextPrimary,
            fontWeight = FontWeight.Bold,
            fontSize = 14.sp,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis
        )
        Spacer(modifier = Modifier.height(2.dp))
        // Uploader
        Text(
            text = item.uploader,
            color = TextSecondary,
            fontSize = 12.sp,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
    HorizontalDivider(color = BorderColor, thickness = 4.dp, modifier = Modifier.padding(horizontal = 16.dp))
}