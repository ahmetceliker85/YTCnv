package com.pg_axis.ytcnv

import okhttp3.OkHttpClient
import okhttp3.RequestBody.Companion.toRequestBody
import org.schabi.newpipe.extractor.downloader.Downloader
import org.schabi.newpipe.extractor.downloader.Request
import org.schabi.newpipe.extractor.downloader.Response
import org.schabi.newpipe.extractor.exceptions.ReCaptchaException

class NewPipeDownloader : Downloader() {
    private val client = OkHttpClient()

    override fun execute(request: Request): Response {
        val requestBuilder = okhttp3.Request.Builder().url(request.url())

        request.headers().forEach { (key, values) ->
            values.forEach { value -> requestBuilder.addHeader(key, value) }
        }

        when (request.httpMethod()) {
            "GET" -> requestBuilder.get()
            "POST" -> {
                val content = request.dataToSend() ?: byteArrayOf()
                requestBuilder.post(
                    content.toRequestBody(null, 0, content.size)
                )
            }
        }

        val response = client.newCall(requestBuilder.build()).execute()

        if (response.code == 429) throw ReCaptchaException("429 Too Many Requests", request.url())

        return Response(
            response.code,
            response.message,
            response.headers.toMultimap(),
            response.body.string(),
            response.request.url.toString()
        )
    }
}

