using System;

namespace LSUtils.EventSystem;

/// <summary>
/// Generic event base class that provides strongly-typed access to the source object that triggered the event.
/// This class extends LSBaseEvent with type-safe instance access and is the recommended foundation
/// for most domain events that are associated with a specific entity or object.
/// </summary>
/// <typeparam name="TInstance">
/// The type of the object that triggered this event. Must be a reference type (class).
/// This provides compile-time type safety and eliminates the need for casting in event handlers.
/// </typeparam>
/// <remarks>
/// This class is ideal for domain events where you need direct access to the object that caused the event.
/// The typed instance is immutable once the event is created, ensuring referential integrity
/// throughout the event processing pipeline.
/// 
/// Use this class when:
/// - The event is directly related to a specific object instance
/// - Event handlers need type-safe access to the source object
/// - You want to maintain strong relationships between events and their sources
/// 
/// Example scenarios:
/// - User registration events (UserRegistrationEvent&lt;User&gt;)
/// - Order processing events (OrderProcessedEvent&lt;Order&gt;)
/// - Entity validation events (ValidationEvent&lt;T&gt; where T is your entity)
/// - State change events (StateChangedEvent&lt;StateMachine&gt;)
/// </remarks>
/// <example>
/// <code>
/// // Define a user-specific event
/// public class UserRegistrationEvent : LSEvent&lt;User&gt; {
///     public string RegistrationSource { get; }
///     public DateTime RegistrationTime { get; }
///     
///     public UserRegistrationEvent(User user, string source) : base(user) {
///         RegistrationSource = source;
///         RegistrationTime = DateTime.UtcNow;
///     }
/// }
/// 
/// // Usage in handlers
/// public LSPhaseResult HandleUserRegistration(UserRegistrationEvent evt, LSPhaseContext context) {
///     var user = evt.Instance; // Strongly typed access to User
///     
///     // Validate user properties
///     if (string.IsNullOrEmpty(user.Email)) {
///         evt.SetData("validation.error", "Email is required");
///         return LSPhaseResult.CANCEL;
///     }
///     
///     // Process registration
///     SendWelcomeEmail(user.Email);
///     evt.SetData("email.sent", true);
///     
///     return LSPhaseResult.CONTINUE;
/// }
/// </code>
/// </example>
public abstract class LSEvent<TInstance> : LSBaseEvent where TInstance : class {
    /// <summary>
    /// The strongly-typed instance that triggered this event.
    /// This property provides direct, type-safe access to the source object without casting,
    /// enabling clean and efficient event handler implementations.
    /// </summary>
    /// <value>
    /// The instance is guaranteed to be non-null and of the exact type specified by TInstance.
    /// This reference remains constant throughout the event's lifecycle, ensuring consistency
    /// across all phases of event processing.
    /// </value>
    public TInstance Instance { get; }

    /// <summary>
    /// Initializes a new event with the specified source instance.
    /// The instance becomes immutably associated with this event and cannot be changed
    /// after construction, ensuring referential integrity throughout event processing.
    /// </summary>
    /// <param name="instance">
    /// The instance that triggered this event. Must not be null.
    /// This object will be available to all event handlers through the Instance property.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when instance is null. All events must have a valid source instance
    /// to maintain the contract of strongly-typed event access.
    /// </exception>
    /// <remarks>
    /// The constructor captures the instance reference but does not perform deep copying.
    /// This means handlers have access to the live object, not a snapshot. If you need
    /// immutable event data, consider copying relevant properties into the event's
    /// custom data storage using SetData().
    /// 
    /// Example:
    /// <code>
    /// public UserUpdatedEvent(User user) : base(user) {
    ///     // Optionally store immutable snapshots
    ///     SetData("previous.email", user.Email);
    ///     SetData("previous.name", user.Name);
    /// }
    /// </code>
    /// </remarks>
    protected LSEvent(TInstance instance) {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance), 
            $"The source instance for event type {GetType().Name} cannot be null. " +
            "Events must be associated with a valid instance to maintain type safety.");
    }
}
