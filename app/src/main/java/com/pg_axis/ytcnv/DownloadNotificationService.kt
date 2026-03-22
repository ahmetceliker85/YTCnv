package com.pg_axis.ytcnv

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.IBinder

class DownloadNotificationService : Service() {

    companion object {
        const val CHANNEL_ID = "ytcnv_download_channel"
        const val FINISH_CHANNEL_ID = "ytcnv_finnish_channel"
        const val NOTIFICATION_ID = 1
        const val FINISH_NOTIFICATION_ID = 2

        fun showFinishNotification(context: Context, fileName: String) {
            val manager = context.getSystemService(NotificationManager::class.java)

            val intent = Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK
            }
            val pendingIntent = PendingIntent.getActivity(
                context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )

            val notification = Notification.Builder(context, FINISH_CHANNEL_ID)
                .setContentTitle("Download Finished")
                .setContentText("Downloaded $fileName")
                .setSmallIcon(R.drawable.splash)
                .setContentIntent(pendingIntent)
                .setAutoCancel(true)
                .build()

            manager.notify(FINISH_NOTIFICATION_ID, notification)
        }
    }

    override fun onCreate() {
        super.onCreate()

        val manager = getSystemService(NotificationManager::class.java)

        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_ID,
                "YTCnv Downloads",
                NotificationManager.IMPORTANCE_LOW
            ).apply { description = "Download progress notifications" }
        )

        manager.createNotificationChannel(
            NotificationChannel(
                FINISH_CHANNEL_ID,
                "YTCnv Download Complete",
                NotificationManager.IMPORTANCE_DEFAULT
            ).apply { description = "Notifies when a download finishes" }
        )
    }


    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val openIntent = Intent(this, MainActivity::class.java)
        openIntent.flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_REORDER_TO_FRONT

        val pendingIntent = PendingIntent.getActivity(
            this, 0, openIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("YTCnv")
            .setContentText("Download in progress...")
            .setSmallIcon(R.drawable.splash)
            .setOngoing(true)
            .setContentIntent(pendingIntent)
            .build()

        startForeground(NOTIFICATION_ID, notification)
        return START_NOT_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null
}