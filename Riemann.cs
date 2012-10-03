using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using ProtoBuf;

namespace riemann {
	public class Riemann {
		private RiemannTags _tag;
		private readonly object _tagLock = new object();

		private class RiemannTags : IDisposable {
			private readonly Riemann _owner;
			private readonly RiemannTags _underlying;
			private readonly string _tag;

			public RiemannTags(Riemann owner, RiemannTags underlying, string tag) {
				_owner = owner;
				_underlying = underlying;
				_tag = tag;
			}

			public void Dispose() {
				_owner._tag = _underlying;
			}

			public IEnumerable<string> Tags {
				get {
					if (_underlying != null) {
						foreach (var tag in _underlying.Tags) {
							yield return tag;
						}
					}
					yield return _tag;
				}
			}
		}

		private readonly string _host;
		private readonly ushort _port;
		private readonly string _name = GetFqdn();

		private static string GetFqdn() {
			var properties = IPGlobalProperties.GetIPGlobalProperties();
			return string.Format("{0}.{1}", properties.HostName, properties.DomainName);
		}

		public Riemann(string host, ushort port) {
			_writer = new Lazy<Stream>(MakeStream);
			_datagram = new Lazy<Socket>(MakeDatagram);
			_host = host;
			_port = port;
		}

		public IDisposable Tag(string tag) {
			lock (_tagLock) {
				_tag = new RiemannTags(this, _tag, tag);
				return _tag;
			}
		}

		public IDisposable Tick(int tickTimeInSeconds, string service, Func<TickEvent> onTick) {
			var timer = new Timer(
				_ => {
					var t = onTick();
					SendEvent(service, t.State, t.Description, t.MetricValue, tickTimeInSeconds);
				});
			timer.Change(TimeSpan.FromSeconds(tickTimeInSeconds), TimeSpan.FromSeconds(tickTimeInSeconds));
			return timer;
		}

		private readonly Lazy<Stream> _writer;
		private readonly Lazy<Socket> _datagram;
		private const int SocketExceptionErrorCodeMessageTooLong = 10040;

		private Stream MakeStream() {
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(_host, _port);
			return new NetworkStream(socket, true);
		}

		private Stream Stream {
			get { return _writer.Value; }
		}

		private Socket MakeDatagram() {
			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			socket.Connect(_host, _port);
			return socket;
		}

		private Socket Datagram {
			get { return _datagram.Value; }
		}

		public void SendEvent(string service, string state, string description, float metric, int ttl = 0) {
			if (state.Length > 255) {
				throw new ArgumentException("State parameter is too long, must be 255 characters or less", "state");
			}
			lock (this) {
				var ev = new Event { host = _name, service = service, state = state, description = description, metric_f = metric };
				if (ttl != 0) {
					ev.ttl = ttl;
				}
				lock (_tagLock) {
					if (_tag != null) {
						ev.tags.AddRange(_tag.Tags);
					}
				}
				{
					var message = new Msg();
					message.events.Add(ev);
					var memoryStream = new MemoryStream();
					Serializer.Serialize(memoryStream, message);
					var array = memoryStream.ToArray();
					try {
						Datagram.Send(array);
					} catch (SocketException se) {
						if (se.ErrorCode == SocketExceptionErrorCodeMessageTooLong) {
					var x = BitConverter.GetBytes(array.Length);
					Array.Reverse(x);
					Stream.Write(x, 0, 4);
					Stream.Write(array, 0, array.Length);
					Stream.Flush();
						} else {
							throw;
						}
					}
				}
			}
		}
	}
}