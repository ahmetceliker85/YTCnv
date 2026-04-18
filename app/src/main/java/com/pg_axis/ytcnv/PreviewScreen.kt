package com.pg_axis.ytcnv

import android.annotation.SuppressLint
import android.content.pm.ActivityInfo
import androidx.activity.compose.LocalActivity
import androidx.annotation.OptIn
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.MediaItem
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.MergingMediaSource
import androidx.media3.exoplayer.source.ProgressiveMediaSource
import androidx.media3.ui.AspectRatioFrameLayout
import androidx.media3.ui.PlayerView
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@SuppressLint("SourceLockedOrientationActivity")
@OptIn(UnstableApi::class)
@Composable
fun PreviewScreen(
    onBack: () -> Unit,
    viewModel: PreviewViewModel
) {
    val activity = LocalActivity.current
    val coroutineScope = rememberCoroutineScope()

    val exoPlayer = remember {
        ExoPlayer.Builder(activity!!).build().apply { playWhenReady = true }
    }
    DisposableEffect(Unit) {
        onDispose {
            viewModel.savedPositionMs = exoPlayer.currentPosition
            exoPlayer.release()
        }
    }

    DisposableEffect(Unit) {
        activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_SENSOR
        onDispose {
            activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT
        }
    }

    LaunchedEffect(viewModel.videoUrl, viewModel.audioUrl, viewModel.isMerged) {
        val vid = viewModel.videoUrl ?: return@LaunchedEffect
        val factory = DefaultHttpDataSource.Factory()
        if (viewModel.isMerged && viewModel.audioUrl != null) {
            val videoSrc = ProgressiveMediaSource.Factory(factory)
                .createMediaSource(MediaItem.fromUri(vid))
            val audioSrc = ProgressiveMediaSource.Factory(factory)
                .createMediaSource(MediaItem.fromUri(viewModel.audioUrl!!))
            exoPlayer.setMediaSource(MergingMediaSource(videoSrc, audioSrc))
        } else {
            exoPlayer.setMediaItem(MediaItem.fromUri(vid))
        }
        exoPlayer.prepare()
        if (viewModel.savedPositionMs > 0) {
            exoPlayer.seekTo(viewModel.savedPositionMs)
        }
    }

    // --- UI state ---
    var isPlaying by remember { mutableStateOf(true) }
    var currentPosition by remember { mutableLongStateOf(0L) }
    var duration by remember { mutableLongStateOf(0L) }
    var controlsVisible by remember { mutableStateOf(true) }
    // null = hidden, -10 = rewind indicator, +10 = forward indicator
    var skipIndicator by remember { mutableStateOf<Int?>(null) }
    // Seek bar drag state
    var isSeeking by remember { mutableStateOf(false) }
    var seekingProgress by remember { mutableFloatStateOf(0f) }

    // Poll playback position every 500ms
    LaunchedEffect(exoPlayer) {
        while (true) {
            currentPosition = exoPlayer.currentPosition
            duration = exoPlayer.duration.coerceAtLeast(0)
            isPlaying = exoPlayer.isPlaying
            delay(500)
        }
    }

    DisposableEffect(exoPlayer) {
        val listener = object : Player.Listener {
            override fun onIsPlayingChanged(playing: Boolean) { isPlaying = playing }
        }
        exoPlayer.addListener(listener)
        onDispose { exoPlayer.removeListener(listener) }
    }

    var hideJob by remember { mutableStateOf<kotlinx.coroutines.Job?>(null) }
    fun showControls() {
        controlsVisible = true
        hideJob?.cancel()
        hideJob = coroutineScope.launch {
            delay(3_000)
            controlsVisible = false
        }
    }

    LaunchedEffect(skipIndicator) {
        if (skipIndicator != null) {
            delay(700)
            skipIndicator = null
        }
    }

    LaunchedEffect(Unit) { showControls() }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black)
    ) {
        AndroidView(
            factory = { ctx ->
                PlayerView(ctx).apply {
                    player = exoPlayer
                    useController = false
                    resizeMode = AspectRatioFrameLayout.RESIZE_MODE_FIT
                }
            },
            modifier = Modifier.fillMaxSize()
        )

        if (viewModel.isLoading) {
            CircularProgressIndicator(
                modifier = Modifier.align(Alignment.Center),
                color = Color.White
            )
        }
        viewModel.errorMessage?.let { err ->
            Text(
                text = err,
                color = Color.White,
                modifier = Modifier
                    .align(Alignment.Center)
                    .padding(24.dp)
            )
        }

        Row(modifier = Modifier.fillMaxSize()) {
            Box(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight()
                    .pointerInput(Unit) {
                        detectTapGestures(
                            onTap = { showControls() },
                            onDoubleTap = {
                                exoPlayer.seekTo(
                                    (exoPlayer.currentPosition - 10_000).coerceAtLeast(0)
                                )
                                skipIndicator = -10
                                showControls()
                            }
                        )
                    }
            )
            Box(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight()
                    .pointerInput(Unit) {
                        detectTapGestures(
                            onTap = { showControls() },
                            onDoubleTap = {
                                exoPlayer.seekTo(
                                    (exoPlayer.currentPosition + 10_000).coerceAtMost(duration)
                                )
                                skipIndicator = 10
                                showControls()
                            }
                        )
                    }
            )
        }

        skipIndicator?.let { secs ->
            Box(
                modifier = Modifier
                    .align(if (secs < 0) Alignment.CenterStart else Alignment.CenterEnd)
                    .padding(horizontal = 40.dp)
                    .background(Color.Black.copy(alpha = 0.55f), RoundedCornerShape(10.dp))
                    .padding(horizontal = 20.dp, vertical = 10.dp)
            ) {
                Text(
                    text = if (secs < 0) "−10s" else "+10s",
                    color = Color.White,
                    fontWeight = FontWeight.Bold,
                    fontSize = 16.sp
                )
            }
        }

        AnimatedVisibility(
            visible = controlsVisible,
            enter = fadeIn(tween(180)),
            exit = fadeOut(tween(250)),
            modifier = Modifier.fillMaxSize()
        ) {
            Box(modifier = Modifier.fillMaxSize()) {
                Box(
                    modifier = Modifier
                        .align(Alignment.TopStart)
                        .windowInsetsPadding(WindowInsets.systemBars)
                        .padding(12.dp)
                        .size(width = 44.dp, height = 36.dp)
                        .clip(RoundedCornerShape(10.dp))
                        .background(Color.Black.copy(alpha = 0.52f))
                        .clickable(
                            indication = null,
                            interactionSource = remember { MutableInteractionSource() }
                        ) { onBack() },
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        painter = painterResource(R.drawable.back),
                        contentDescription = "Back",
                        tint = Color(0xFFEEEEEE),
                        modifier = Modifier.size(20.dp)
                    )
                }

                Text(
                    text = viewModel.videoTitle,
                    color = Color.White.copy(alpha = 0.92f),
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Medium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier
                        .align(Alignment.TopStart)
                        .windowInsetsPadding(WindowInsets.systemBars)
                        .padding(start = 68.dp, end = 16.dp, top = 20.dp)
                )

                Box(
                    modifier = Modifier
                        .align(Alignment.Center)
                        .size(60.dp)
                        .clip(CircleShape)
                        .background(Color.Black.copy(alpha = 0.52f))
                        .clickable(
                            indication = null,
                            interactionSource = remember { MutableInteractionSource() }
                        ) {
                            if (exoPlayer.isPlaying) exoPlayer.pause() else exoPlayer.play()
                            showControls()
                        },
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        painter = if (isPlaying) painterResource(R.drawable.pause) else painterResource(R.drawable.play),
                        contentDescription = if (isPlaying) "Pause" else "Play",
                        tint = Color.White,
                        modifier = Modifier.size(30.dp)
                    )
                }

                Column(
                    modifier = Modifier
                        .align(Alignment.BottomStart)
                        .fillMaxWidth()
                        .background(
                            Brush.verticalGradient(
                                listOf(Color.Transparent, Color.Black.copy(alpha = 0.72f))
                            )
                        )
                        .windowInsetsPadding(WindowInsets.systemBars)
                        .padding(horizontal = 12.dp)
                        .padding(bottom = 8.dp, top = 24.dp)
                ) {
                    val displayProgress = when {
                        isSeeking -> seekingProgress
                        duration > 0 -> currentPosition.toFloat() / duration.toFloat()
                        else -> 0f
                    }
                    Slider(
                        value = displayProgress,
                        onValueChange = { v ->
                            isSeeking = true
                            seekingProgress = v
                            showControls()
                        },
                        onValueChangeFinished = {
                            exoPlayer.seekTo((seekingProgress * duration).toLong())
                            isSeeking = false
                        },
                        modifier = Modifier.fillMaxWidth(),
                        colors = SliderDefaults.colors(
                            thumbColor = Color.White,
                            activeTrackColor = Color.White,
                            inactiveTrackColor = Color.White.copy(alpha = 0.35f)
                        )
                    )
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 4.dp),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(
                            text = formatMs(
                                if (isSeeking) (seekingProgress * duration).toLong()
                                else currentPosition
                            ),
                            color = Color.White,
                            fontSize = 12.sp
                        )
                        Text(
                            text = formatMs(duration),
                            color = Color.White.copy(alpha = 0.65f),
                            fontSize = 12.sp
                        )
                    }
                }
            }
        }
    }
}

private fun formatMs(ms: Long): String {
    if (ms <= 0) return "0:00"
    val s = ms / 1000
    val h = s / 3600
    val m = (s % 3600) / 60
    val sec = s % 60
    return if (h > 0) "%d:%02d:%02d".format(h, m, sec)
    else "%d:%02d".format(m, sec)
}