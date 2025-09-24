namespace LSUtils;

using LSUtils.EventSystem;

public class LSTimestamp : ILSEventable {

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

    public void SetTimestamp(int day, int hour, int minute, bool dontUpdate = false, LSEventContextManager? contextManager = null) {
        AddDays(day, true);
        AddHours(hour, true);
        AddMinutes(minute, true);
        //TotalMinutes = (day * 24 * 60) + (hour * 60) + minute;
        if (dontUpdate == false) Update(contextManager);
    }
    public void Update(LSEventContextManager? contextManager = null) {
        var @event = new OnUpdateEvent(this);
        @event.Process(this, contextManager);
    }
    public bool AddMinutes(int minutes, bool dontUpdate = false) {
        if (minutes <= 0)
            return false;
        TotalMinutes += minutes;
        if (dontUpdate == false) Update();
        return true;
    }
    public bool AddHours(int hours, bool dontUpdate = false, LSEventContextManager? contextManager = null) {
        if (hours <= 0)
            return false;
        TotalMinutes += (hours * 60);
        if (dontUpdate == false) Update(contextManager);
        return true;
    }
    public bool AddDays(int days, bool dontUpdate = false, LSEventContextManager? contextManager = null) {
        if (days <= 0)
            return false;
        TotalMinutes += (days * 24 * 60);
        if (dontUpdate == false) Update(contextManager);
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

    public LSEventProcessStatus Initialize(LSEventContextDelegate? ctxBuilder = null, LSEventContextManager? manager = null) {
        var @event = new OnInitializeEvent(this);
        if (ctxBuilder != null) @event.Context(ctxBuilder, this);
        return @event.Process(this, manager);
    }

    public class OnInitializeEvent : LSEvent {
        public LSTimestamp Instance { get; }
        internal OnInitializeEvent(LSTimestamp instance) {
            Instance = instance;
        }
    }
    public class OnUpdateEvent : LSEvent {
        public LSTimestamp Instance { get; }
        public OnUpdateEvent(LSTimestamp instance) {
            Instance = instance;
        }
    }

}
