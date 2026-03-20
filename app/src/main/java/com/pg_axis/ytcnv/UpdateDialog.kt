package com.pg_axis.ytcnv

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import com.pg_axis.ytcnv.ui.theme.*
import androidx.core.net.toUri

@Composable
fun UpdateDialog(
    updateInfo: UpdateInfo,
    onDismiss: (dontShowAgain: Boolean) -> Unit
) {
    val context = LocalContext.current
    var dontShowAgain by remember { mutableStateOf(false) }

    val changelogText = buildAnnotatedString {
        withStyle(SpanStyle(fontWeight = FontWeight.Bold, color = TextPrimary)) {
            append("Change log\n\n")
        }
        val cleaned = updateInfo.changelog
            .replace("### Change log\r\n\r\n", "")
            .replace("- ", "• ")
        append(cleaned)
    }

    Dialog(onDismissRequest = { onDismiss(dontShowAgain) }) {
        Card(
            shape = RoundedCornerShape(12.dp),
            colors = CardDefaults.cardColors(containerColor = CardDark)
        ) {
            Column(modifier = Modifier.padding(20.dp)) {
                Text(
                    text = "Update available",
                    fontWeight = FontWeight.Bold,
                    fontSize = 17.sp,
                    color = TextPrimary,
                    modifier = Modifier.padding(bottom = 8.dp)
                )
                Text(
                    text = "A new version (${updateInfo.version}) is available.",
                    color = TextSecondary,
                    fontSize = 14.sp,
                    modifier = Modifier.padding(bottom = 12.dp)
                )

                // Changelog
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 200.dp)
                        .verticalScroll(rememberScrollState())
                ) {
                    Text(
                        text = changelogText,
                        color = TextPrimary,
                        fontSize = 13.sp
                    )
                }

                Spacer(modifier = Modifier.height(12.dp))

                // Don't show again checkbox
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.padding(bottom = 12.dp)
                ) {
                    Checkbox(
                        checked = dontShowAgain,
                        onCheckedChange = { dontShowAgain = it },
                        colors = CheckboxDefaults.colors(
                            checkedColor = CyanPrimary,
                            uncheckedColor = TextSecondary
                        )
                    )
                    Text(
                        text = "Don't remind me again",
                        color = TextSecondary,
                        fontSize = 13.sp
                    )
                }

                // Buttons
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.End
                ) {
                    TextButton(onClick = { onDismiss(dontShowAgain) }) {
                        Text("Later", color = TextSecondary)
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    Button(
                        onClick = {
                            val tagName = updateInfo.version
                            val apkUrl = "https://github.com/PGAxis/YTCnv/releases/download/v${tagName}/youtubeConverter-${tagName}.apk"
                            context.startActivity(
                                Intent(Intent.ACTION_VIEW, apkUrl.toUri())
                            )
                            onDismiss(dontShowAgain)
                        }
                    ) {
                        Text("Download")
                    }
                }
            }
        }
    }
}