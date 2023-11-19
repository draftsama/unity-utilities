using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
#if !UniRxLibrary
using ObservableUnity = UniRx.Observable;
// ReSharper disable UnusedMember.Global

#endif

namespace UniRx
{
    public static class ObservableWebRequest
    {
        public static IObservable<UnityWebRequest> ToRequestObservable(this UnityWebRequest _request,
            IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                Fetch(_request, null, _observer, _progress, _cancellation));
        }

        public static IObservable<string> ToObservable(this UnityWebRequest _request, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<string>((_observer, _cancellation) =>
                FetchText(_request, null, _observer, _progress, _cancellation));
        }

        public static IObservable<byte[]> ToBytesObservable(this UnityWebRequest _request,
            IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<byte[]>((_observer, _cancellation) =>
                Fetch(_request, null, _observer, _progress, _cancellation));
        }

        public static IObservable<string> Get(string _url, IDictionary<string, string> _headers = null,
            IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<string>((_observer, _cancellation) =>
                FetchText(UnityWebRequest.Get(_url), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<byte[]> GetAndGetBytes(string _url, IDictionary<string, string> _headers = null,
            IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<byte[]>((_observer, _cancellation) =>
                FetchBytes(UnityWebRequest.Get(_url), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<UnityWebRequest> GetRequest(string _url, IDictionary<string, string> _headers = null,
            IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchGetRequest(UnityWebRequest.Get(_url), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<string> Post(string _url, Dictionary<string, string> _postData,
            IDictionary<string, string> _headers = null, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<string>((_observer, _cancellation) =>
                FetchText(UnityWebRequest.Post(_url, _postData), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<byte[]> PostAndGetBytes(string _url, Dictionary<string, string> _postData,
            IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<byte[]>((_observer, _cancellation) =>
                FetchBytes(UnityWebRequest.Post(_url, _postData), null, _observer, _progress, _cancellation));
        }

        public static IObservable<byte[]> PostAndGetBytes(string _url, Dictionary<string, string> _postData,
            IDictionary<string, string> _headers, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<byte[]>((_observer, _cancellation) =>
                FetchBytes(UnityWebRequest.Post(_url, _postData), _headers, _observer, _progress, _cancellation));
        }
        public static IObservable<UnityWebRequest> PutRequest(string _url, string _postData,
            IDictionary<string, string> _headers, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchPutRequest(UnityWebRequest.Put(_url, _postData), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<UnityWebRequest> PutRequest(string _url, byte[] _bytes,
            IDictionary<string, string> _headers, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchPutRequest(UnityWebRequest.Put(_url, _bytes), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<UnityWebRequest> PostRequest(string _url, Dictionary<string, string> _postData,
            IProgress<float> _progress = null)
        {
            return PostRequest(_url, _postData, null, _progress);
        }

        public static IObservable<UnityWebRequest> PostRequest(string _url, Dictionary<string, string> _postData,
            IDictionary<string, string> _headers, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchRequest(UnityWebRequest.Post(_url, _postData), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<UnityWebRequest> PostRequest(string _url, string _postData,
            IDictionary<string, string> _headers, IProgress<float> _progress = null)
        {
            #if UNITY_2022_1_OR_NEWER


            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchRequest(UnityWebRequest.PostWwwForm(_url, _postData), new System.Text.UTF8Encoding().GetBytes(_postData), _headers, _observer, _progress, _cancellation));
       
            #else
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchRequest(UnityWebRequest.Post(_url, _postData), new System.Text.UTF8Encoding().GetBytes(_postData), _headers, _observer, _progress, _cancellation));
            #endif
        }

        public static IObservable<UnityWebRequest> DeleteRequest(string _url,
            IDictionary<string, string> _headers, IProgress<float> _progress = null)
        {
            return ObservableUnity.FromCoroutine<UnityWebRequest>((_observer, _cancellation) =>
                FetchDeleteRequest(UnityWebRequest.Delete(_url), _headers, _observer, _progress, _cancellation));
        }

        public static IObservable<Texture2D> GetTexture(string _url, bool _nonReadable = false)
        {
            return ObservableUnity.FromCoroutine<Texture2D>((_observer, _cancellation) =>
                FetchTextureRequest(UnityWebRequestTexture.GetTexture(_url, _nonReadable), _observer));
        }

        public static IObservable<AudioClip> GetAudioClip(string _url, AudioType _audioType)
        {
            return ObservableUnity.FromCoroutine<AudioClip>((_observer, _cancellation) =>
                 FetchAudioRequest(UnityWebRequestMultimedia.GetAudioClip(_url, _audioType), _observer));
        }

        static IEnumerator Fetch<T>(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<T> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            if (_headers != null)
            {
                foreach (var header in _headers) _request.SetRequestHeader(header.Key, header.Value);
            }



            if (_reportProgress != null)
            {
                var operation = _request.SendWebRequest();
                while (!operation.isDone && !_cancel.IsCancellationRequested)
                {
                    try
                    {
                        _reportProgress.Report(operation.progress);
                    }
                    catch (Exception ex)
                    {
                        _observer.OnError(ex);
                        yield break;
                    }

                    yield return null;
                }
            }
            else
            {
                yield return _request.SendWebRequest();
            }

            if (_cancel.IsCancellationRequested)
            {
                yield break;
            }

            if (_reportProgress != null)
            {
                try
                {
                    _reportProgress.Report(_request.downloadProgress);
                }
                catch (Exception ex)
                {
                    _observer.OnError(ex);
                }
            }
        }

        static IEnumerator FetchText(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<string> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnError(new UnityWebRequestErrorException(_request));
                }
                else
                {
                    var text = System.Text.Encoding.UTF8.GetString(_request.downloadHandler.data);
                    _observer.OnNext(text);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchBytes(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<byte[]> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnError(new UnityWebRequestErrorException(_request));
                }
                else
                {
                    _observer.OnNext(_request.downloadHandler.data);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchRequest(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<UnityWebRequest> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                _request.method = UnityWebRequest.kHttpVerbPOST;
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnNext(_request);
                }
                else
                {
                    _observer.OnNext(_request);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchDeleteRequest(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<UnityWebRequest> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                _request.method = UnityWebRequest.kHttpVerbDELETE;
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnNext(_request);
                }
                else
                {
                    _observer.OnNext(_request);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchRequest(UnityWebRequest _request, byte[] _data, IDictionary<string, string> _headers,
            IObserver<UnityWebRequest> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                try
                {
                    _request.method = UnityWebRequest.kHttpVerbPOST;
                    _request.uploadHandler = new UploadHandlerRaw(_data);
                }
                catch (Exception e)
                {
                    _observer.OnError(e);
                    yield break;
                }
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnNext(_request);
                }
                else
                {
                    _observer.OnNext(_request);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchGetRequest(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<UnityWebRequest> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                _request.method = UnityWebRequest.kHttpVerbGET;
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnNext(_request);
                }
                else
                {
                    _observer.OnNext(_request);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchPutRequest(UnityWebRequest _request, IDictionary<string, string> _headers,
            IObserver<UnityWebRequest> _observer, IProgress<float> _reportProgress, CancellationToken _cancel)
        {
            using (_request)
            {
                _request.method = UnityWebRequest.kHttpVerbPUT;
                yield return Fetch(_request, _headers, _observer, _reportProgress, _cancel);

                if (_cancel.IsCancellationRequested)
                {
                    yield break;
                }

                if (!string.IsNullOrEmpty(_request.error))
                {
                    _observer.OnNext(_request);
                }
                else
                {
                    _observer.OnNext(_request);
                    _observer.OnCompleted();
                }
            }
        }

        static IEnumerator FetchTextureRequest(UnityWebRequest _request, IObserver<Texture2D> _observer)
        {
            using (_request)
            {
                yield return _request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (_request.result == UnityWebRequest.Result.ConnectionError || 
                    _request.result == UnityWebRequest.Result.DataProcessingError || 
                    _request.result == UnityWebRequest.Result.ProtocolError)
                {
                    _observer.OnNext(null);
                }
                else if (_request.result == UnityWebRequest.Result.Success)
                {
                    _observer.OnNext(DownloadHandlerTexture.GetContent(_request));
                    _observer.OnCompleted();
                }
#else
                if (_request.isNetworkError || _request.isHttpError)
                {
                    _observer.OnNext(null);

                }
                {
                    _observer.OnNext(DownloadHandlerTexture.GetContent(_request));
                    _observer.OnCompleted();
                }
#endif
            }
        }
        static IEnumerator FetchAudioRequest(UnityWebRequest _request, IObserver<AudioClip> _observer)
        {
            using (_request)
            {
                yield return _request.SendWebRequest();
#if UNITY_2020_2_OR_NEWER

                if (_request.result == UnityWebRequest.Result.ConnectionError ||
                   _request.result == UnityWebRequest.Result.DataProcessingError ||
                   _request.result == UnityWebRequest.Result.ProtocolError)
                {
                    _observer.OnNext(null);
                }
                else if (_request.result == UnityWebRequest.Result.Success)
                {
                    _observer.OnNext(DownloadHandlerAudioClip.GetContent(_request));
                    _observer.OnCompleted();
                }
#else
                if (_request.isNetworkError || _request.isHttpError)
                {
                    _observer.OnNext(null);

                }
                {
                    _observer.OnNext(DownloadHandlerAudioClip.GetContent(_request));
                    _observer.OnCompleted();
                }
#endif
            }
        }
    }

    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class UnityWebRequestErrorException : Exception
    {
        public string RawErrorMessage { get; private set; }
        public bool HasResponse { get; private set; }
        public string Text { get; private set; }
        public System.Net.HttpStatusCode StatusCode { get; private set; }
        public Dictionary<string, string> ResponseHeaders { get; private set; }
        public UnityWebRequest Request { get; private set; }

        // cache the text because if www was disposed, can't access it.
        public UnityWebRequestErrorException(UnityWebRequest _request)
        {
            Request = _request;
            RawErrorMessage = _request.error;
            ResponseHeaders = _request.GetResponseHeaders();
            HasResponse = false;

            StatusCode = (System.Net.HttpStatusCode)_request.responseCode;

            if (_request.downloadHandler != null)
            {
                Text = _request.downloadHandler.text;
            }

            if (_request.responseCode != 0)
            {
                HasResponse = true;
            }
        }

        public override string ToString()
        {
            var text = Text;
            if (string.IsNullOrEmpty(text))
            {
                return RawErrorMessage;
            }
            else
            {
                return RawErrorMessage + " " + text;
            }
        }
    }
}