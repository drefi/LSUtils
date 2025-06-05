using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LSUtils.LSLocale;

public class FormatterToken : ILSEventable {
    static FormatterToken? _instance;
    #region Instance
    public static FormatterToken Instance {
        get {
            if (_instance == null) _instance = new FormatterToken(DefaultPattern);
            return _instance;
        }
    }
    public static string DefaultPattern => @"\{([^}]+)\}";
    #endregion
    #region Fields
    protected readonly Dictionary<string, Dictionary<Languages, string>> _tokens = new Dictionary<string, Dictionary<Languages, string>>();
    protected Regex _regex;
    protected HashSet<string> _missingKeys = new HashSet<string>();
    public readonly string Pattern;
    public System.Guid ID { get; }
    public string ClassName => nameof(FormatterToken);
    public Languages CurrentLanguage { get; protected set; }
    #endregion
    /// <summary>
    /// Creates a new instance of the FormatterToken class.
    /// </summary>
    /// <param name="pattern">A regular expression pattern that matches the token name. The default pattern is "{([^}]+)}".</param>
    protected FormatterToken(string pattern) {
        ID = System.Guid.NewGuid();
        Pattern = pattern;
        _regex = new Regex(Pattern);
    }
    public bool Initialize(LSAction? onSuccess = null, LSMessageHandler? onFailure = null, LSDispatcher? dispatcher = null) {
        var @event = OnInitializeEvent.Create<FormatterToken>(this, onSuccess, onFailure);
        dispatcher ??= LSDispatcher.Instance;
        return dispatcher.Dispatch(@event);
    }

