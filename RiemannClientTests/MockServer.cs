using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ProtoBuf;
using Riemann.Proto;

namespace RiemannClientTests
{
	public class MockServer
	{
		private TcpListener _tcpListener;
		private TcpClient _tcpClient;
		private ManualResetEvent _isStopped = new ManualResetEvent(false);

		public Queue<Msg> TcpMessageQueue = new Queue<Msg>();

		public MockServer()
		{
			_isStopped.Set();
		}

		public void Start()
		{
			Console.WriteLine("MockServer Starting");
			_isStopped.Reset();
			_tcpListener = new TcpListener(IPAddress.Loopback, 5558);
			_tcpListener.Start();
			Console.WriteLine("MockServer Started. Beginning accept...");
			_tcpListener.BeginAcceptTcpClient(ClientConnected, _tcpListener);				
			Console.WriteLine("MockServer accepting clients");
		}

		private void ClientConnected(IAsyncResult ar)
		{
			if (_tcpListener == null)
			{
				Console.WriteLine("server shutdown, not handling client request");
				_isStopped.Set();
				return;
			}
			Console.WriteLine("MockServer handling client connection");
			var server = (TcpListener) ar.AsyncState;
			using (_tcpClient = server.EndAcceptTcpClient(ar))
			using (var stream = _tcpClient.GetStream())
			{
				try
				{
					while (true)
					{
						var msg = Serializer.DeserializeWithLengthPrefix<Msg>(stream, PrefixStyle.Fixed32BigEndian);
						if (msg != null)
						{
							Console.WriteLine("MockServer read client message: {0}", msg);
							TcpMessageQueue.Enqueue(msg);
							var reply = new Msg {ok = true};
							Console.WriteLine("MockServer writing client reply");
							Serializer.SerializeWithLengthPrefix(stream, reply, PrefixStyle.Fixed32BigEndian);
							Console.WriteLine("MockServer client reply written");
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("MockServer caught exception in client handler: {0}", ex.Message);
				}
			}
			_isStopped.Set();
		}

		public void Stop()
		{
			if (_tcpListener != null)
			{
				_tcpListener.Stop();
				_tcpListener = null;
			}
			if (_tcpClient != null)
			{
				_tcpClient.Close();
				_tcpClient = null;
			}
			_isStopped.WaitOne();
		}
	}
}
