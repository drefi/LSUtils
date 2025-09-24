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
    public class OnConfirmationEvent : LSEvent {
        public ConfirmationSignal ConfirmationSignal { get; protected set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="OnConfirmationEvent"/> class with confirm and cancel buttons.
        /// </summary>
        internal OnConfirmationEvent(ConfirmationSignal confirmationSignal) {
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
    public class OnNotifyEvent : LSEvent {
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
        internal OnNotifyEvent(NotificationSignal notificationSignal) {
            _notificationSignal = notificationSignal;
        }
    }

    #endregion

    #region Static Methods

    public static LSEventProcessStatus Notify(string message, string description = "", bool allowDismiss = false, double timeout = 3f, LSEventContextManager? contextManager = null) {
        var @event = new OnNotifyEvent(new NotificationSignal(message, description, allowDismiss, timeout));
        return @event.Process(null, contextManager);
    }

    public static LSEventProcessStatus Notify(NotificationSignal notificationSignal, LSEventContextManager? contextManager = null) {
        var @event = new OnNotifyEvent(notificationSignal);
        return @event.Process(null, contextManager);
    }

    public static LSEventProcessStatus Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSEventContextManager? contextManager = null) {
        var @event = new OnConfirmationEvent(new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, false, null, null));
        return @event.Process(null, contextManager);
    }

    public static LSEventProcessStatus Confirmation(ConfirmationSignal confirmationSignal, LSEventContextManager? contextManager = null) {
        var @event = new OnConfirmationEvent(confirmationSignal);
        return @event.Process(null, contextManager);
    }

    public static LSEventProcessStatus Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSEventContextManager? contextManager = null) {
        var @event = new OnConfirmationEvent(new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, true, buttonCancelLabel, buttonCancelCallback));
        return @event.Process(null, contextManager);
    }

    #endregion
}
