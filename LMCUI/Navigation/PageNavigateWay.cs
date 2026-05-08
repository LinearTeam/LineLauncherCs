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

using System;
using FluentAvalonia.UI.Controls;

namespace LMCUI.Navigation;

public class PageNavigateWay
{
    public Type PageType { get; set; }
    public object? Param { get; set; }
    public FANavigationViewItem Item { get; set; }
    public bool DirectlySet { get; set; }

    public PageNavigateWay(Type pageType, object? param, FANavigationViewItem item, bool directlySet = false)
    {
        PageType = pageType;
        Param = param;
        Item = item;
        DirectlySet = directlySet;
    }

    public PageNavigateWay(Type pageType, FANavigationViewItem item) : this(pageType, null, item)
    {
    }
}
