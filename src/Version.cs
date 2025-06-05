namespace LSUtils;
public class Version {
    public Version() {
        majorVersion = 0;
        minorVersion = 0;
        dateVersion = 0;
        buildVersion = 0;
        buildVersionType = BuildVersionType.UNKNOWN;
    }
    protected Version(Version other) {
        majorVersion = other.majorVersion;
        minorVersion = other.minorVersion;
        dateVersion = other.dateVersion;
        buildVersion = other.buildVersion;
        buildVersionType = other.buildVersionType;
    }
    public Version Clone() {
        return new Version(this);
    }

    public int majorVersion;
    public int minorVersion;
    public int dateVersion;
    public int buildVersion;
    public BuildVersionType buildVersionType;

    public event LSAction? OnChange;
    public void IncreaseMajor() {
        majorVersion++;
        minorVersion = 1;
        if (UpdateDate() == false)
            buildVersion = 1;
        if (OnChange != null) OnChange();
    }
    public void IncreaseMinor() {
        minorVersion++;
        if (UpdateDate() == false)
            buildVersion = 1;
        if (OnChange != null) OnChange();
    }
    public bool UpdateDate() {
        int dateInt = int.Parse(string.Format("{0:yyyyMMdd}", System.DateTime.Now));
        if (dateInt == dateVersion)
            return false;
        dateVersion = dateInt;
        buildVersion = 1;
        if (OnChange != null) OnChange();
        return true;
    }
    public void IncreaseBuild() {
        buildVersion++;
        if (OnChange != null) OnChange();
    }
    public bool Compare(Version other) {
        if (majorVersion != other.majorVersion || minorVersion != other.minorVersion || buildVersion != other.buildVersion || dateVersion != other.dateVersion)
            return false;
        return true;
    }
    public bool Compatible(Version other) {
        if (other.majorVersion != majorVersion)
            return false;
        if (other.minorVersion != minorVersion)
            return false;
        return true;
    }
    public bool Older(Version other) {
        if (majorVersion > other.majorVersion)
            return false;
        if (minorVersion >= other.minorVersion)
            return false;
        return true;
    }
    public bool Newer(Version other) {
        if (majorVersion < other.majorVersion)
            return false;
        if (minorVersion <= other.minorVersion)
            return false;
        return true;
    }
    public override string ToString() {
        return majorVersion + "." + minorVersion + "-" + dateVersion + buildVersionType.ToString()[0] + buildVersion;
    }
    public static bool TryParse(string versionString, out Version version) {
        version = new Version();
        if (string.IsNullOrEmpty(versionString) == true)
            return false;
        string[] majorSplit = versionString.Split('.');
        if (int.TryParse(majorSplit[0], out version.majorVersion) == false) {
            //Debug.LogError("problema na string majorSplit[0] = " + majorSplit[0]);
            return false;
        }
        string[] minorSplit = majorSplit[1].Split('-');
        if (int.TryParse(minorSplit[0], out version.minorVersion) == false) {
            //Debug.LogError("problema na string minorSplit[0] = " + minorSplit[0]);
            return false;
        }

        string dateVersionPart = minorSplit[1].Substring(0, 8);
        if (int.TryParse(dateVersionPart, out version.dateVersion) == false) {
            //Debug.LogError("problema na string dateVersionPart = " + dateVersionPart);
            return false;
        }
        int lenghtLeft = minorSplit[1].Length - 8;
        string buildVersionPart = minorSplit[1].Substring(8, lenghtLeft);

        if (buildVersionPart.StartsWith("D") == true) {
            version.buildVersionType = BuildVersionType.DEMO;
        } else if (buildVersionPart.StartsWith("A") == true) {
            version.buildVersionType = BuildVersionType.ALPHA;
        } else if (buildVersionPart.StartsWith("B") == true) {
            version.buildVersionType = BuildVersionType.BETA;
        } else if (buildVersionPart.StartsWith("R") == true) {
            version.buildVersionType = BuildVersionType.RELEASE;
        } else {
            version.buildVersionType = BuildVersionType.UNKNOWN;
        }
        string buildVersionString = buildVersionPart.Substring(1);
        if (int.TryParse(buildVersionString, out version.buildVersion) == false) {
            //Debug.LogError("problema na string buildVersionPart = " + buildVersionPart);
            return false;
        }
        return true;
    }
    public static Version Zero {
        get {
            return new Version();
        }
    }
}
public class VersionException : LSException {
    public VersionException() {
    }
    public VersionException(string message) : base(message) {
    }
}

public enum BuildVersionType {
    UNKNOWN,
    DEMO,
    ALPHA,
    BETA,
    RELEASE
}
