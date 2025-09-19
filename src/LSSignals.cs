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
    public class OnPrintEvent : LSEvent_obsolete {
        /// <summary>
        /// The print message.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnPrintEvent"/> class.
        /// </summary>
        /// <param name="message">The message to print.</param>
        internal OnPrintEvent(LSEventOptions options, string message) : base(options) {
            Message = message;
        }
    }

    /// <summary>
    /// Event triggered for error messages.
    /// </summary>
    public class OnErrorEvent : LSEvent_obsolete {
        /// <summary>
        /// The error message.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnErrorEvent"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        internal OnErrorEvent(LSEventOptions options, string message) : base(options) {
            Message = message;
        }
    }

    /// <summary>
    /// Event triggered for warning messages.
    /// </summary>
    public class OnWarningEvent : LSEvent_obsolete {
        /// <summary>
        /// The warning message.
        /// </summary>
        public string Message { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnWarningEvent"/> class.
        /// </summary>
        /// <param name="message">The warning message.</param>
        internal OnWarningEvent(LSEventOptions options, string message) : base(options) {
            Message = message;
        }
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
    public class OnConfirmationEvent : LSEvent_obsolete {
        public ConfirmationSignal ConfirmationSignal { get; protected set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="OnConfirmationEvent"/> class with confirm and cancel buttons.
        /// </summary>
        internal OnConfirmationEvent(LSEventOptions options, ConfirmationSignal confirmationSignal) : base(options) {
            ConfirmationSignal = confirmationSignal;
        }
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
    public class OnNotifyEvent : LSEvent_obsolete {
        protected NotificationSignal _notificationSignal;
        /// <summary>
        /// The notification message.
        /// </summary>
        public string Message => _notificationSignal.Message;
        /// <summary>
        /// The notification description.
        /// </summary>
        public string Description => _notificationSignal.Description;
        /// <summary>
        /// Indicates if the notification can be dismissed.
        /// </summary>
        public bool AllowDismiss => _notificationSignal.AllowDismiss;
        /// <summary>
        /// The timeout for the notification (in seconds).
        /// </summary>
        public double NotificationTimeout => _notificationSignal.NotificationTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnNotifyEvent"/> class.
        /// </summary>
        /// <param name="message">The notification message.</param>
        /// <param name="description">The notification description.</param>
        /// <param name="allowDismiss">Whether the notification can be dismissed.</param>
        /// <param name="notificationTimeout">Timeout in seconds.</param>
        internal OnNotifyEvent(LSEventOptions options, NotificationSignal notificationSignal) : base(options) {
            _notificationSignal = notificationSignal;
        }
    }

    #endregion

    #region Static Methods

    public static void Error(string message, LSEventOptions options) {
        var @event = new OnErrorEvent(options, message);
        @event.Dispatch();
    }

    public static void Error(string message, LSDispatcher? dispatcher = null) {
        var @event = new OnErrorEvent(new LSEventOptions(dispatcher), message);
        @event.Dispatch();
    }

    public static void Warning(string message, LSEventOptions options) {
        var @event = new OnWarningEvent(options, message);
        @event.Dispatch();
    }

    public static void Warning(string message, LSDispatcher? dispatcher = null) {
        var @event = new OnWarningEvent(new LSEventOptions(dispatcher), message);
        @event.Dispatch();
    }

    public static void Print(string message, LSEventOptions options) {
        var @event = new OnPrintEvent(options, message);
        @event.Dispatch();
    }

    public static void Print(string message, LSDispatcher? dispatcher = null) {
        var @event = new OnPrintEvent(new LSEventOptions(dispatcher), message);
        @event.Dispatch();
    }

    public static void Notify(string message, string description, bool allowDismiss, double timeout, LSEventOptions options) {
        var @event = new OnNotifyEvent(options, new NotificationSignal(message, description, allowDismiss, timeout));
        @event.Dispatch();
    }

    public static void Notify(string message, string description = "", bool allowDismiss = false, double timeout = 3f, LSDispatcher? dispatcher = null) {
        var @event = new OnNotifyEvent(new LSEventOptions(dispatcher), new NotificationSignal(message, description, allowDismiss, timeout));
        @event.Dispatch();
    }

    public static void Notify(NotificationSignal notificationSignal, LSEventOptions options) {
        var @event = new OnNotifyEvent(options, notificationSignal);
        @event.Dispatch();
    }

    public static void Notify(NotificationSignal notificationSignal, LSDispatcher? dispatcher = null) {
        var @event = new OnNotifyEvent(new LSEventOptions(dispatcher), notificationSignal);
        @event.Dispatch();
    }

    public static void Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSEventOptions options) {
        var @event = new OnConfirmationEvent(options, new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, false, null, null));
        @event.Dispatch();
    }

    public static void Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSDispatcher? dispatcher = null) {
        var @event = new OnConfirmationEvent(new LSEventOptions(dispatcher), new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, false, null, null));
        @event.Dispatch();
    }

    public static void Confirmation(ConfirmationSignal confirmationSignal, LSEventOptions options) {
        var @event = new OnConfirmationEvent(options, confirmationSignal);
        @event.Dispatch();
    }

    public static void Confirmation(ConfirmationSignal confirmationSignal, LSDispatcher? dispatcher = null) {
        var @event = new OnConfirmationEvent(new LSEventOptions(dispatcher), confirmationSignal);
        @event.Dispatch();
    }

    public static void Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSEventOptions options) {
        var @event = new OnConfirmationEvent(options, new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, true, buttonCancelLabel, buttonCancelCallback));
        @event.Dispatch();
    }

    public static void Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSDispatcher? dispatcher = null) {
        var @event = new OnConfirmationEvent(new LSEventOptions(dispatcher), new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, true, buttonCancelLabel, buttonCancelCallback));
        @event.Dispatch();
    }

    #endregion
}
