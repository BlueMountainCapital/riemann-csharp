using System;
using System.Collections.Generic;

namespace Riemann {
	public interface IClient {
		///
		/// <summary>Adds a tag to the current context (relative to this client). This call is not thread-safe.</summary>
		/// <param name='tag'>New tag to add to the Riemann events sent using this client.</param>
		///
		IDisposable Tag(string tag);

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
		IDisposable Tick(int tickTimeInSeconds, string service, Func<TickEvent> onTick);

		///
		/// <summary>Send many events to Riemann at once.</summary>
		/// <param name="events">
		/// Enumerable of the events to process.
		/// Enumerable will be enumerated after being passed in.
		/// </param>
		///
		void SendEvents(IEnumerable<Event> events);

	    ///  <summary>Send a single event to Riemann; assumes that the local host originated the event</summary>
	    ///  <param name='service'>Name of the service to push.</param>
	    ///  <param name='state'>State of the service; usual values are "ok", "critical", "warning"</param>
	    ///  <param name='description'>
	    ///  A description of the current state, if applicable.
	    ///  Use null or an empty string to denote no additional information.
	    ///  </param>
	    ///  <param name='metric'>A value related to the service.</param>
	    ///  <param name='ttl'>Number of seconds this event will be applicable for.</param>
	    ///  <param name="tags">List of tags to associate with this event</param>
	    ///  <param name="attributes">Optional arbitrary custom name/value content</param>
	    void SendEvent(string service, string state, string description, float metric,
	        int ttl = 0, List<string> tags = null, Dictionary<string, string> attributes = null);

	    /// 
	    ///  <summary>Send a single event to Riemann.</summary>
	    /// <param name="host">Event source host</param>
	    /// <param name='service'>Name of the service to push.</param>
	    /// <param name='state'>State of the service; usual values are "ok", "critical", "warning"</param>
	    /// <param name='description'>
	    ///  A description of the current state, if applicable.
	    ///  Use null or an empty string to denote no additional information.
	    /// </param>
	    /// <param name='metric'>A value related to the service.</param>
	    /// <param name='ttl'>Number of seconds this event will be applicable for.</param>
	    /// <param name="tags">List of tags to associate with this event</param>
	    /// <param name="attributes">Optional arbitrary name/value string content</param>
	    void SendEvent(string host, string service, string state, string description, float metric, int ttl = 0, 
            List<string> tags = null, Dictionary<string, string> attributes = null);

		///
		/// <summary>Queries Riemann</summary>
		/// <param name='query'>Query to send Riemann for process</param>
		/// <returns>List of States that answer the query.</returns>
		///
		IEnumerable<Proto.State> Query(string query);
	}
}