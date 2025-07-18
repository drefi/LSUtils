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
        protected OnPrintEvent(string message, LSEventOptions eventOptions) : base(eventOptions) => Message = message;
        public static OnPrintEvent Create(string message, LSEventOptions? eventOptions = null) {
            eventOptions ??= LSEventOptions.Create();
            return new OnPrintEvent(message, eventOptions);
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
        protected OnErrorEvent(string message, LSEventOptions eventOptions) : base(eventOptions) => Message = message ?? string.Empty;

        public static OnErrorEvent Create(string message, LSEventOptions? eventOptions = null) {
            eventOptions ??= LSEventOptions.Create();
            return new OnErrorEvent(message, eventOptions);
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
        protected OnWarningEvent(string message, LSEventOptions eventOptions) : base(eventOptions) => Message = message ?? string.Empty;

        public static OnWarningEvent Create(string message, LSEventOptions? eventOptions = null) {
            eventOptions ??= LSEventOptions.Create();
            return new OnWarningEvent(message, eventOptions);
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
        /// Initializes a new instance of the <see cref="OnConfirmationEvent"/> class with confirm and cancel buttons.
        /// </summary>
        protected OnConfirmationEvent(string title, string description, string buttonConfirmLabel, LSAction buttonConfirmCallback, bool cancellable, string buttonCancelLabel, LSAction? buttonCancelCallback, LSEventOptions eventOptions) : base(eventOptions) {
            Title = title;
            Description = description;
            ButtonConfirmLabel = buttonConfirmLabel;
            ButtonConfirmCallback = buttonConfirmCallback;
            Cancellable = cancellable;
            ButtonCancelText = buttonCancelLabel;
            ButtonCancelCallback = buttonCancelCallback;
        }
        public static OnConfirmationEvent Create(string title, string description, string buttonConfirmLabel, LSAction buttonConfirmCallback, LSEventOptions? eventOptions = null) {
            eventOptions ??= LSEventOptions.Create();
            return new OnConfirmationEvent(title, description, buttonConfirmLabel, buttonConfirmCallback, false, string.Empty, null, eventOptions);
        }
        public static OnConfirmationEvent Create(string title, string description, string buttonConfirmLabel, LSAction buttonConfirmCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSEventOptions? eventOptions = null) {
            eventOptions ??= LSEventOptions.Create();
            return new OnConfirmationEvent(title, description, buttonConfirmLabel, buttonConfirmCallback, true, buttonCancelLabel, buttonCancelCallback, eventOptions);
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
        public double NotificationTimeout { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OnNotifyEvent"/> class.
        /// </summary>
        /// <param name="message">The notification message.</param>
        /// <param name="description">The notification description.</param>
        /// <param name="allowDismiss">Whether the notification can be dismissed.</param>
        /// <param name="notificationTimeout">Timeout in seconds.</param>
        protected OnNotifyEvent(string message, string description, bool allowDismiss, double notificationTimeout, LSEventOptions eventOptions) : base(eventOptions) {
            Description = description;
            Message = message;
            AllowDismiss = allowDismiss;
            NotificationTimeout = notificationTimeout;
        }
        public static OnNotifyEvent Create(string message, string description = "", bool allowDismiss = true, double timeout = 3f, LSEventOptions? eventOptions = null) {
            eventOptions ??= LSEventOptions.Create();
            return new OnNotifyEvent(message, description, allowDismiss, timeout, eventOptions);
        }
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Dispatches an error event with the provided message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorHandler">Error Handler.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Error(string message, LSEventOptions? eventOptions = null) {
        OnErrorEvent @event = OnErrorEvent.Create(message, eventOptions);
        return @event.Dispatch();
    }

    /// <summary>
    /// Dispatches a warning event with the provided message.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="onFailure">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Warning(string message, LSEventOptions? eventOptions = null) {
        OnWarningEvent @event = OnWarningEvent.Create(message, eventOptions);
        return @event.Dispatch();
    }

    /// <summary>
    /// Dispatches a print event with the provided message.
    /// </summary>
    /// <param name="message">The print message.</param>
    /// <param name="errorHandler">Error handler.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Print(string message, LSEventOptions? eventOptions = null) {
        OnPrintEvent @event = OnPrintEvent.Create(message, eventOptions);
        return @event.Dispatch();
    }

    /// <summary>
    /// Dispatches a notification event with the provided details.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="description">The notification description.</param>
    /// <param name="allowDismiss">Indicates if the notification can be dismissed.</param>
    /// <param name="timeout">Timeout for the notification (in seconds).</param>
    /// <param name="onSuccess">Callback if dispatch succeeds.</param>
    /// <param name="errorHandler">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <exception cref="LSNotificationException">Thrown if no listeners are registered.</exception>
    public static bool Notify(string message, string description = "", bool allowDismiss = false, double timeout = 3f, LSEventOptions? eventOptions = null) {
        OnNotifyEvent @event = OnNotifyEvent.Create(message, description, allowDismiss, timeout, eventOptions);
        return @event.Dispatch();
    }

    /// <summary>
    /// Dispatches a confirmation event with the provided details.
    /// </summary>
    /// <param name="title">The confirmation title.</param>
    /// <param name="description">The confirmation description.</param>
    /// <param name="buttonConfirmationLabel">The label for the confirmation button.</param>
    /// <param name="buttonConfirmationCallback">The callback for the confirmation button.</param>
    /// <param name="errorHandler">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool Confirmation(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, LSEventOptions? eventOptions = null) {
        OnConfirmationEvent @event = OnConfirmationEvent.Create(title, description, buttonConfirmationLabel, buttonConfirmationCallback, eventOptions);
        return @event.Dispatch();
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
    /// <param name="errorHandler">Callback if dispatch fails.</param>
    /// <param name="dispatcher">Optional dispatcher instance.</param>
    /// <returns>True if dispatched successfully.</returns>
    public static bool ConfirmationCancel(string title, string description, string buttonConfirmationLabel, LSAction buttonConfirmationCallback, string buttonCancelLabel, LSAction buttonCancelCallback, LSEventOptions? eventOptions = null) {
        OnConfirmationEvent @event = OnConfirmationEvent.Create(title, description, buttonConfirmationLabel, buttonConfirmationCallback, buttonCancelLabel, buttonCancelCallback, eventOptions);
        return @event.Dispatch();
    }

    #endregion
}
