namespace LSUtils.Processing;
/// <summary>
/// Provides event-based notifications for printing, warnings, errors, confirmations, and general notifications.
/// </summary>
public static class LSSignals {
    /// <summary>
    /// Gets the class name.
    /// </summary>
    public static string ClassName => typeof(LSSignals).AssemblyQualifiedName ?? nameof(LSSignals);

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
    public class ConfirmationProcess : LSProcess {
        public ConfirmationSignal ConfirmationSignal { get; protected set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfirmationProcess"/> class with confirm and cancel buttons.
        /// </summary>
        internal ConfirmationProcess(ConfirmationSignal confirmationSignal) {
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
    public class NotifyProcess : LSProcess {
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
        /// Initializes a new instance of the <see cref="NotifyProcess"/> class.
        /// </summary>
        /// <param name="message">The notification message.</param>
        /// <param name="description">The notification description.</param>
        /// <param name="allowDismiss">Whether the notification can be dismissed.</param>
        /// <param name="notificationTimeout">Timeout in seconds.</param>
        internal NotifyProcess(NotificationSignal notificationSignal) {
            _notificationSignal = notificationSignal;
        }
    }
    #region Static Methods

    public static LSProcessResultStatus Notify(string message, string description = "", bool allowDismiss = false, double timeout = 3f, LSProcessManager? contextManager = null) {
        var process = new NotifyProcess(new NotificationSignal(message, description, allowDismiss, timeout));
        return process.Execute(null, contextManager);
    }

    public static LSProcessResultStatus Notify(NotificationSignal notificationSignal, LSProcessManager? contextManager = null) {
        var process = new NotifyProcess(notificationSignal);
        return process.Execute(null, contextManager);
    }

    public static LSProcessResultStatus Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSProcessManager? contextManager = null) {
        var process = new ConfirmationProcess(new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, false, null, null));
        return process.Execute(null, contextManager);
    }

    public static LSProcessResultStatus Confirmation(ConfirmationSignal confirmationSignal, LSProcessManager? contextManager = null) {
        var process = new ConfirmationProcess(confirmationSignal);
        return process.Execute(null, contextManager);
    }

    public static LSProcessResultStatus Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSProcessManager? contextManager = null) {
        var process = new ConfirmationProcess(new ConfirmationSignal(title, description, buttonConfirmationLabel, buttonConfirmationCallback, true, buttonCancelLabel, buttonCancelCallback));
        return process.Execute(null, contextManager);
    }

    #endregion
}
