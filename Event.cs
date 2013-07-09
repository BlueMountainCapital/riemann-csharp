using System;
using System.Collections.Generic;

namespace Riemann {
	///
	/// <summary>Represents a single event ready to send to Riemann.</summary>
	///
	public struct Event {
	        ///
		/// <summary>This is the service we are reporting events about.</summary>
		///
		public readonly string Service;

		///
		/// <summary>This is the current state of the service.</summary>
		///
		public readonly string State;
		
		///
		/// <summary>Additional details regarding the state of the service.</summary>
		///
		public readonly string Description;

		///
		/// <summary>A value which represents the state of the service.</summary>
		///
		public readonly float Metric;

		///
		/// <summary>Amount of time this metric should be considered valid.</summary>
		///
		public readonly int TTL;

        /// 
        /// <summary>Freeform list of strings, will override the tags set from the Client</summary>
        /// 
        public readonly IList<string> Tags;

		/// <summary>Constructs an event</summary>
		/// <param name="service">Service name</param>
		/// <param name="state">Current status of the service.</param>
		/// <param name="description">Additional details regarding the state of the service.</param>
		/// <param name="metric">A value which represents the state of the service.</param>
		/// <param name="ttl">Amount of time the value will stay valid for a service.</param>
		/// <exception cref="ArgumentException">Length of state is more than 255 characters.</exception>
		///
		public Event(string service, string state, string description, float metric, int ttl = 0) {
			if (state.Length > 255) {
				throw new ArgumentException("State parameter is too long, must be 255 characters or less", "state");
			}
			Service = service;
			State = state;
			Description = description;
			Metric = metric;
			TTL = ttl;
            Tags = new List<string>();
		}
	}
}