// <summary>
// 
// The MIT License (MIT)
// 
// Copyright (c) 2020 Federico Mazzanti
// 
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// 
// </summary>

namespace Mafesoft.RestClient
{
    using System;

    /// <summary>
    /// The events raised while sending the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Sets onStart action and occurs when the request is started
        /// </summary>
        /// <param name="onStart"></param>
        /// <returns></returns>
        public RestBuilder OnStart(Action<StartEventArgs>? onStart)
            => With(r => r.onStartAction = onStart);

        /// <summary>
        ///  Sets OnPreviewContentRequestAsString, displays the request content as string
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentRequestAsString(Action<PreviewContentAsStringEventArgs>? onPreviewContent)
            => With(r => r.onPreviewContentRequestAsStringAction = onPreviewContent);

        /// <summary>
        /// Sets onUploadProgress action, occurs when the request is processing
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnUploadProgress(Action<ProgressEventArgs>? onProgress)
            => With(r => r.onUploadProgressAction = onProgress);

        /// <summary>
        /// Sets onDownloadProgress action, occurs when the response is processing (also while reading a Stream result)
        /// </summary>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public RestBuilder OnDownloadProgress(Action<ProgressEventArgs>? onProgress)
            => With(r => r.onDownloadProgressAction = onProgress);

        /// <summary>
        /// Sets OnPreCompleted, occurs when the result of request il builded and it isn't completed yet
        /// </summary>
        /// <param name="onPreCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnPreCompleted(Action<PreCompletedEventArgs>? onPreCompleted)
            => With(r => r.onPreCompletedAction = onPreCompleted);

        /// <summary>
        /// Sets OnPreviewContentResponseAsString, displays the response as string.
        /// When it is set the response is buffered as string before being deserialized.
        /// </summary>
        /// <param name="onPreviewContent"></param>
        /// <returns></returns>
        public RestBuilder OnPreviewContentResponseAsString(Action<PreviewContentAsStringEventArgs>? onPreviewContent)
            => With(r => r.onPreviewContentResponseAsStringAction = onPreviewContent);

        /// <summary>
        /// Sets onCompleted action, occurs when the call is completed.
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public RestBuilder OnCompleted(Action<CompletedEventArgs>? onCompleted)
            => With(r => r.onCompletedAction = onCompleted);

        /// <summary>
        /// Sets onException, occurs when there is an exception during the call or while reading the response
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public RestBuilder OnException(Action<Exception>? exception)
            => With(r => r.onExceptionAction = exception);
    }
}
