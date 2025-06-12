using LSUtils.LSLocale;

namespace LSUtils;

/// <summary>
/// Provides event-based notifications for printing, warnings, errors, confirmations, and general notifications.
/// </summary>
public static class LSSignals {
    /// <summary>
    /// Gets the class name.
    /// </summary>
    public static string ClassName => nameof(LSSignals);

    #region Events

    /// <summary>
    /// Event triggered for print messages.
    /// </summary>
    public class OnPrintEvent : LSEvent {
        /// <summary>
        /// The print message.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnPrintEvent"/> class.
        /// </summary>
        /// <param name="message">The message to print.</param>
        public OnPrintEvent(string message) : base(System.Guid.NewGuid()) => Message = message;

        /// <summary>
        /// Registers a listener for print events.
        /// </summary>
        /// <param name="listener">The listener delegate.</param>
        /// <param name="triggers">Number of times to trigger (-1 for unlimited).</param>
        /// <param name="listenerID">Optional listener ID.</param>
        /// <param name="onFailure">Callback if registration fails.</param>
        /// <param name="dispatcher">Optional dispatcher instance.</param>
        /// <returns>The listener's unique identifier.</returns>
        public static System.Guid Register(LSListener<OnPrintEvent> listener, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
            dispatcher = dispatcher ?? LSDispatcher.Instance;
            ILSEventable[] instances = new ILSEventable[0];
            return dispatcher.Register<OnPrintEvent>(listener, instances, triggers, listenerID, onFailure);
        }
    }

    /// <summary>
    /// Event triggered for error messages.
    /// </summary>
    public class OnErrorEvent : LSEvent {
        /// <summary>
        /// The error message.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnErrorEvent"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public OnErrorEvent(string message) : base(System.Guid.NewGuid()) => Message = message ?? string.Empty;

        /// <summary>
        /// Registers a listener for error events.
        /// </summary>
        /// <param name="listener">The listener delegate.</param>
        /// <param name="triggers">Number of times to trigger (-1 for unlimited).</param>
        /// <param name="listenerID">Optional listener ID.</param>
        /// <param name="onFailure">Callback if registration fails.</param>
        /// <param name="dispatcher">Optional dispatcher instance.</param>
        /// <returns>The listener's unique identifier.</returns>
        public static System.Guid Register(LSListener<OnErrorEvent> listener, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
            dispatcher = dispatcher ?? LSDispatcher.Instance;
            ILSEventable[] instances = new ILSEventable[0];
            return dispatcher.Register<OnErrorEvent>(listener, instances, triggers, listenerID, onFailure);
        }
    }

    /// <summary>
    /// Event triggered for warning messages.
    /// </summary>
    public class OnWarningEvent : LSEvent {
        /// <summary>
        /// The warning message.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnWarningEvent"/> class.
        /// </summary>
        /// <param name="message">The warning message.</param>
        public OnWarningEvent(string message) : base(System.Guid.NewGuid()) => Message = message ?? string.Empty;

        /// <summary>
        /// Registers a listener for warning events.
        /// </summary>
        /// <param name="listener">The listener delegate.</param>
        /// <param name="triggers">Number of times to trigger (-1 for unlimited).</param>
        /// <param name="listenerID">Optional listener ID.</param>
        /// <param name="onFailure">Callback if registration fails.</param>
        /// <param name="dispatcher">Optional dispatcher instance.</param>
        /// <returns>The listener's unique identifier.</returns>
        public static System.Guid Register(LSListener<OnWarningEvent> listener, int triggers = -1, System.Guid listenerID = default, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
            dispatcher = dispatcher ?? LSDispatcher.Instance;
            ILSEventable[] instances = new ILSEventable[0];
            return dispatcher.Register<OnWarningEvent>(listener, instances, triggers, listenerID, onFailure);
        }
    }

