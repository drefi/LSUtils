using System.Numerics;

namespace LSUtils;
/// <summary>
/// A delegate with no parameters.
/// </summary>
public delegate void LSAction();

/// <summary>
/// A delegate with one parameter.
/// </summary>
/// <typeparam name="T">The type of the parameter.</typeparam>
/// <param name="obj">The parameter.</param>
public delegate void LSAction<in T>(T obj);

/// <summary>
/// A delegate with two parameters.
/// </summary>
/// <typeparam name="T1">The type of the first parameter.</typeparam>
/// <typeparam name="T2">The type of the second parameter.</typeparam>
/// <param name="arg1">The first parameter.</param>
/// <param name="arg2">The second parameter.</param>
public delegate void LSAction<in T1, in T2>(T1 arg1, T2 arg2);

/// <summary>
/// A delegate that handles a message.
/// </summary>
/// <param name="msg">The message to handle.</param>
/// <returns>True if the message was handled, false otherwise.</returns>
public delegate bool LSMessageHandler(string? msg);

/// <summary>
/// A delegate that listens for an event.
/// </summary>
/// <typeparam name="TEvent">The type of event to listen for.</typeparam>
/// <param name="listenerID">The identifier of the listener.</param>
/// <param name="event">The event.</param>
public delegate void LSListener<TEvent>(System.Guid listenerID, TEvent @event) where TEvent : LSEvent;

/// <summary>
/// A delegate that updates a tick.
/// </summary>
/// <param name="deltaTick">The time since the last tick.</param>
/// <param name="percentage">The percentage of the tick that has passed.</param>
public delegate void LSTickUpdateHandler(double deltaTick, float percentage, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null);
