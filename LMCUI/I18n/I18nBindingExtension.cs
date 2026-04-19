// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using Avalonia.Markup.Xaml;
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