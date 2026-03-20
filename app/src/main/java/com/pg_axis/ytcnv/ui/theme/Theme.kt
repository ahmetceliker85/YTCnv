package com.pg_axis.ytcnv.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val YTCnvColorScheme = darkColorScheme(
    primary = CyanPrimary,
    onPrimary = TextOnButton,
    primaryContainer = SurfaceVariantDark,
    onPrimaryContainer = TextPrimary,

    secondary = BlueSecondary,
    onSecondary = TextPrimary,
    secondaryContainer = CardDark,
    onSecondaryContainer = TextPrimary,

    tertiary = BlueTertiary,
    onTertiary = TextPrimary,
    tertiaryContainer = SurfaceVariantDark,
    onTertiaryContainer = TextPrimary,

    background = BackgroundDark,
    onBackground = TextPrimary,

    surface = SurfaceDark,
    onSurface = TextPrimary,
    surfaceVariant = SurfaceVariantDark,
    onSurfaceVariant = TextSecondary,

    outline = BorderColor,
    outlineVariant = DividerColor,

    error = PopupError,
    onError = TextPrimary,
)

@Composable
fun YTCnvTheme(
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = YTCnvColorScheme,
        typography = Typography,
        content = content
    )
}