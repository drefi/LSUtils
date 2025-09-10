namespace LSUtils.EventSystem;

/// <summary>
/// Custom delegate for event condition checking to avoid conflicts with System.Func.
/// This delegate represents a condition that can be evaluated for an event instance.
/// </summary>
/// <typeparam name="TEvent">The event type to evaluate the condition for.</typeparam>
/// <param name="event">The event instance to evaluate.</param>
/// <returns>True if the condition is satisfied, false otherwise.</returns>
public delegate bool LSLegacyEventCondition<in TEvent>(TEvent @event) where TEvent : ILSEvent;
