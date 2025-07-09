using System.Collections.Generic;
using System.Linq;

namespace LSUtils;

/// <summary>
/// A thread-safe semaphore implementation that manages a queue of signal IDs and provides
/// synchronization capabilities with failure handling and cancellation support.
/// </summary>
/// <remarks>
/// This semaphore uses GUID-based signaling for tracking individual operations.
/// It supports success, failure, and cancellation callbacks, making it suitable for
/// complex asynchronous operations that require coordination and error handling.
/// </remarks>
public class Semaphore {
    #region Static Configuration
    /// <summary>
    /// Gets or sets the separator used to join multiple failure messages.
    /// </summary>
    /// <value>The default separator is " | ".</value>
    public static string FailMsgSeparator { get; internal set; } = " | ";
    #endregion

    #region Private Fields
    /// <summary>
    /// The queue containing signal IDs for pending operations.
    /// </summary>
    private readonly Queue<System.Guid> _queue = new Queue<System.Guid>();
    
    /// <summary>
    /// Collection of failure messages accumulated during semaphore operations.
    /// </summary>
    protected List<string> _failMessages;
    
    /// <summary>
    /// Thread synchronization lock for semaphore operations.
    /// </summary>
    private object _semaphoreLock = new object();
    #endregion

    #region Public Properties
    /// <summary>
    /// Gets the class name of this semaphore instance.
    /// </summary>
    /// <value>Returns "Semaphore".</value>
    public string ClassName => nameof(Semaphore);

