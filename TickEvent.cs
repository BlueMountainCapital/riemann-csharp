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
		///
		public TickEvent(string state, string description, float metricValue) {
			State = state;
			Description = description;
			MetricValue = metricValue;
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
	}
}