namespace LSUtils;

public class Timestamp {

    public delegate void TimestampDelegate(Timestamp timestamp);
    public event TimestampDelegate? OnTimeUpdate;
    public int TotalMinutes;
    public int Minute { get { return TotalMinutes % 60; } }
    public int Hour { get { return (TotalMinutes / 60) % 24; } }
    public int Day { get { return TotalMinutes / 60 / 24; } }

    public Timestamp() {
        TotalMinutes = 0;
    }
    public Timestamp(int day, int hour, int minute) {
        SetTimestamp(day, hour, minute, true);
    }
    public Timestamp(Timestamp copy) {
        TotalMinutes = copy.TotalMinutes;
    }
    public Timestamp(int totalMinutes) {
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
        if (OnTimeUpdate != null) OnTimeUpdate(this);
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
    public int Diff(Timestamp timestamp) {
        return timestamp.TotalMinutes - TotalMinutes;
    }
    public bool InRange(Timestamp begin, Timestamp end) {
        if (begin.Diff(end) < 0 || Diff(begin) > 0 || Diff(end) < 0)
            return false;
        return true;
    }
    public override string ToString() {
        return "[Timestamp: " + TotalMinutes + " => Day = " + Day + " Hour = " + Hour + " Minute = " + Minute + "]";
    }
}
