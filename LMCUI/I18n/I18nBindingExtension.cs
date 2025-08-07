using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System;

namespace LMCUI.I18n;

public class I18nBindingExtension : MarkupExtension
{
    public string Key { get; set; }
    public bool UseObservable { get; set; } = true;

    public I18nBindingExtension() { }

    public I18nBindingExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            throw new ArgumentNullException(nameof(Key));

        return I18nManager.Instance.GetString(Key);
    }
}