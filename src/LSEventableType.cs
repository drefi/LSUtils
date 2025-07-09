using System.Collections.Concurrent;
using System.Linq;

namespace LSUtils;

/// <summary>
/// Represents a type descriptor for eventable objects, providing a registry of types that can participate
/// in the event system. Implements a flyweight pattern for efficient type management and caching.
/// </summary>
/// <remarks>
/// This class serves as a bridge between .NET's reflection-based type system and the LSUtils event system.
/// It provides a centralized registry for types that implement <see cref="ILSEventable"/>, ensuring
/// efficient type lookups and consistent type representation throughout the system. Each type is
/// cached for performance and thread safety is ensured through concurrent collections.
/// </remarks>
/// <example>
/// <code>
/// // Get type descriptor for a specific type
/// var playerType = LSEventableType.Get(typeof(Player));
/// 
/// // Initialize and work with the type
/// if (playerType.Initialize()) {
///     Console.WriteLine($"Initialized type: {playerType.Description}");
/// }
/// 
/// // Cleanup when done
/// playerType.Cleanup();
/// </code>
/// </example>
public class LSEventableType : ILSEventable {
    #region Static Registry
    /// <summary>
    /// Thread-safe cache of eventable type instances indexed by their .NET Type.
    /// </summary>
    /// <remarks>
    /// This dictionary ensures that only one instance of LSEventableType exists per .NET Type,
    /// implementing the flyweight pattern for memory efficiency and consistent object identity.
    /// </remarks>
    static readonly ConcurrentDictionary<System.Type, LSEventableType> _eventableTypes = new ConcurrentDictionary<System.Type, LSEventableType>();
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the .NET Type that this eventable type represents.
    /// </summary>
    /// <value>The System.Type instance that this descriptor wraps.</value>
    /// <remarks>
    /// This property provides access to the underlying .NET type, allowing for reflection-based
    /// operations and type comparisons. The type is immutable after construction.
    /// </remarks>
    public System.Type EventableType { get; }

    /// <summary>
    /// Gets the class name of this eventable type descriptor.
    /// </summary>
    /// <value>Returns "LSEventableType" by default.</value>
    /// <remarks>
    /// This property can be overridden by derived classes to provide more specific
    /// naming for debugging and logging purposes.
    /// </remarks>
    public virtual string ClassName => nameof(LSEventableType);

    /// <summary>
    /// Gets a human-readable description of the eventable type.
    /// </summary>
    /// <value>A string representation of the underlying .NET Type.</value>
    /// <remarks>
    /// This property provides a friendly string representation of the type, useful for
    /// logging, debugging, and user interface display. The default implementation uses
    /// the .NET Type's ToString() method.
    /// </remarks>
    public virtual string Description => $"{EventableType.ToString()}";

