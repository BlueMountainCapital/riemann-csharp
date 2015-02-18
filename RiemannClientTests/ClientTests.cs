
namespace RiemannClientTests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Riemann;

    [TestClass]
    public class ClientTests
    {
        private MockServer _server;
        private Client _client;

        [TestInitialize]
        public void Setup()
        {
            _server = new MockServer();
            _client = new Client("localhost", 5558, false, true);
            _client.SuppressSendErrors = false;
            _server.Start();
        }
        [TestCleanup]
        public void TearDown()
        {
            _client.Dispose();
            _server.Stop();
        }
        [TestMethod]
        public void SentEventsShouldHaveTimestampSpecified()
        {
            _client.SendEvent("test", String.Empty, String.Empty, 123);
            Assert.AreEqual(1, _server.TcpMessageQueue.Count);
            Assert.AreEqual(1, _server.TcpMessageQueue.First().events.Count());
            Assert.AreNotEqual(0, _server.TcpMessageQueue.First().events.First().time);
        }
    }
}
