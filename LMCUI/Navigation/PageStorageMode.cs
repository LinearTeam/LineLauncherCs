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

namespace LMCUI.Navigation;

/// <summary>
/// 页面存储模式，决定页面实例如何被管理
/// </summary>
public enum PageStorageMode
{
    /// <summary>
    /// 每次导航都创建新实例
    /// </summary>
    Transient,

    /// <summary>
    /// 缓存单例，导航时复用同一实例
    /// </summary>
    Singleton,

    /// <summary>
    /// 按参数缓存实例，不同参数不同实例（需要实现 GetCacheKey）
    /// </summary>
    Parameterized
}
