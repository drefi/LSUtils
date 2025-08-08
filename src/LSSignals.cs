using LSUtils.LSLocale;

namespace LSUtils.EventSystem;

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
    public class OnPrintEvent : LSEvent<string> {
        /// <summary>
        /// The print message.
        /// </summary>
        public string Message => Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnPrintEvent"/> class.
        /// </summary>
        /// <param name="message">The message to print.</param>
        public OnPrintEvent(string message) : base(message) { }
    }

    /// <summary>
    /// Event triggered for error messages.
    /// </summary>
    public class OnErrorEvent : LSEvent<string> {
        /// <summary>
        /// The error message.
        /// </summary>
        public string Message => Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnErrorEvent"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public OnErrorEvent(string message) : base(message) { }
    }

    /// <summary>
    /// Event triggered for warning messages.
    /// </summary>
    public class OnWarningEvent : LSEvent<string> {
        /// <summary>
        /// The warning message.
        /// </summary>
        public string Message => Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnWarningEvent"/> class.
        /// </summary>
        /// <param name="message">The warning message.</param>
        public OnWarningEvent(string message) : base(message) { }
    }

    public class ConfirmationSignal {
        /// <summary>
        /// The confirmation dialog title.
        /// </summary>
        public string? Title { get; protected set; }
        /// <summary>
        /// The confirmation dialog description.
        /// </summary>
        public string? Description { get; protected set; }
        /// <summary>
        /// The label for the confirm button.
        /// </summary>
        public string? ButtonConfirmLabel { get; protected set; }
        /// <summary>
        /// The callback for the confirm button.
        /// </summary>
        public LSAction? ButtonConfirmCallback { get; protected set; }
        /// <summary>
        /// The label for the cancel button.
        /// </summary>
        public string? ButtonCancelText { get; protected set; }
        /// <summary>
        /// The callback for the cancel button.
        /// </summary>
        public LSAction? ButtonCancelCallback { get; protected set; }
        /// <summary>
        /// Indicates if the dialog is cancellable.
        /// </summary>
        public bool Cancellable { get; protected set; }

        public ConfirmationSignal(string? title, string? description, string? buttonConfirmLabel, LSAction? buttonConfirmCallback, bool cancellable, string? buttonCancelLabel, LSAction? buttonCancelCallback) {
            Title = title;
            Description = description;
            ButtonConfirmLabel = buttonConfirmLabel;
            ButtonConfirmCallback = buttonConfirmCallback;
            Cancellable = cancellable;
            ButtonCancelText = buttonCancelLabel;
            ButtonCancelCallback = buttonCancelCallback;
        }

    }
    /// <summary>
    /// Event triggered for confirmation messages.
    /// </summary>
    public class OnConfirmationEvent : LSEvent<ConfirmationSignal> {
        public ConfirmationSignal ConfirmationSignal => Instance;
        /// <summary>
        /// Initializes a new instance of the <see cref="OnConfirmationEvent"/> class with confirm and cancel buttons.
        /// </summary>
        public OnConfirmationEvent(ConfirmationSignal confirmationSignal) : base(confirmationSignal) { }
    }


    public class NotificationSignal {
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
        public double NotificationTimeout { get; protected set; }

        public NotificationSignal(string message, string description, bool allowDismiss, double notificationTimeout) {
            Message = message;
            Description = description;
            AllowDismiss = allowDismiss;
            NotificationTimeout = notificationTimeout;
        }
    }
    /// <summary>
    /// Event triggered for general notifications.
    /// </summary>
    public class OnNotifyEvent : LSEvent<NotificationSignal> {

        /// <summary>
        /// The notification message.
        /// </summary>
        public string Message => Instance.Message;
        /// <summary>
        /// The notification description.
        /// </summary>
        public string Description => Instance.Description;
        /// <summary>
        /// Indicates if the notification can be dismissed.
        /// </summary>
        public bool AllowDismiss => Instance.AllowDismiss;
        /// <summary>
        /// The timeout for the notification (in seconds).
        /// </summary>
        public double NotificationTimeout => Instance.NotificationTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnNotifyEvent"/> class.
        /// </summary>
        /// <param name="message">The notification message.</param>
        /// <param name="description">The notification description.</param>
        /// <param name="allowDismiss">Whether the notification can be dismissed.</param>
        /// <param name="notificationTimeout">Timeout in seconds.</param>
        public OnNotifyEvent(NotificationSignal notificationSignal) : base(notificationSignal) { }
    }

    #endregion

    #region Static Methods

    public static bool Error(string message, LSDispatcher? dispatcher) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnErrorEvent(message);
        return dispatcher.ProcessEvent(@event);
    }

    public static bool Warning(string message, LSDispatcher? dispatcher) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnWarningEvent(message);
        return dispatcher.ProcessEvent(@event);
    }

    public static bool Print(string message, LSDispatcher? dispatcher) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnPrintEvent(message);
        return dispatcher.ProcessEvent(@event);
    }

    public static bool Notify(string message, string description = "", bool allowDismiss = false, double timeout = 3f, LSDispatcher? dispatcher = null) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnNotifyEvent(new NotificationSignal(message, description, allowDismiss, timeout));
        return dispatcher.ProcessEvent(@event);
    }
    public static bool Notify(NotificationSignal notificationSignal, LSDispatcher? dispatcher = null) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnNotifyEvent(notificationSignal);
        return dispatcher.ProcessEvent(@event);
    }

    public static bool Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSDispatcher? dispatcher = null) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnConfirmationEvent(new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, false, null, null));
        return dispatcher.ProcessEvent(@event);
    }
    public static bool Confirmation(ConfirmationSignal confirmationSignal, LSDispatcher? dispatcher = null) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnConfirmationEvent(confirmationSignal);
        return dispatcher.ProcessEvent(@event);
    }
    public static bool Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSDispatcher? dispatcher = null) {
        dispatcher ??= LSDispatcher.Singleton;
        var @event = new OnConfirmationEvent(new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, true, buttonCancelLabel, buttonCancelCallback));
        return dispatcher.ProcessEvent(@event);
    }

    #endregion
}