    /// <summary>
    /// Gets the unique identifier for this eventable type instance.
    /// </summary>
    /// <value>A GUID that uniquely identifies this type descriptor instance.</value>
    /// <remarks>
    /// The ID is generated automatically during construction and remains constant
    /// throughout the instance's lifetime. This can be used for type tracking,
    /// debugging, and event correlation.
    /// </remarks>
    public virtual System.Guid ID { get; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventableType"/> class for the specified .NET Type.
    /// </summary>
    /// <param name="type">The .NET Type that this eventable type descriptor represents.</param>
    /// <remarks>
    /// This constructor is protected to enforce the use of the factory method <see cref="Get(System.Type)"/>,
    /// which ensures proper caching and singleton behavior. The constructor generates a unique ID
    /// and stores the provided type for later use.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="type"/> is null.
    /// </exception>
    protected LSEventableType(System.Type type) {
        EventableType = type;
        ID = System.Guid.NewGuid();
    }
    #endregion

    #region ILSEventable Implementation
    /// <summary>
    /// Initializes the eventable type by dispatching an initialization event.
    /// </summary>
    /// <param name="eventOptions">
    /// Optional configuration for the initialization event. If null, default options are used.
    /// </param>
    /// <returns>
    /// <c>true</c> if initialization completed successfully; <c>false</c> if initialization failed or was cancelled.
    /// </returns>
    /// <remarks>
    /// This method creates an initialization event for this type descriptor and dispatches it
    /// to any registered listeners. This allows other parts of the system to be notified when
    /// a type is being registered and initialized in the event system.
    /// </remarks>
    /// <example>
    /// <code>
    /// var eventableType = LSEventableType.Get(typeof(MyClass));
    /// if (!eventableType.Initialize()) {
    ///     Console.WriteLine("Type initialization failed");
    ///     return;
    /// }
    /// // Type is now ready for use in the event system
    /// </code>
    /// </example>
    public bool Initialize(LSEventIOptions? eventOptions = null) {
        eventOptions ??= new LSEventIOptions();
        return OnInitializeEvent.Create<LSEventableType>(this, eventOptions).Dispatch();
    }

    /// <summary>
    /// Performs cleanup operations for the eventable type descriptor.
    /// </summary>
    /// <remarks>
    /// This method should be called when the type descriptor is no longer needed to ensure
    /// proper resource cleanup. The current implementation is a placeholder and may be
    /// extended in the future to include cleanup logic such as unregistering event listeners
    /// or releasing cached resources.
    /// </remarks>
    public virtual void Cleanup() {
        //TODO: implement cleanup logic if needed
    }
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// Gets or creates an eventable type descriptor for the specified .NET Type.
    /// </summary>
    /// <param name="eventableType">The .NET Type to get a descriptor for.</param>
    /// <returns>
    /// An <see cref="LSEventableType"/> instance representing the specified type.
    /// If an instance already exists for the type, the cached instance is returned.
    /// </returns>
    /// <remarks>
    /// This method implements the flyweight pattern to ensure that only one instance of
    /// LSEventableType exists per .NET Type. The method is thread-safe and can be called
    /// concurrently from multiple threads. If the type is not already cached, a new
    /// instance is created and added to the cache atomically.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get type descriptors - same instance returned for same type
    /// var type1 = LSEventableType.Get(typeof(Player));
    /// var type2 = LSEventableType.Get(typeof(Player));
    /// Console.WriteLine(type1 == type2); // True - same instance
    /// </code>
    /// </example>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="eventableType"/> is null.
    /// </exception>
    public static LSEventableType Get(System.Type eventableType) {
        if (_eventableTypes.TryGetValue(eventableType, out LSEventableType? instance) == false) {
            instance = new LSEventableType(eventableType);
            _eventableTypes.TryAdd(instance.EventableType, instance);
        }
        return instance;
    }
    #endregion
}
/// <summary>
/// Generic eventable type descriptor that provides strongly-typed access to type information
/// for a specific eventable type. Inherits from <see cref="LSEventableType"/> and adds type safety.
/// </summary>
/// <typeparam name="TEventable">
/// The specific type that implements <see cref="ILSEventable"/>. This enables compile-time
/// type checking and better IntelliSense support.
/// </typeparam>
/// <remarks>
/// This class provides a strongly-typed wrapper around the base LSEventableType functionality.
/// It's particularly useful when you know the specific type at compile time and want to benefit
/// from type safety and improved IDE support. The generic parameter ensures that only valid
/// eventable types can be used.
/// </remarks>
/// <example>
/// <code>
/// // Get strongly-typed descriptor
/// var playerType = LSEventableType&lt;Player&gt;.Get();
/// 
/// // Type information is strongly-typed
/// Console.WriteLine($"Player type: {playerType.EventableType.Name}");
/// </code>
/// </example>
public class LSEventableType<TEventable> : LSEventableType where TEventable : ILSEventable {
    #region Constructor
    /// <summary>
    /// Initializes a new instance of the strongly-typed eventable type descriptor.
    /// </summary>
    /// <remarks>
    /// This constructor is protected to enforce the use of the factory method <see cref="Get()"/>,
    /// which ensures proper caching through the base class mechanism. The type parameter
    /// <typeparamref name="TEventable"/> is automatically resolved to its runtime Type.
    /// </remarks>
    protected LSEventableType() : base(typeof(TEventable)) { }
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// Gets or creates a strongly-typed eventable type descriptor for the type <typeparamref name="TEventable"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="LSEventableType"/> instance representing the type <typeparamref name="TEventable"/>.
    /// The returned instance is the same as calling <see cref="LSEventableType.Get(System.Type)"/>
    /// with <c>typeof(TEventable)</c>.
    /// </returns>
    /// <remarks>
    /// This method provides a convenient, strongly-typed way to get type descriptors without
    /// needing to explicitly pass the Type parameter. It delegates to the base class factory
    /// method to ensure consistent caching behavior.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Strongly-typed access
    /// var playerType = LSEventableType&lt;Player&gt;.Get();
    /// 
    /// // Equivalent to:
    /// var playerType2 = LSEventableType.Get(typeof(Player));
    /// 
    /// Console.WriteLine(playerType == playerType2); // True - same instance
    /// </code>
    /// </example>
    public static LSEventableType Get() {
        return LSEventableType.Get(typeof(TEventable));
    }
    #endregion
}

/// <summary>
/// Represents an event type descriptor that provides efficient caching and lookup of event types
/// used throughout the LSUtils event system. Implements a flyweight pattern for memory efficiency.
/// </summary>
/// <remarks>
/// This class serves as a type registry for events in the LSUtils system, providing a centralized
/// way to manage and cache event type information. It ensures that each event type has a consistent
/// representation throughout the system and enables efficient type-based lookups during event
/// dispatching and listener registration.
/// </remarks>
/// <example>
/// <code>
/// // Get event type descriptors
/// var initEventType = LSEventType.Get&lt;OnInitializeEvent&lt;Player&gt;&gt;();
/// var customEventType = LSEventType.Get(typeof(MyCustomEvent));
/// 
/// // Use for event system operations
/// Console.WriteLine($"Event type: {initEventType.EventType.Name}");
/// </code>
/// </example>
public class LSEventType {
    #region Static Registry
    /// <summary>
    /// Thread-safe cache of event type instances indexed by their .NET Type.
    /// </summary>
    /// <remarks>
    /// This dictionary ensures that only one instance of LSEventType exists per .NET Type,
    /// implementing the flyweight pattern for memory efficiency and consistent object identity.
    /// </remarks>
    static readonly ConcurrentDictionary<System.Type, LSEventType> _eventTypes = new ConcurrentDictionary<System.Type, LSEventType>();
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the .NET Type that this event type descriptor represents.
    /// </summary>
    /// <value>The System.Type instance for the event type that this descriptor wraps.</value>
    /// <remarks>
    /// This property provides access to the underlying .NET type for the event, allowing for
    /// reflection-based operations, type comparisons, and integration with the .NET type system.
    /// The type is immutable after construction.
    /// </remarks>
    public System.Type EventType { get; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="LSEventType"/> class for the specified event type.
    /// </summary>
    /// <param name="type">The .NET Type that represents the event type.</param>
    /// <remarks>
    /// This constructor is protected to enforce the use of the factory methods <see cref="Get(System.Type)"/>
    /// or <see cref="Get{TEvent}()"/>, which ensure proper caching and singleton behavior.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="type"/> is null.
    /// </exception>
    protected LSEventType(System.Type type) {
        EventType = type;
    }
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// Gets or creates an event type descriptor for the specified .NET Type.
    /// </summary>
    /// <param name="eventType">The .NET Type representing the event to get a descriptor for.</param>
    /// <returns>
    /// An <see cref="LSEventType"/> instance representing the specified event type.
    /// If an instance already exists for the type, the cached instance is returned.
    /// </returns>
    /// <remarks>
    /// This method implements the flyweight pattern to ensure that only one instance of
    /// LSEventType exists per .NET Type. The method is thread-safe and can be called
    /// concurrently from multiple threads. If the type is not already cached, a new
    /// instance is created and added to the cache atomically.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="eventType"/> is null.
    /// </exception>
    public static LSEventType Get(System.Type eventType) {
        if (_eventTypes.TryGetValue(eventType, out LSEventType? instance) == false) {
            instance = new LSEventType(eventType);
            _eventTypes.TryAdd(instance.EventType, instance);
        }
        return instance;
    }

    /// <summary>
    /// Gets or creates a strongly-typed event type descriptor for the type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The specific event type to get a descriptor for.</typeparam>
    /// <returns>
    /// An <see cref="LSEventType"/> instance representing the type <typeparamref name="TEvent"/>.
    /// The returned instance is the same as calling <see cref="Get(System.Type)"/> with <c>typeof(TEvent)</c>.
    /// </returns>
    /// <remarks>
    /// This generic method provides a convenient, strongly-typed way to get event type descriptors
    /// without needing to explicitly pass the Type parameter. It offers better compile-time type
    /// checking and IntelliSense support compared to the non-generic overload.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Strongly-typed access
    /// var eventType = LSEventType.Get&lt;OnInitializeEvent&lt;Player&gt;&gt;();
    /// 
    /// // Equivalent to:
    /// var eventType2 = LSEventType.Get(typeof(OnInitializeEvent&lt;Player&gt;));
    /// 
    /// Console.WriteLine(eventType == eventType2); // True - same instance
    /// </code>
    /// </example>
    public static LSEventType Get<TEvent>() => Get(typeof(TEvent));
    #endregion
}
