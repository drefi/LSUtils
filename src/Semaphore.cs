using System.Collections.Generic;
using System.Linq;

namespace LSUtils;

public class Semaphore {
    #region Fields
    public static string FailMsgSeparator { get; internal set; } = " | ";
    public string ClassName => nameof(Semaphore);
    private readonly Queue<System.Guid> _queue = new Queue<System.Guid>();
    protected List<string> _failMessages;
    private object _semaphoreLock = new object();
    /// <summary>
    /// Event triggered when a failure occurs.
    /// </summary>
    /// <param name="msg">The failure message.</param>
    /// <remarks>
    /// The event handler should return true if the failure was handled, false otherwise.
    /// </remarks>
    public event LSAction<string>? FailureCallback;
    /// <summary>
    /// Event triggered when the semaphore is cancelled.
    /// </summary>
    /// <remarks>
    /// The function takes no input and returns no value.
    /// </remarks>
    public event LSAction? CancelCallback;
    /// <summary>
    /// Event triggered when the semaphore is done (i.e., the counter reaches 0).
    /// </summary>
    /// <remarks>
    /// The function takes no input and returns no value.
    /// </remarks>
    public event LSAction? SuccessCallback;
    /// <summary>
    /// Gets a value indicating whether the semaphore operation is complete.
    /// </summary>
    public bool IsDone { get; protected set; }
    /// <summary>
    /// Gets a value indicating whether the semaphore encountered a failure.
    /// </summary>
    public bool HasFailed { get; protected set; }
    /// <summary>
    /// Gets a value indicating whether the semaphore has been cancelled.
    /// </summary>
    public bool IsCancelled { get; protected set; }
    /// <summary>
    /// Gets the number of locks on the semaphore.
    /// </summary>
    /// <value>The count.</value>
    public int Count => _queue?.Count ?? -1;
    #endregion
    /// <summary>
    /// Initializes a new instance of the Semaphore class. Sets up the internal state,
    /// including the queue, failure and cancellation flags, and initializes the callbacks.
    /// Also triggers an initial wait on the semaphore.
    /// </summary>
    protected Semaphore() {
        HasFailed = false;
        IsCancelled = false;
        CancelCallback = null;
        SuccessCallback = null;
        _failMessages = new List<string>();
    }
    /// <summary>
    /// Increment the semaphore's counter by the given number of locks.
    /// This is a non-blocking call.
    /// </summary>
    /// <param name="locks">The number of locks to increment the counter by.</param>
    public void Wait(System.Guid signalID = default) {
        lock (_semaphoreLock) {
            if (IsDone || IsCancelled) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            if (signalID == default || signalID == System.Guid.Empty) signalID = System.Guid.NewGuid();
            _queue.Enqueue(signalID);
        }
    }
    public void Signal() => Signal(out _);
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
    public void Failure(string msg) => Failure(out _, msg);
    public int Failure(out System.Guid signalID, string? failMessage = null) {
        lock (_semaphoreLock) {
            signalID = System.Guid.Empty;
            if (IsDone || IsCancelled) {
                throw new LSException($"semaphore_already_{(IsDone ? "done" : "cancelled")}");
            }
            if (failMessage != null) _failMessages.Add(failMessage);
            HasFailed = true;
            return Signal(out signalID);
        }
    }
    public void Cancel() => Cancel(out _);
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

    /// <summary>
    /// Creates and returns a new instance of the Semaphore class.
    /// </summary>
    /// <returns>A new Semaphore instance.</returns>
    public static Semaphore Create(int locks = 1) {
        if (locks <= 0) locks = 1; // Ensure at least one lock
        Semaphore semaphore = new Semaphore();
        for (int i = 0; i < locks; i++) semaphore.Wait(System.Guid.NewGuid());
        return semaphore;
    }
}
