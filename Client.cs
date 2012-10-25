using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using ProtoBuf;

namespace Riemann {
	public class Client {
		private RiemannTags _tag;
		private readonly object _tagLock = new object();

		private class RiemannTags : IDisposable {
			private readonly Client _owner;
			private readonly RiemannTags _underlying;
			private readonly string _tag;

			public RiemannTags(Client owner, RiemannTags underlying, string tag) {
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

		public Client(string host, ushort port) {
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

		private class TickDisposable : IDisposable {
			public readonly int TickTime;
			public readonly string Service;
			public int NextTick;
			public bool RemoveRequested;
			private readonly Func<TickEvent> _onTick; 

			public TickDisposable(int tickTime, string service, Func<TickEvent> onTick) {
				TickTime = tickTime;
				Service = service;
				_onTick = onTick;
			}

			public void Dispose() {
				RemoveRequested = true;
			}

			public TickEvent Tick() {
				return _onTick();
			}
		}

		private readonly object _timerLock = new object();
		private Timer _timer;
		private List<TickDisposable> _ticks;

		public IDisposable Tick(int tickTimeInSeconds, string service, Func<TickEvent> onTick) {
			var disposable = new TickDisposable(tickTimeInSeconds, service, onTick);
			lock(_timerLock) {
				if (_ticks == null) {
					_ticks = new List<TickDisposable>();
					_timer = new Timer(_=> ProcessTicks());
					_timer.Change(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0));
				}
				_ticks.Add(disposable);
			}
			return disposable;
		}

		private void ProcessTicks() {
			var events = new List<Event>();
			List<TickDisposable> ticks;
			lock(_timerLock) {
				ticks = _ticks.ToList();
			}
			var removals = new List<TickDisposable>();
			foreach (var tick in ticks) {
				if (tick.RemoveRequested) {
					removals.Add(tick);
				}
				tick.NextTick = tick.NextTick - 1;
				if (tick.NextTick <= 0) {
					var t = tick.Tick();
					events.Add(new Event(tick.Service, t.State, t.Description, t.MetricValue, tick.TickTime));
					tick.NextTick = tick.TickTime;
				}
			}
			if (removals.Count > 0) {
				lock(_timerLock) {
					if (removals.Count == _ticks.Count) {
						_ticks = null;
						_timer.Dispose();
						_timer = null;
					} else {
						foreach (var removal in removals) {
							_ticks.Remove(removal);
						}
					}
				}
			}
			if (events.Count > 0) {
				SendEvents(events);
			}
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

		public void SendEvents(IEnumerable<Event> events) {
			var tags = new List<string>();
			lock (_tagLock) {
				if (_tag != null) {
					tags = _tag.Tags.ToList();
				}
			}
			var protoEvents = events.Select(
				e => new Proto.Event {
					host = _name,
					service = e.Service,
					state = e.State,
					description = e.Description,
					metric_f = e.Metric,
					ttl = e.TTL
				}).ToList();

			var message = new Proto.Msg();			
			foreach (var protoEvent in protoEvents) {
				protoEvent.tags.AddRange(tags);
				message.events.Add(protoEvent);
			}
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
					var response = Serializer.Deserialize<Proto.Msg>(Stream);
					if (!response.ok) {
						throw new Exception(response.error);
					}
				} else {
					throw;
				}
			}
		}

		public void SendEvent(string service, string state, string description, float metric, int ttl = 0) {
			var ev = new Event(service, state, description, metric, ttl);
			SendEvents(new[] {ev});
		}

		public IEnumerable<Proto.State> Query(string query) {
			var q = new Proto.Query {@string = query};
			var msg = new Proto.Msg {query = q};
			Serializer.Serialize(Stream, msg);
			var response = Serializer.Deserialize<Proto.Msg>(Stream);
			if (response.ok) {
				return response.states;
			}
			throw new Exception(response.error);
		}
	}
}