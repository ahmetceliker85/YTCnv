package com.pg_axis.ytcnv

import android.app.Activity
import android.content.Intent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.pg_axis.ytcnv.ui.theme.*

@Composable
fun SettingsScreen(
    onBack: () -> Unit,
    viewModel: SettingsViewModel
) {
    val context = LocalContext.current
    val packageInfo = context.packageManager.getPackageInfo(context.packageName, 0)
    val versionName = packageInfo.versionName
    val versionCode = packageInfo.longVersionCode

    // SAF folder picker launcher
    val folderPickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK) {
            result.data?.data?.let { uri ->
                context.contentResolver.takePersistableUriPermission(
                    uri,
                    Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                )
                val folderName = uri.lastPathSegment
                    ?.substringAfterLast(":")
                    ?.substringAfterLast("/")
                    ?: uri.toString()
                viewModel.onFolderPicked(uri.toString(), folderName)
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BackgroundDark)
            .windowInsetsPadding(WindowInsets.systemBars)
    ) {
        // ─── Header ───
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 8.dp, vertical = 8.dp)
                .height(60.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) {
                Icon(
                    painter = painterResource(id = R.drawable.back),
                    contentDescription = "Back",
                    tint = CyanLight
                )
            }
            Text(
                text = "Settings",
                fontSize = 18.sp,
                fontWeight = FontWeight.Bold,
                color = TextPrimary
            )
        }

        Column(
            modifier = Modifier
                .weight(1f)
                .padding(horizontal = 20.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            // ─── Folder picker ───
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = CardDark)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Button(
                        onClick = {
                            val intent = Intent(Intent.ACTION_OPEN_DOCUMENT_TREE)
                            folderPickerLauncher.launch(intent)
                        },
                        colors = ButtonDefaults.buttonColors(
                            containerColor = CyanPrimary,
                            contentColor = BackgroundDark
                        )
                    ) {
                        Text("Change download destination")
                    }
                    Text(
                        text = viewModel.settings.mainFolder + viewModel.settings.finalFolder,
                        color = TextSecondary,
                        fontSize = 13.sp
                    )
                }
            }

            // ─── Toggle: 4K ───
            SettingsToggleRow(
                label = "Enable up to 4K downloads",
                checked = viewModel.settings.use4K,
                onCheckedChange = { viewModel.onUse4KChanged(it) }
            )

            // ─── Toggle: Quick download ───
            SettingsToggleRow(
                label = "Enable quick download",
                checked = viewModel.settings.quickDwnld,
                onCheckedChange = { viewModel.onQuickDwnldChanged(it) }
            )

            // ─── Toggle: Don't show updates ───
            SettingsToggleRow(
                label = "Don't remind me of new versions",
                checked = viewModel.settings.dontShowUpdate,
                onCheckedChange = { viewModel.onDontShowUpdateChanged(it) }
            )

            // ─── Toggle: Notify on finish ───
            SettingsToggleRow(
                label = "Notify when download finishes",
                checked = viewModel.settings.notifyOnFinish,
                onCheckedChange = { viewModel.onNotifyOnFinishChanged(it) }
            )
        }

        // ─── Version label ───
        Text(
            text = "$versionName ($versionCode)",
            color = Gray500,
            fontSize = 12.sp,
            modifier = Modifier
                .align(Alignment.CenterHorizontally)
                .padding(8.dp)
        )
    }
}

@Composable
fun SettingsToggleRow(
    label: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = CardDark)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = label,
                color = TextPrimary,
                modifier = Modifier.weight(1f)
            )
            Switch(
                checked = checked,
                onCheckedChange = onCheckedChange,
                colors = SwitchDefaults.colors(
                    checkedThumbColor = BackgroundDark,
                    checkedTrackColor = CyanPrimary,
                    uncheckedThumbColor = TextSecondary,
                    uncheckedTrackColor = SurfaceVariantDark
                )
            )
        }
    }
}