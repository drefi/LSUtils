namespace LSUtils;

/// <summary>
/// Provides event-based notifications including printing, warnings, errors, confirmations, and general notifications.
/// </summary>
public static class LSSignals {
    #region Events

    /// <summary>
    /// Event triggered for print messages.
    /// </summary>
    public class OnPrintEvent : LSEvent {
        public string Message { get; protected set; }

        public OnPrintEvent(string message) : base(System.Guid.NewGuid()) => Message = message;

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
        public string Message { get; protected set; }

        public OnErrorEvent(string message) : base(System.Guid.NewGuid()) => Message = message ?? string.Empty;

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
        public string Message { get; protected set; }

        public OnWarningEvent(string message) : base(System.Guid.NewGuid()) => Message = message ?? string.Empty;

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
        public string Title { get; protected set; }
        public string Description { get; protected set; }
        public string ButtonConfirmLabel { get; protected set; }
        public LSAction? ButtonConfirmCallback { get; protected set; }
        public string ButtonCancelText { get; protected set; }
        public LSAction? ButtonCancelCallback { get; protected set; }
        public bool Cancellable { get; protected set; }

        public OnConfirmationEvent(string title, string description, string buttonConfirmLabel, LSAction buttonConfirmCallback) : base(System.Guid.NewGuid()) {
            Title = title;
            Description = description;
            ButtonConfirmLabel = buttonConfirmLabel;
            ButtonConfirmCallback = buttonConfirmCallback;
            ButtonCancelText = string.Empty;
            ButtonCancelCallback = null;
            Cancellable = false;
        }

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
        public string Message { get; protected set; }
        public string Description { get; protected set; }
        public bool AllowDismiss { get; protected set; }
        public double Timeout { get; protected set; }

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
    /// <param name="msg">The error message.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Error(string? msg, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnErrorEvent>() == 0) throw new LSNotificationException($"no_error_handler:{msg}");
        OnErrorEvent @event = new OnErrorEvent(msg!);
        return @event.Dispatch((msg) => throw new LSNotificationException($"no_error_handler:{msg}"), dispatcher);
    }

    /// <summary>
    /// Dispatches an error event with a custom error callback.
    /// </summary>
    /// <param name="errorCallback">The error callback to invoke.</param>
    /// <param name="message">The error message.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Error(LSMessageHandler? errorCallback, string? message, LSDispatcher? dispatcher = null) {
        if (errorCallback == null) return Error(message, dispatcher);
        return errorCallback(message);
    }

    /// <summary>
    /// Dispatches a warning event with the provided message.
    /// </summary>
    /// <param name="msg">The warning message.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Warning(string? msg, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnWarningEvent>() == 0) throw new LSNotificationException($"no_warning_handler:{msg}");
        OnWarningEvent @event = new OnWarningEvent(msg!);
        return @event.Dispatch((msg) => throw new LSNotificationException($"no_error_handler:{msg}"), dispatcher);
    }

    /// <summary>
    /// Dispatches a warning event with a custom warning callback.
    /// </summary>
    /// <param name="warningCallback">The warning callback to invoke.</param>
    /// <param name="message">The warning message.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Warning(LSMessageHandler? warningCallback, string? message, LSDispatcher? dispatcher = null) {
        if (warningCallback == null) return Warning(message!, dispatcher);
        return warningCallback(message);
    }

    /// <summary>
    /// Dispatches a print event with the provided message.
    /// </summary>
    /// <param name="msg">The print message.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Print(string msg, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnPrintEvent>() == 0) throw new LSNotificationException($"no_print_handler:{msg}");
        OnPrintEvent @event = new OnPrintEvent(msg);
        return @event.Dispatch((msg) => throw new LSNotificationException($"no_error_handler:{msg}"), dispatcher);
    }

    /// <summary>
    /// Dispatches a print event with a custom print callback.
    /// </summary>
    /// <param name="printCallback">The print callback to invoke.</param>
    /// <param name="msg">The print message.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Print(LSMessageHandler? printCallback, string msg, LSDispatcher? dispatcher = null) {
        if (printCallback == null) return Print(msg, dispatcher);
        printCallback(msg);
        return true;
    }

    /// <summary>
    /// Dispatches a notification event with the provided details.
    /// </summary>
    /// <param name="msg">The notification message.</param>
    /// <param name="description">The notification description.</param>
    /// <param name="callback">Optional callback to invoke after dispatch.</param>
    /// <param name="allowDismiss">Indicates if the notification can be dismissed.</param>
    /// <param name="timeout">Timeout for the notification.</param>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static void Notify(string msg, string description = "", bool allowDismiss = false, double timeout = 3f, LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnNotifyEvent>() == 0) throw new LSNotificationException(msg);
        OnNotifyEvent @event = new OnNotifyEvent(msg, description, allowDismiss, timeout);
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
    public static bool Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSDispatcher? dispatcher = null) {
        if (LSEvent.GetListenersCount<OnConfirmationEvent>() == 0) {
            return Print($"LSNotification::Confirmation::{title} -- {description} -- {buttonConfirmationLabel}", dispatcher);
        }
        OnConfirmationEvent @event = new OnConfirmationEvent(title, description, buttonConfirmationLabel, buttonConfirmationCallback);
        return @event.Dispatch((msg) => throw new LSNotificationException($"no_error_handler:{msg}"), dispatcher);
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
    public static bool ConfirmationCancel(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSDispatcher? dispatcher = null) {
        if (OnConfirmationEvent.GetListenersCount<OnConfirmationEvent>() == 0) {
            return Print($"LSNotification::ConfirmationCancel::{title} -- {description} -- {buttonConfirmationLabel} -- {buttonCancelLabel}");
        }
        OnConfirmationEvent @event = new OnConfirmationEvent(title, description, buttonConfirmationLabel, buttonConfirmationCallback, buttonCancelLabel, buttonCancelCallback);
        return @event.Dispatch((msg) => throw new LSNotificationException($"no_error_handler:{msg}"), dispatcher);
    }

    #endregion
}
