using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using ProtoBuf;
using Riemann.Proto;

namespace Riemann {
	///
	/// <summary>Client represents a connection to the Riemann service.</summary>
	///
	public class Client : IDisposable, IClient
	{
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
		private readonly bool _throwExceptionsOnTicks;
	    private readonly bool _useTcp;

		private static string GetFqdn() {
			var properties = IPGlobalProperties.GetIPGlobalProperties();
			return string.Format("{0}.{1}", properties.HostName, properties.DomainName);
		}

		///
		/// <summary>Constructs a new Client with the specified host, port</summary>
		/// <param name='host'>Remote hostname to connect to. Default: localhost</param>
		/// <param name='port'>Port to connect to. Default: 5555</param>
		/// <param name='throwExceptionOnTicks'>Throw an exception on the background thread managing the TickEvents. Default: true</param>
		/// <param name="useTcp">Use TCP for transport (UDP otherwise). Default: false</param>
		///
		public Client(string host = "localhost", int port = 5555, bool throwExceptionOnTicks = true, bool useTcp = false) {
			_writer = new Lazy<Stream>(MakeStream);
			_datagram = new Lazy<Socket>(MakeDatagram);
			_host = host;
			_port = (ushort)port;
			_throwExceptionsOnTicks = throwExceptionOnTicks;
		    _useTcp = useTcp;
		}

		///
		/// <summary>Adds a tag to the current context (relative to this client). This call is not thread-safe.</summary>
		/// <param name='tag'>New tag to add to the Riemann events sent using this client.</param>
		///
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

		///
		/// <summary>
		/// After <paramref name="tickTimeInSeconds" /> seconds, <paramref name="onTick" /> will be invoked.
		/// The resulting <see cref="TickEvent" /> is composed with the <paramref name="service" /> to generate an Event.
		/// </summary>
		/// <param name="tickTimeInSeconds">
		/// Number of seconds to wait before calling the event back.
		/// <note>Because only a single thread calls the events back, it may be called back sooner.</note>
		/// </param>
		/// <param name="service">Name of the service to send to Riemann</param>
		/// <param name="onTick">Function to call back after wait period</param>
		/// <returns>
		/// A disposable that, if called, will remove this callback from getting called.
		/// <note>An additional tick may elapse after removal, due to the multithreaded nature.</note>
		/// </returns>
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
			try {
				var events = new List<Event>();
				List<TickDisposable> ticks;
				lock (_timerLock) {
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
					    var tickTags = new List<string>();
					    if (t.Tags != null)
					    {
					        tickTags.AddRange(t.Tags);
					    }
						events.Add(new Event(tick.Service, t.State, t.Description, t.MetricValue, t.TTL.HasValue ? t.TTL.Value : tick.TickTime * 2, tickTags));
						tick.NextTick = tick.TickTime;  
					}
				}
				if (removals.Count > 0) {
					lock (_timerLock) {
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
			} catch {
				if (_throwExceptionsOnTicks) throw;
			}
		}

		private readonly Lazy<Stream> _writer;
		private readonly Lazy<Socket> _datagram;
		private const SocketError SocketErrorMessageTooLong = SocketError.MessageSize;

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

		///
		/// <summary>Send many events to Riemann at once.</summary>
		/// <param name="events">
		/// Enumerable of the events to process.
		/// Enumerable will be enumerated after being passed in.
		/// </param>
		///
		public void SendEvents(IEnumerable<Event> events) {
			var tags = new List<string>();
			lock (_tagLock) {
				if (_tag != null) {
					tags = _tag.Tags.ToList();
				}
			}
			var protoEvents = events.Select(
				e =>  {
                    var evnt = new Proto.Event
                    {
                        host = _name,
                        service = e.Service,
                        state = e.State,
                        description = e.Description,
                        metric_f = e.Metric,
                        ttl = e.TTL
                    };
                    evnt.tags.AddRange(e.Tags);
                    return evnt;
				}).ToList();

			var message = new Proto.Msg();
			foreach (var protoEvent in protoEvents) {
				protoEvent.tags.AddRange(tags);
				message.events.Add(protoEvent);
			}
			var array = MessageBytes(message);

		    if (_useTcp) {
		        WriteToStream(array);
		        return;
		    }

			try {
				Datagram.Send(array);
			} catch (SocketException se) {
				if (se.SocketErrorCode == SocketErrorMessageTooLong) {
					WriteToStream(array);
				} else {
					throw;
				}
			}
		}

	    private void WriteToStream(Byte[] array) {
            var x = BitConverter.GetBytes(array.Length);
            Array.Reverse(x);
            Stream.Write(x, 0, 4);
            Stream.Write(array, 0, array.Length);
            Stream.Flush();
	    }

		private static byte[] MessageBytes(Msg message)
		{
			using (var memoryStream = new MemoryStream()) {
				Serializer.Serialize(memoryStream, message);
				return memoryStream.ToArray();
			}
		}

		///
		/// <summary>Send a single event to Riemann.</summary>
		/// <param name='service'>Name of the service to push.</param>
		/// <param name='state'>State of the service; usual values are "ok", "critical", "warning"</param>
		/// <param name='description'>
		/// A description of the current state, if applicable.
		/// Use null or an empty string to denote no additional information.
		/// </param>
		/// <param name='metric'>A value related to the service.</param>
		/// <param name='ttl'>Number of seconds this event will be applicable for.</param>
        /// <param name="tags">List of tags to associate with this event</param>
		///
        public void SendEvent(string service, string state, string description, float metric, int ttl = 0, List<string> tags = null)
        {
			var ev = new Event(service, state, description, metric, ttl, tags);
			SendEvents(new[] {ev});
		}

		///
		/// <summary>Queries Riemann</summary>
		/// <param name='query'>Query to send Riemann for process</param>
		/// <returns>List of States that answer the query.</returns>
		///
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

		///
		/// <summary>Cleans up state related to this client.</summary>
		///
		public void Dispose() {
			if (_writer.IsValueCreated) {
				_writer.Value.Close();
				_writer.Value.Dispose();
			}
			if (_datagram.IsValueCreated) {
				_datagram.Value.Close();
				_datagram.Value.Dispose();
			}
			GC.SuppressFinalize(this);
		}

		///
		/// <summary>Closes connections.</summary>
		///
		~Client() {
			Dispose();
		}
	}
}
