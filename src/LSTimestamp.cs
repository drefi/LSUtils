namespace LSUtils;

using LSUtils.EventSystem;

public class LSTimestamp : ILSEventable_obsolete {
    public class OnUpdateEvent : LSEvent<LSTimestamp> {
        public OnUpdateEvent(LSTimestamp timestamp, LSEventOptions? options = null) : base(timestamp, options) { }
    }
    
    public LSDispatcher? Dispatcher { get; protected set; }
    public int TotalMinutes;
    public int Minute { get { return TotalMinutes % 60; } }
    public int Hour { get { return (TotalMinutes / 60) % 24; } }
    public int Day { get { return TotalMinutes / 60 / 24; } }

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
    
    public EventProcessResult Initialize(LSEventOptions options) {
        Dispatcher = options.Dispatcher;
        //TODO: proper event
        return EventProcessResult.SUCCESS;
    }
    public void SetTimestamp(int day, int hour, int minute, bool dontUpdate = false) {
        AddDays(day, true);
        AddHours(hour, true);
        AddMinutes(minute, true);
        //TotalMinutes = (day * 24 * 60) + (hour * 60) + minute;
        if (dontUpdate == false) Update();
    }
    public void Update() {
        if (Dispatcher != null) {
            var options = new LSEventOptions(Dispatcher, this);
            var updateEvent = new OnUpdateEvent(this, options);
            updateEvent.Dispatch();
        }
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
}
