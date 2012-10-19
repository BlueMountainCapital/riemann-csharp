using System;

namespace Riemann {
	public struct Event {
		public string Service;
		public string State;
		public string Description;
		public float Metric;
		public int TTL;

		public Event(string service, string state, string description, float metric, int ttl = 0) {
			if (state.Length > 255) {
				throw new ArgumentException("State parameter is too long, must be 255 characters or less", "state");
			}
			Service = service;
			State = state;
			Description = description;
			Metric = metric;
			TTL = ttl;
		}
	}
}