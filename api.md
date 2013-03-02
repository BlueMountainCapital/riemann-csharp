---
layout: api
title: riemann.cs API
---

## Riemann namespace

### <a id="Client">Client</a>

#### Client(String host, short port)

Constructs a new Client with the specified host, port

- **host**: Remote hostname to connect to. Default: localhost
- **port**: Port to connect to. Default: 5555

#### Tag(String tag)

Adds a tag to the current context (relative to this client). This call is not thread-safe.

- **tag**: New tag to add to the Riemann events sent using this client.

#### Tick(int tickTimeInSeconds, String service, Func&lt;[TickEvent](#TickEvent)&gt;

After tickTimeInSeconds seconds, onTick will be invoked. The resulting
[TickEvent](#TickEvent) is composed with the service to
generate an Event.

- **tickTimeInSeconds**: Number of seconds to wait before calling the
  event back. <em>Because only a single thread calls the events back,
  it may be called back sooner.</em>

- **service**: Name of the service to send to Riemann

- **onTick**: Function to call back after wait period

- Returns a disposable that, if called, will remove this callback from
  getting called. <em>An additional tick may elapse after removal, due
  to the multithreaded nature.</em>

#### SendEvents(IEnumerable&lt;[Event](#Event)&gt; events)

Send many events to Riemann at once.

- **events**: Enumerable of the events to process. Enumerable will be
  enumerated after being passed in.

#### SendEvent(String service, String state, String description, float metric, int ttl)

Send a single event to Riemann.

- **service**: Name of the service to push.

- **state**: State of the service; usual values are "ok", "critical",
  "warning"

- **description**: A description of the current state, if applicable.
  Use null or an empty string to denote no additional information.

- **metric**: A value related to the service.

- **ttl**: Number of seconds this event will be applicable for.

#### Query(String query)

Queries Riemann

- **query**: Query to send to Riemann for process
- Returns list of States that answer the query.

#### Dispose()

Cleans up state related to this client.

#### Finalize()

Closes connections.

### <a id="Event">Event</a>

#### Event(String service, String state, String description, float metric, int ttl)

Constructs an event

- **service**: Service name

- **state**: Current status of the service.

- **description**: Additional details regarding the state of the
  service.

- **metric**: A value which represents the state of the service.

- **ttl**: Amount of time the value will stay valid for a service.

- Throws *ArgumentException*: Length of state is more than 255
  characters.

#### String Service

This is the service we are reporting events about.

#### String State

This is the current state of the service.

#### String Description

Additional details regarding the state of the service.

#### float Metric

A value which represents the state of the service.

#### int TTL

Amount of time this metric should be considered valid.

### <a id="TickEvent">TickEvent</a>

#### TickEvent(String state, String description, float metricValue)

Constructs an event

- **state**: Current status of the service.

- **description**: Additional details regarding the state of the
  service.

- **metricValue**: A value which represents the state of the service.

#### String State

This is the current state of the service.

#### String Description

Additional details regarding the state of the service.

#### float MetricValue

A value which represents the state of the service.