    /// <summary>
    /// Event triggered for confirmation messages.
    /// </summary>
    public class OnConfirmationEvent : LSEvent {
        /// <summary>
        /// The confirmation dialog title.
        /// </summary>
        public string Title { get; protected set; }
        /// <summary>
        /// The confirmation dialog description.
        /// </summary>
        public string Description { get; protected set; }
        /// <summary>
        /// The label for the confirm button.
        /// </summary>
        public string ButtonConfirmLabel { get; protected set; }
        /// <summary>
        /// The callback for the confirm button.
        /// </summary>
        public LSAction? ButtonConfirmCallback { get; protected set; }
        /// <summary>
        /// The label for the cancel button.
        /// </summary>
        public string ButtonCancelText { get; protected set; }
        /// <summary>
        /// The callback for the cancel button.
        /// </summary>
        public LSAction? ButtonCancelCallback { get; protected set; }
        /// <summary>
        /// Indicates if the dialog is cancellable.
        /// </summary>
        public bool Cancellable { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnConfirmationEvent"/> class with only a confirm button.
        /// </summary>
        public OnConfirmationEvent(string title, string description, string buttonConfirmLabel, LSAction buttonConfirmCallback) : base(System.Guid.NewGuid()) {
            Title = title;
            Description = description;
            ButtonConfirmLabel = buttonConfirmLabel;
            ButtonConfirmCallback = buttonConfirmCallback;
            ButtonCancelText = string.Empty;
            ButtonCancelCallback = null;
            Cancellable = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnConfirmationEvent"/> class with confirm and cancel buttons.
        /// </summary>
        public OnConfirmationEvent(string title, string description, string buttonConfirmLabel, LSAction buttonConfirmCallback, string buttonCancelLabel, LSAction buttonCancelCallback) : base(System.Guid.NewGuid()) {
            Title = title;
            Description = description;
            ButtonConfirmLabel = buttonConfirmLabel;
            ButtonConfirmCallback = buttonConfirmCallback;
            ButtonCancelText = buttonCancelLabel;
            ButtonCancelCallback = buttonCancelCallback;
            Cancellable = true;
        }
    }

    /// <summary>
    /// Event triggered for general notifications.
    /// </summary>
    public class OnNotifyEvent : LSEvent {
        /// <summary>
        /// The notification message.
        /// </summary>
        public string Message { get; protected set; }
        /// <summary>
        /// The notification description.
        /// </summary>
        public string Description { get; protected set; }
        /// <summary>
        /// Indicates if the notification can be dismissed.
        /// </summary>
        public bool AllowDismiss { get; protected set; }
        /// <summary>
        /// The timeout for the notification (in seconds).
        /// </summary>
        public double Timeout { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnNotifyEvent"/> class.
        /// </summary>
        /// <param name="message">The notification message.</param>
        /// <param name="description">The notification description.</param>
        /// <param name="allowDismiss">Whether the notification can be dismissed.</param>
        /// <param name="timeout">Timeout in seconds.</param>
        public OnNotifyEvent(string message, string description = "", bool allowDismiss = true, double timeout = 3f) : base(System.Guid.NewGuid()) {
            Description = description;
            Message = message;
            AllowDismiss = allowDismiss;
            Timeout = timeout;
        }
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Dispatches an error event with the provided message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Error(string message, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnErrorEvent>() == 0) throw new LSNotificationException($"no_error_handler:{message}");
        OnErrorEvent @event = new OnErrorEvent(message);
        @event.FailureCallback += onFailure;
        return @event.Dispatch(onFailure, dispatcher);
    }

    /// <summary>
    /// Dispatches an error event with a custom error callback.
    /// </summary>
    /// <param name="errorCallback">The error callback to invoke.</param>
    /// <param name="message">The error message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Error(LSMessageHandler? errorCallback, string message, LSDispatcher? dispatcher = null) {
        if (errorCallback == null) return Error(message, (msg) => false, dispatcher);
        return errorCallback(message);
    }

    /// <summary>
    /// Dispatches a warning event with the provided message.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Warning(string message, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnWarningEvent>() == 0) throw new LSNotificationException($"no_warning_handler:{message}");
        OnWarningEvent @event = new OnWarningEvent(message);
        return @event.Dispatch(onFailure, dispatcher);
    }

