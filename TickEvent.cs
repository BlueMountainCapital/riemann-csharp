using System.Collections.Generic;

namespace Riemann {
	///
	/// <summary>Represents a single event for use with <see cref="Client.Tick" />.</summary>
	///
	public struct TickEvent {
	        ///
		/// <summary>Constructs an event</summary>
		/// <param name="state">Current status of the service.</param>
		/// <param name="description">Additional details regarding the state of the service.</param>
		/// <param name="metricValue">A value which represents the state of the service.</param>
		/// <param name="ttl">TTL for this event</param>
		/// <param name="tags">Tags to attach to this event</param>
		///
		public TickEvent(string state, string description, float metricValue, int? ttl = null, List<string> tags = null) {
			State = state;
			Description = description;
			MetricValue = metricValue;
	        TTL = ttl;
	        Tags = tags;
		}

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
		public readonly float MetricValue;

        /// <summary>
        /// Optional TTL to attach to the tick event (default TTL is 2x tick time)
        /// </summary>
	    public readonly int? TTL;

        /// <summary>
        /// Tags to attach to the tick event
        /// </summary>
	    public readonly List<string> Tags;
	}
}