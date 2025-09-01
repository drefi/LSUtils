namespace LSUtils;

using LSUtils.EventSystem;

public class LSTimestamp {
    public class OnUpdateEvent : LSEvent<LSTimestamp> {
        public LSTimestamp Timestamp => Instance;
        public OnUpdateEvent(LSTimestamp timestamp) : base(timestamp) { }
    }
    protected LSDispatcher _dispatcher;
    public int TotalMinutes;
    public int Minute { get { return TotalMinutes % 60; } }
    public int Hour { get { return (TotalMinutes / 60) % 24; } }
    public int Day { get { return TotalMinutes / 60 / 24; } }

    public LSTimestamp(LSDispatcher? dispatcher = null) {
        TotalMinutes = 0;
        _dispatcher = dispatcher == null ? LSDispatcher.Singleton : dispatcher;
    }
    public LSTimestamp(int day, int hour, int minute, LSDispatcher? dispatcher = null) : this(dispatcher) {
        SetTimestamp(day, hour, minute, true);
    }
    public LSTimestamp(LSTimestamp copy, LSDispatcher? dispatcher = null) : this(dispatcher) {
        TotalMinutes = copy.TotalMinutes;
    }
    public LSTimestamp(int totalMinutes, LSDispatcher? dispatcher = null) : this(dispatcher) {
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
        _dispatcher.processEvent(new OnUpdateEvent(this));
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