    /// <summary>
    /// Gets a value indicating whether the semaphore operation is complete.
    /// </summary>
    /// <value>
    /// <c>true</c> if the semaphore has completed all operations (counter reached 0); otherwise, <c>false</c>.
    /// </value>
    public bool IsDone { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether the semaphore encountered a failure.
    /// </summary>
    /// <value>
    /// <c>true</c> if at least one failure was reported; otherwise, <c>false</c>.
    /// </value>
    public bool HasFailed { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether the semaphore has been cancelled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the semaphore was cancelled; otherwise, <c>false</c>.
    /// </value>
    public bool IsCancelled { get; protected set; }

    /// <summary>
    /// Gets the number of pending signals in the semaphore queue.
    /// </summary>
    /// <value>
    /// The count of pending operations, or -1 if the queue is null.
    /// </value>
    public int Count => _queue?.Count ?? -1;
    #endregion

    #region Events
    /// <summary>
    /// Event triggered when a failure occurs during semaphore operations.
    /// </summary>
    /// <remarks>
    /// The event provides the aggregated failure message containing all reported failures.
    /// Multiple failure messages are joined using the <see cref="FailMsgSeparator"/>.
    /// </remarks>
    public event LSAction<string>? FailureCallback;

    /// <summary>
    /// Event triggered when the semaphore is cancelled.
    /// </summary>
    /// <remarks>
    /// This event is raised when <see cref="Cancel()"/> is called, regardless of whether
    /// there were any failures. The event handler receives no parameters.
    /// </remarks>
    public event LSAction? CancelCallback;

    /// <summary>
    /// Event triggered when the semaphore completes successfully.
    /// </summary>
    /// <remarks>
    /// This event is raised when the semaphore counter reaches 0 and no failures occurred.
    /// The event handler receives no parameters.
    /// </remarks>
    public event LSAction? SuccessCallback;
    #endregion
    #region Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="Semaphore"/> class.
    /// </summary>
    /// <remarks>
    /// Sets up the internal state including the failure message collection and
    /// initializes all flags to their default values. The semaphore starts in
    /// an active state, ready to accept wait operations.
    /// </remarks>
    protected Semaphore() {
        HasFailed = false;
        IsCancelled = false;
        CancelCallback = null;
        SuccessCallback = null;
        _failMessages = new List<string>();
    }
    #endregion

    #region Factory Methods
    /// <summary>
    /// Creates and returns a new instance of the <see cref="Semaphore"/> class with the specified number of locks.
    /// </summary>
    /// <param name="locks">The initial number of locks to create. Must be greater than 0.</param>
    /// <returns>A new <see cref="Semaphore"/> instance with the specified number of pending operations.</returns>
    /// <remarks>
    /// Each lock corresponds to a pending operation that must be signaled before the semaphore completes.
    /// If the specified number of locks is less than or equal to 0, it will be set to 1.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create a semaphore that waits for 3 operations to complete
    /// var semaphore = Semaphore.Create(3);
    /// 
    /// // Set up callbacks
    /// semaphore.SuccessCallback = () => Console.WriteLine("All operations completed!");
    /// semaphore.FailureCallback = (msg) => Console.WriteLine($"Failed: {msg}");
    /// 
    /// // Signal completion of operations
    /// semaphore.Signal(); // 2 remaining
    /// semaphore.Signal(); // 1 remaining
    /// semaphore.Signal(); // 0 remaining - triggers SuccessCallback
    /// </code>
    /// </example>
    public static Semaphore Create(int locks = 1) {
        if (locks <= 0) locks = 1; // Ensure at least one lock
        Semaphore semaphore = new Semaphore();
        for (int i = 0; i < locks; i++) semaphore.Wait(System.Guid.NewGuid());
        return semaphore;
    }
    #endregion
    #region Core Operations
    /// <summary>
    /// Adds a new signal to the semaphore queue, incrementing the number of pending operations.
    /// </summary>
    /// <param name="signalID">
    /// The unique identifier for this signal. If not provided or empty, a new GUID will be generated.
    /// </param>
    /// <exception cref="LSException">
    /// Thrown when the semaphore is already done or cancelled.
    /// </exception>
    /// <remarks>
    /// This is a non-blocking operation that adds the signal to the internal queue.
    /// Each call to <see cref="Wait(System.Guid)"/> must be matched with a corresponding 
    /// call to <see cref="Signal()"/> or <see cref="Failure(string, bool)"/>.
    /// </remarks>
    public void Wait(System.Guid signalID = default) {
        lock (_semaphoreLock) {
            if (IsDone || IsCancelled) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            if (signalID == default || signalID == System.Guid.Empty) signalID = System.Guid.NewGuid();
            _queue.Enqueue(signalID);
        }
    }

    /// <summary>
    /// Signals the completion of one operation, removing one signal from the queue.
    /// </summary>
    /// <remarks>
    /// If this is the last signal in the queue and no failures occurred, the 
    /// <see cref="SuccessCallback"/> will be triggered. If failures occurred,
    /// the <see cref="FailureCallback"/> will be triggered instead.
    /// </remarks>
    /// <exception cref="LSException">
    /// Thrown when the semaphore is already done, cancelled, or has no pending signals.
    /// </exception>
    public void Signal() => Signal(out _);

    /// <summary>
    /// Signals the completion of one operation and returns the signal ID that was processed.
    /// </summary>
    /// <param name="signalID">The GUID of the signal that was processed from the queue.</param>
    /// <returns>
    /// The number of remaining signals in the queue, 0 if completed successfully, 
    /// or -1 if completed with failures.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when the semaphore is already done, cancelled, or has no pending signals.
    /// </exception>
    public int Signal(out System.Guid signalID) {
        lock (_semaphoreLock) {
            signalID = System.Guid.Empty;
            if (IsDone || IsCancelled) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            if (_queue.Count == 0) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            signalID = _queue.Dequeue();
            if (_queue.Count > 0) return _queue.Count;
            IsDone = true;
            if (HasFailed == false) {
                SuccessCallback?.Invoke();
                return 0;
            } else {
                if (FailureCallback != null) {
                    string failureMessage = string.Join(FailMsgSeparator, _failMessages);
                    FailureCallback(failureMessage);
                }
                return -1; // Indicating failure
            }
        }
    }
    #endregion
    #region Error Handling
    /// <summary>
    /// Reports a failure for the current operation and optionally cancels the semaphore.
    /// </summary>
    /// <param name="msg">The failure message to record.</param>
    /// <param name="cancel">
    /// If <c>true</c>, cancels the semaphore after recording the failure; 
    /// if <c>false</c>, continues normal operation.
    /// </param>
    /// <remarks>
    /// The failure message will be added to the collection of failure messages.
    /// If this is not a cancelling failure, the operation continues and signals normally.
    /// </remarks>
    public void Failure(string msg, bool cancel = false) => Failure(out _, msg, cancel);

    /// <summary>
    /// Reports a failure for the current operation and returns the processed signal ID.
    /// </summary>
    /// <param name="signalID">The GUID of the signal that was processed from the queue.</param>
    /// <param name="failMessage">The failure message to record. Can be null.</param>
    /// <param name="cancel">
    /// If <c>true</c>, cancels the semaphore after recording the failure; 
    /// if <c>false</c>, signals normally after recording the failure.
    /// </param>
    /// <returns>
    /// The number of remaining signals in the queue, or -1 if the semaphore was cancelled or completed with failures.
    /// </returns>
    /// <exception cref="LSException">
    /// Thrown when the semaphore is already done or cancelled.
    /// </exception>
    /// <remarks>
    /// Multiple failure messages are accumulated and will be joined with <see cref="FailMsgSeparator"/> 
    /// when the <see cref="FailureCallback"/> is triggered.
    /// </remarks>
    public int Failure(out System.Guid signalID, string? failMessage = null, bool cancel = false) {
        lock (_semaphoreLock) {
            signalID = System.Guid.Empty;
            if (IsDone || IsCancelled) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            if (failMessage != null) _failMessages.Add(failMessage);
            HasFailed = true;
            if (cancel) {
                Cancel(out _);
                return -1; // Indicating failure and cancellation
            }
            return Signal(out signalID);
        }
    }
    #endregion

    #region Cancellation
    /// <summary>
    /// Cancels the semaphore, clearing all pending signals and triggering appropriate callbacks.
    /// </summary>
    /// <remarks>
    /// When cancelled, any remaining signals are cleared and the <see cref="CancelCallback"/> is triggered.
    /// If there were any failures, the <see cref="FailureCallback"/> is also triggered.
    /// </remarks>
    public void Cancel() => Cancel(out _);

    /// <summary>
    /// Cancels the semaphore and returns the IDs of all remaining signals that were cleared.
    /// </summary>
    /// <param name="remainingSignalIDs">
    /// An array containing the GUIDs of all signals that were pending when cancellation occurred.
    /// </param>
    /// <returns>The number of signals that were cancelled.</returns>
    /// <exception cref="LSException">
    /// Thrown when the semaphore is already done or cancelled.
    /// </exception>
    /// <remarks>
    /// This operation immediately clears the signal queue and sets the semaphore to a cancelled state.
    /// Both <see cref="FailureCallback"/> (if there were failures) and <see cref="CancelCallback"/> 
    /// may be triggered as a result of this operation.
    /// </remarks>
    public int Cancel(out System.Guid[] remainingSignalIDs) {
        lock (_semaphoreLock) {
            if (IsDone || IsCancelled) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            remainingSignalIDs = _queue.ToArray();
            int remainingSignal = _queue.Count;
            _queue.Clear();
            IsCancelled = true;
            if (HasFailed && FailureCallback != null) {
                string failureMessage = string.Join(FailMsgSeparator, _failMessages);
                FailureCallback(failureMessage);
            }
            CancelCallback?.Invoke();
            return remainingSignal;
        }
    }
    #endregion
}
