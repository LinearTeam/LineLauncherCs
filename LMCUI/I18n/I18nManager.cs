using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LMCUI.I18n;

using LMC;

public sealed class I18nManager
{
    private static readonly Lazy<I18nManager> s_instance = 
        new Lazy<I18nManager>(() => new I18nManager());
        
    public static I18nManager Instance => s_instance.Value;

    private ConcurrentDictionary<string, string> _strings = 
        new ConcurrentDictionary<string, string>();
        
    private ConcurrentDictionary<string, Dictionary<string, string>> _allLanguages =
        new ConcurrentDictionary<string, Dictionary<string, string>>();
        
    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("zh-CN");
        
    public IReadOnlyList<CultureInfo> AvailableCultures { get; private set; } = 
        Array.Empty<CultureInfo>();

    public event Action? CultureChanged;

    private I18nManager()
    {
        // LoadAllLanguages();
        LoadCulture(_currentCulture);
    }

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture.Name != value.Name && _allLanguages.ContainsKey(value.Name))
            {
                _currentCulture = value;
                LoadCulture(value);
                CultureChanged?.Invoke();
                    
                Current.Config.SelectedLanguage = value.Name;
            }
        }
    }

    public void LoadAllLanguages()
    {
        var languagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            
        if (!Directory.Exists(languagesDir))
        {
            Directory.CreateDirectory(languagesDir);
            return;
        }

        var xmlFiles = Directory.GetFiles(languagesDir, "*.xml");
        var languages = new List<CultureInfo>();
        var allDict = new ConcurrentDictionary<string, Dictionary<string, string>>();

        foreach (var file in xmlFiles)
        {
            try
            {
                var cultureName = Path.GetFileNameWithoutExtension(file);
                var culture = CultureInfo.GetCultureInfo(cultureName);
                languages.Add(culture);
                    
                var dict = XmlLanguageLoader.LoadLanguageFile(file);
                allDict[cultureName] = dict;
            }
            catch
            {
                // ignored
            }
        }

        _allLanguages = allDict;
        AvailableCultures = languages.OrderBy(c => c.NativeName).ToList();
            
        var savedLang = Current.Config.SelectedLanguage;
        if (_allLanguages.ContainsKey(savedLang))
        {
            _currentCulture = CultureInfo.GetCultureInfo(savedLang);
            LoadCulture(_currentCulture);
        }
    }

    private void LoadCulture(CultureInfo cultureInfo)
    {
        var cultureName = cultureInfo.Name;
        if (_allLanguages.TryGetValue(cultureName, out var dict))
        {
            _strings = new ConcurrentDictionary<string, string>(dict);
        }
    }

    public string GetString(string key)
    {
        return _strings.TryGetValue(key, out var value) 
            ? value 
            : $"{key}";
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        return args.Length > 0 
            ? string.Format(format, args) 
            : format;
    }

    public IObservable<string> GetBinding(string key)
    {
        return new I18nBinding(key);
    }

    public IReadOnlyDictionary<string, string> GetAllStringsForCurrentCulture()
    {
        return _strings;
    }

    private sealed class I18nBinding : IObservable<string>
    {
        private readonly string _key;
        private readonly List<IObserver<string>> _observers = new();
            
        public I18nBinding(string key)
        {
            _key = key;
            I18nManager.Instance.CultureChanged += OnCultureChanged;
        }

        private void OnCultureChanged()
        {
            var value = I18nManager.Instance.GetString(_key);
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            _observers.Add(observer);
            observer.OnNext(I18nManager.Instance.GetString(_key));
                
            return new Unsubscriber(() =>
            {
                _observers.Remove(observer);
                I18nManager.Instance.CultureChanged -= OnCultureChanged;
            });
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private Action? _unsubscribeAction;
        public Unsubscriber(Action unsubscribeAction) => _unsubscribeAction = unsubscribeAction;
        public void Dispose() => _unsubscribeAction?.Invoke();
    }
}