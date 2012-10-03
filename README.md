# Riemann C# driver

This driver provides a thin layer of abstraction on top of the
protobuf layer. The two main methods are:

     SendEvent(string service, string state, string description, float
     metric, int ttl = 0)

Sends a single event to Riemann

     Tick(int tickTimeInSeconds, string service, Func<TickEvent>
     onTick)

Registers a timer that will send the value returned by onTick at
tickTimeInSeconds seconds. Returns an IDisposable which can be used to
remove the timer.