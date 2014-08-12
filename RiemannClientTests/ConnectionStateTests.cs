using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Riemann;
using Riemann.Proto;
using Event = Riemann.Event;

namespace RiemannClientTests
{
	[TestClass]
	public class ConnectionStateTests
	{
		private Client _client;
		private MockServer _server;

		[TestInitialize]
		public void Setup()
		{
			_server = new MockServer();
			_client = new Client("localhost", 5558, false, true);
			_client.SuppressSendErrors = false;
		}

		[TestCleanup]
		public void TearDown()
		{
			_client.Dispose();
			_server.Stop();
		}

		private void SendUntilSuccessfulAndAssert(Event evt, long timeoutMs = 6000)
		{
			var startTime = DateTime.Now;
			while (true)
			{
				try
				{
					Console.WriteLine("sending evt until successful");
					_client.SendEvents(new [] {evt});
					break;
				}
				catch (IOException ex)
				{
					Assert.AreNotEqual(Client.State.Connected, _client.ConnectionState);
					Console.WriteLine(string.Format("send exception: {0}", ex));
					Thread.Sleep(500);
				}
				if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
				{
					Assert.Fail("Client could not reconnect within timeout period");
				}
			}
			Assert.AreEqual(Client.State.Connected, _client.ConnectionState);
			Assert.AreEqual(1, _server.TcpMessageQueue.Count);

			var msg = _server.TcpMessageQueue.Dequeue();
			Assert.AreEqual(1, msg.events.Count);

			var serverEvent = msg.events[0];
			Assert.AreEqual(evt.Service, serverEvent.service);
			Assert.AreEqual(evt.State, serverEvent.state);
			Assert.AreEqual(evt.Description, serverEvent.description);
			Assert.AreEqual(evt.Metric, serverEvent.metric_f);
		}

		private void SendAndExpectFailure(Event evt)
		{
			try
			{
				Console.WriteLine("sending evt and expecting failure");
				_client.SendEvents(new[] {evt});
				Assert.Fail("Exception should be thrown when server is down");
			}
			catch (Exception ex)
			{
				Assert.AreNotEqual(Client.State.Connected, _client.ConnectionState);
				Console.WriteLine("got send exception: {0}", ex.Message);
			}
		}

		[TestMethod]
		public void TestClientStartsWhenServerIsDown()
		{
			SendAndExpectFailure(new Event("foo", "bar", null, 1));

			Console.WriteLine("starting server");
			_server.Start();

			SendUntilSuccessfulAndAssert(new Event("foo", "bar", "this is a real event", 2));
		}

		[TestMethod]
		public void TestClientRecoversOnServerRestart()
		{
			Console.WriteLine("starting");
			_server.Start();

			Console.WriteLine("sending 1");
			SendUntilSuccessfulAndAssert(new Event("foo", "bar", "this is a real event", 1));
			
			_server.Stop();

			SendAndExpectFailure(new Event("baz", "urk", "another event", 2));

			Console.WriteLine("starting");
			_server.Start();

			Console.WriteLine("sending 2");
			SendUntilSuccessfulAndAssert(new Event("xyzzy", "zvzzt", "here we go again", 3), 60000);
		}

		[TestMethod]
		public void TestSuppressSendErrors()
		{
			_client.SuppressSendErrors = true;

			_client.SendEvent("foo", "bar", "this is a real event", 1);
			Assert.AreEqual(Client.State.Disconnected, _client.ConnectionState);
		}
	}
}
