namespace Riemann {
	public struct TickEvent {
		public TickEvent(string state, string description, float metricValue) {
			State = state;
			Description = description;
			MetricValue = metricValue;
		}

		public readonly string State;
		public readonly string Description;
		public readonly float MetricValue;
	}
}