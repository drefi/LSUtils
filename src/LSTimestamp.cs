namespace LSUtils;

using LSUtils.ProcessSystem;

// TODO: Tests
// TODO: Logging according to LSLogger standards
// TODO: LSProcess needs review
public class LSTimestamp : ILSProcessable {
    protected bool _isInitialized;
    protected LSProcessManager? _manager;
    public int TotalMinutes;
    public int Minute { get { return TotalMinutes % 60; } }
    public int Hour { get { return (TotalMinutes / 60) % 24; } }
    public int Day { get { return TotalMinutes / 60 / 24; } }

    public System.Guid ID { get; } = System.Guid.NewGuid();


    public LSTimestamp() {
        TotalMinutes = 0;
    }

    public LSTimestamp(int day, int hour, int minute) {
        TotalMinutes = 0;
        SetTimestamp(day, hour, minute, true);
    }

    public LSTimestamp(LSTimestamp copy) {
        TotalMinutes = copy.TotalMinutes;
    }

    public LSTimestamp(int totalMinutes) {
        TotalMinutes = totalMinutes;
    }

    public void SetTimestamp(int day, int hour, int minute, bool dontUpdate = false) {
        AddDays(day, true);
        AddHours(hour, true);
        AddMinutes(minute, true);
        //TotalMinutes = (day * 24 * 60) + (hour * 60) + minute;
        if (dontUpdate == false) Update();
    }
    public void Update() {
        var process = new UpdateProcess(this);
        process.Execute(_manager ?? LSProcessManager.Singleton, LSProcessManager.ProcessInstanceBehaviour.ALL, this);
    }
    public bool AddMinutes(int minutes, bool dontUpdate = false) {
        if (minutes <= 0)
            return false;
        TotalMinutes += minutes;
        if (dontUpdate == false) Update();
        return true;
    }
    public bool AddHours(int hours, bool dontUpdate = false) {
        if (hours <= 0)
            return false;
        TotalMinutes += (hours * 60);
        if (dontUpdate == false) Update();
        return true;
    }
    public bool AddDays(int days, bool dontUpdate = false) {
        if (days <= 0)
            return false;
        TotalMinutes += (days * 24 * 60);
        if (dontUpdate == false) Update();
        return true;
    }
    public int Diff(LSTimestamp timestamp) {
        return timestamp.TotalMinutes - TotalMinutes;
    }
    public bool InRange(LSTimestamp begin, LSTimestamp end) {
        if (begin.Diff(end) < 0 || Diff(begin) > 0 || Diff(end) < 0)
            return false;
        return true;
    }
    public override string ToString() {
        return "[Timestamp: " + TotalMinutes + " => Day = " + Day + " Hour = " + Hour + " Minute = " + Minute + "]";
    }

    public LSProcessResultStatus Initialize(LSProcessBuilderAction? onInitializeSequence = null, LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
        _manager = manager ?? LSProcessManager.Singleton;
        var process = new InitializeProcess(this);
        return process.WithProcessing(root => root
            .Handler(LSProcessLabels.IS_INITIALIZED_KEY, session => {
                if (_isInitialized) return LSProcessResultStatus.FAILURE;
                _isInitialized = true;
                return LSProcessResultStatus.SUCCESS;
            }, priority: LSProcessPriority.CRITICAL, readOnly: true)
            .Sequence(nameof(onInitializeSequence), onInitializeSequence,
                readOnly: true,
                priority: LSProcessPriority.HIGH,
                conditions: (proc, node) => onInitializeSequence != null)
        ).Execute(_manager, LSProcessManager.ProcessInstanceBehaviour.ALL, this);
    }

    public class InitializeProcess : LSProcess {
        public LSTimestamp Instance { get; }
        internal InitializeProcess(LSTimestamp instance) {
            Instance = instance;
        }
    }
    public class UpdateProcess : LSProcess {
        public LSTimestamp Instance { get; }
        public UpdateProcess(LSTimestamp instance) {
            Instance = instance;
        }
    }

}