    /// <summary>
    /// Dispatches a warning event with a custom warning callback.
    /// </summary>
    /// <param name="warningCallback">The warning callback to invoke.</param>
    /// <param name="message">The warning message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Warning(LSMessageHandler? warningCallback, string message, LSDispatcher? dispatcher = null) {
        if (warningCallback == null) return Warning(message, (msg) => false, dispatcher);
        return warningCallback(message);
    }

    /// <summary>
    /// Dispatches a print event with the provided message.
    /// </summary>
    /// <param name="message">The print message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Print(string message, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnPrintEvent>() == 0) throw new LSNotificationException($"no_print_handler:{message}");
        OnPrintEvent @event = new OnPrintEvent(message);
        @event.FailureCallback += onFailure;
        return @event.Dispatch(onFailure, dispatcher);
    }

    /// <summary>
    /// Dispatches a print event with a custom print callback.
    /// </summary>
    /// <param name="printCallback">The print callback to invoke.</param>
    /// <param name="message">The print message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Print(LSMessageHandler? printCallback, string message, LSDispatcher? dispatcher = null) {
        if (printCallback == null) return Print(message, (msg) => false, dispatcher);
        printCallback(message);
        return true;
    }

    /// <summary>
    /// Dispatches a notification event with the provided details.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="description">The notification description.</param>
    /// <param name="allowDismiss">Indicates if the notification can be dismissed.</param>
    /// <param name="timeout">Timeout for the notification (in seconds).</param>
    /// <param name="onSuccess">Callback if dispatch succeeds.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static void Notify(string message, string description = "", bool allowDismiss = false, double timeout = 3f, LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnNotifyEvent>() == 0) throw new LSNotificationException(message);
        OnNotifyEvent @event = new OnNotifyEvent(message, description, allowDismiss, timeout);
        @event.SuccessCallback += onSuccess;
        @event.FailureCallback += onFailure;
        @event.Dispatch(onFailure, dispatcher);
    }

    /// <summary>
    /// Dispatches a confirmation event with the provided details.
    /// </summary>
    /// <param name="title">The confirmation title.</param>
    /// <param name="description">The confirmation description.</param>
    /// <param name="buttonConfirmationLabel">The label for the confirmation button.</param>
    /// <param name="buttonConfirmationCallback">The callback for the confirmation button.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnConfirmationEvent>() == 0) throw new LSNotificationException("no_confirmation_handler");
        OnConfirmationEvent @event = new OnConfirmationEvent(title, description, buttonConfirmationLabel, buttonConfirmationCallback);
        @event.FailureCallback += onFailure;
        return @event.Dispatch(onFailure, dispatcher);
    }

    /// <summary>
    /// Dispatches a confirmation event with a cancel option.
    /// </summary>
    /// <param name="title">The confirmation title.</param>
    /// <param name="description">The confirmation description.</param>
    /// <param name="buttonConfirmationLabel">The label for the confirmation button.</param>
    /// <param name="buttonConfirmationCallback">The callback for the confirmation button.</param>
    /// <param name="buttonCancelLabel">The label for the cancel button.</param>
    /// <param name="buttonCancelCallback">The callback for the cancel button.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool ConfirmationCancel(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnConfirmationEvent>() == 0) throw new LSNotificationException("no_confirmation_handler");
        OnConfirmationEvent @event = new OnConfirmationEvent(title, description, buttonConfirmationLabel, buttonConfirmationCallback, buttonCancelLabel, buttonCancelCallback);
        @event.FailureCallback += onFailure;
        return @event.Dispatch(onFailure, dispatcher);
    }

    #endregion
}
