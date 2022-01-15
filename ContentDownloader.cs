using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using UniRx;

namespace Modules.Utilities
{
	public class ContentDownloader
	{
		//--------------------------------------------------------------------------------
		
		private const long		_PARAM_CONNECTION_TIMEOUT	=	15000; 
		
		//--------------------------------------------------------------------------------
		
		public struct DownloadProgress
		{
			public int		DownloadedSize;
			public uint		FileSize;
			public string	Path;

			public float GetProgress()
			{
				return DownloadedSize / (float)FileSize;
			}

			public void DebugLog()
			{
				UnityEngine.Debug.LogFormat
				(
					"Download bytes: {0}\nTotal bytes: {1}\nProgress: {2}\nFile path: {3}",
					DownloadedSize,
					FileSize,
					GetProgress(),
					Path
				);
			}

			public DownloadProgress(int _downloadedSize, uint _fileSize, string _path)
			{
				DownloadedSize = _downloadedSize;
				FileSize = _fileSize;
				Path = _path;
			}
		}

		//--------------------------------------------------------------------------------

		public static IObservable<DownloadProgress> DownloadAsObservable(KeyValuePair<string, string>[] _url, int _buffer)
		{
			var		observables	= _url.Select(_keyValuePair => DownloadAsObservable(_buffer, _keyValuePair.Key, _keyValuePair.Value)).ToList();
			return observables.Concat();
		}
		
		//--------------------------------------------------------------------------------

		private static void StartQuery
			(
				Uri _uri, 
				string _fileName,
				out int _downloaded,
				out uint _contentLength,
				out Socket _client,
				out NetworkStream _networkStream,
				out FileStream 	_fileStream
			)
		{
			var	query	=	"GET " + _uri.AbsoluteUri.Replace(" ", "%20") + " HTTP/1.1\r\n" +
			   	     	 	"Host: " + _uri.Host + "\r\n" +
			   	     	 	"User-Agent: undefined\r\n" +
			   	     	 	"Connection: close\r\n"+
			   	     	 	"\r\n";
	 
			 
			UnityEngine.Debug.LogFormat
			(
				"Query Content of {0}\n{1}", 
				_uri.AbsoluteUri, 
				query
			);
			 
			_client		=	new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
			_client.Connect(_uri.Host, 80);   
			 		
			_networkStream	=	new NetworkStream(_client);
			 
			var	bytes		=	Encoding.Default.GetBytes(query);
			_networkStream.Write(bytes, 0, bytes.Length);
			 
			var reader			=	new BinaryReader(_networkStream, Encoding.Default);
			 
			string response = "";
			string line;
			char c;	
			 
			do 
			{
				line = "";
				
				while (true) 
				{
					c = reader.ReadChar();
					if (c == '\r')
						break;
					line += c;
				}
				
				reader.ReadChar();
				response += line + "\r\n";
			} 
			while (line.Length > 0);  
			 
			UnityEngine.Debug.Log ( "Response:\n " + response );
			 
			var	regexContentLength	=	new Regex(@"(?<=Content-Length:\s)\d+", RegexOptions.IgnoreCase);
			_fileStream				=	new FileStream( _fileName, FileMode.Create);
			_downloaded				=	0;
			_contentLength			=	uint.Parse(regexContentLength.Match(response).Value);
		}
		
		//--------------------------------------------------------------------------------

		public static IObservable<DownloadProgress> DownloadAsObservable(int _buffer, string _url, string _fileName = null)
		{
			var	uri		=	new Uri(_url);
			_fileName	=	string.IsNullOrEmpty(_fileName) ? 
							Path.GetFileName(uri.LocalPath) : 
							_fileName;
			
			return	Observable.Create<DownloadProgress>
			(
				_observer =>
				{
					int				downloaded;
					uint			contentLength;
					Socket 			client;
					NetworkStream 	networkStream;
					FileStream		fileStream;
					
					StartQuery
					(
						uri,
						_fileName,
						out downloaded,
						out contentLength,
						out client,
						out networkStream,
						out fileStream
					);
					
					var	path				=	fileStream.Name;
					var	lastPacketRecieved	=	DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
					
					_observer.OnNext(new DownloadProgress(downloaded, contentLength, path));
					
					var	update		=	
					Observable
						.EveryUpdate()
						.Subscribe
						(
							_ =>
							{
								try
								{
									var currentTimestamp	=	DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
									if (downloaded < contentLength) 
									{
										if (!SocketConnected(client))
										{
											fileStream.Flush();
											fileStream.Close();
											networkStream.Close();
		
											client.Close();
											
											StartQuery
											(
												uri,
												_fileName,
												out downloaded,
												out contentLength,
												out client,
												out networkStream,
												out fileStream
											);
										}
										else
										{
											var buffer = new byte[_buffer];
									
											if (networkStream.DataAvailable) 
											{
												var read	=	networkStream.Read(buffer, 0, buffer.Length);
												downloaded	+=	read;
		
												fileStream.Write(buffer, 0, read);
												lastPacketRecieved	=	currentTimestamp;
											}
		
											UnityEngine.Debug.Log 
											( 
												"Downloaded: " + downloaded + " of " + contentLength + " bytes ..." + "\n" +
												"networkStream.DataAvailable: " + networkStream.DataAvailable
											);

											if (currentTimestamp - lastPacketRecieved > _PARAM_CONNECTION_TIMEOUT)
												throw new Exception("Download connection Hang up before ends.");
										}
										
										_observer.OnNext(new DownloadProgress(downloaded, contentLength, path));
									}
									else
									{
										fileStream.Flush();
										fileStream.Close();
										networkStream.Close();

										client.Close();
									
										UnityEngine.Debug.Log ( "Downloaded: " + _fileName );
										_observer.OnCompleted();
									}
								}
								catch (Exception exception)
								{
									UnityEngine.Debug.LogError(exception.ToString());
									_observer.OnError(exception);
								}
							}
						);
					
					return Disposable.Create(() => update.Dispose());
				}
			);
		}
		
		//--------------------------------------------------------------------------------
		
		private static bool SocketConnected(Socket _socket)
		{
			return !_socket.Poll(1000, SelectMode.SelectRead) || _socket.Available != 0;
		}
		
		//--------------------------------------------------------------------------------
	}
}