    /// <summary>
    /// Sets the language to be used for formatting tokens.
    /// </summary>
    /// <param name="locale">The language to use for formatting tokens.</param>
    /// <param name="tokens">A dictionary of tokens to be used for the specified language.</param>
    /// <param name="callback">An optional callback to be executed after the language has been set.</param>
    /// <remarks>
    /// If the language is Locale.NONE or Locale.ANY, this function will do nothing.
    /// </remarks>
    public bool SetLocale(Languages locale, Dictionary<string, string> tokens, LSAction? onSuccess = null, LSMessageHandler? onFailure = null) {
        if (locale == Languages.NONE || locale == Languages.VALUE) return false;
        var oldLanguage = CurrentLanguage;
        CurrentLanguage = locale;
        if (tokens != null && tokens.Count > 0) {
            foreach (var token in tokens) {
                AddToken(locale, token.Key, token.Value, false, onSuccess, onFailure);
            }
            OnSetLocaleEvent @event = new OnSetLocaleEvent(this, locale);
            @event.SuccessCallback += onSuccess;
            @event.FailureCallback += onFailure;
            @event.Dispatch();
        }
        return true;
    }
    /// <summary>
    /// Gets a list of tokens that are available for the specified language.
    /// </summary>
    /// <param name="locale">The language for which to get the tokens.</param>
    /// <param name="tokens">A dictionary to which the tokens will be added.</param>
    /// <returns>true if tokens were added to the dictionary, false if no tokens were added or if the language is Locale.NONE.</returns>
    /// <remarks>
    /// If the language is Locale.NONE, this function will do nothing and return false.
    /// If the tokens dictionary is null, this function will return false.
    /// If the tokens dictionary already contains a token that is available in the specified language, that token will be skipped.
    /// </remarks>
    public bool GetTokens(Languages locale, Dictionary<string, string> tokens) {
        if (locale == Languages.NONE) return false;
        if (tokens == null) return false;
        int totalTokens = tokens.Count;
        int startTokens = tokens.Count;
        foreach (var token_localeDic in _tokens) {
            if (token_localeDic.Value.ContainsKey(locale) == false) continue;
            if (string.IsNullOrEmpty(token_localeDic.Key) || tokens.ContainsKey(token_localeDic.Key)) continue;
            tokens.Add(token_localeDic.Key, token_localeDic.Value[locale]);
            totalTokens++;
        }
        return (totalTokens > startTokens);
    }
    public void AddToken(Languages locale, string token, string value, bool overwrite = true, LSAction? onSuccess = null, LSMessageHandler? onFailure = null) {
        if (_tokens.ContainsKey(token) == false) _tokens.Add(token, new Dictionary<Languages, string>());
        if (overwrite == false) return;
        _tokens[token][locale] = value;
        OnAddTokenEvent @event = new OnAddTokenEvent(this, locale, token, value);
        @event.SuccessCallback += onSuccess;
        @event.FailureCallback += onFailure;
        @event.Dispatch();
    }
    /// <summary>
    /// Parses the given text and replaces any formatter tokens with their associated value.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="parsedText">The parsed text with tokens replaced, or the original text if no tokens were found.</param>
    /// <returns>An array of tokens that were not found in the formatter's dictionary.</returns>
    /// <remarks>
    /// If a token is found but has no associated value for the current language, the token is treated as not found.
    /// </remarks>
    public string[] Parse(string text, out string parsedText) {
        string log = $"{ClassName}::Parse";
        parsedText = text;
        List<string> missingKeys = new List<string>();
        MatchCollection matches = _regex.Matches(parsedText);
        bool isProcessing = true;
        while (isProcessing) {
            isProcessing = replaceTokens(matches, parsedText, missingKeys, out parsedText);
            matches = _regex.Matches(parsedText);
        }
        foreach (string key in missingKeys) {
            if (_missingKeys.Add(key))
                LSSignals.Warning($"{log}::{key}::not_found");
        }
        return missingKeys.ToArray();
    }
    /// <summary>
    /// Replaces tokens in the given text with their associated value, if found, or adds them to the list of missing keys if not found.
    /// </summary>
    /// <param name="matches">A collection of matches from the regular expression pattern.</param>
    /// <param name="text">The text to parse and replace tokens in.</param>
    /// <param name="missingKeys">A list of tokens that were not found in the formatter's dictionary.</param>
    /// <param name="parsedText">The parsed text with tokens replaced, or the original text if no tokens were found.</param>
    /// <returns>true if any tokens were replaced, false if no tokens were replaced.</returns>
    protected bool replaceTokens(MatchCollection matches, string text, List<string> missingKeys, out string parsedText) {
        var filteredMatches = matches.Cast<Match>().Where(m => !missingKeys.Contains(m.Groups[1].Value));
        parsedText = text;
        foreach (Match match in filteredMatches) {
            var groups = match.Groups;
            var key = groups[1].Value;
            string value = key;
            if (_tokens.TryGetValue(key, out var token)) {
                value = token.TryGetValue(CurrentLanguage, out var langValue) ? langValue :
                token.TryGetValue(Languages.VALUE, out var anyValue) ? anyValue : key;
            }
            if (value == key && missingKeys.Contains(key) == false) {
                missingKeys.Add(key);
            } else if (value != key) {
                text = text.Replace(match.Value, value);
            }
        }
        return filteredMatches.Count() > 0;
    }

    public static FormatterToken Instantiate() => new FormatterToken(DefaultPattern);

    #region Events
    public class OnSetLocaleEvent : LSEvent<FormatterToken> {
        public Languages Language { get; }
        public OnSetLocaleEvent(FormatterToken instance, Languages language) : base(instance) {
            Language = language;
        }
    }
    public class OnAddTokenEvent : LSEvent<FormatterToken> {
        public Languages Locale { get; }
        public string Token { get; }
        public string Value { get; }

        public OnAddTokenEvent(FormatterToken instance, Languages locale, string token, string value) : base(instance) {
            Locale = locale;
            Token = token;
            Value = value;
        }
    }
    #endregion
}
[System.Flags]
public enum Languages {
    NONE,
    VALUE, //itens marked with VALUE are dynamically added and do not belong to any language
    EN_US,
    PT_BR,
}
