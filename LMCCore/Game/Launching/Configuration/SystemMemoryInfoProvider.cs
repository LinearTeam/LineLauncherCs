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

using System.Runtime.InteropServices;

namespace LMCCore.Game.Launching.Configuration;

public static class SystemMemoryInfoProvider
{
    public static SystemMemoryInfo GetCurrent()
    {
        if (OperatingSystem.IsWindows() && TryGetWindowsMemoryInfo(out var memoryInfo))
        {
            return memoryInfo;
        }

        var totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (totalBytes <= 0)
        {
            totalBytes = Environment.WorkingSet;
        }

        return new SystemMemoryInfo(totalBytes, Math.Max(0, totalBytes - Environment.WorkingSet));
    }

    private static bool TryGetWindowsMemoryInfo(out SystemMemoryInfo memoryInfo)
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status))
        {
            memoryInfo = default;
            return false;
        }

        memoryInfo = new SystemMemoryInfo(
            checked((long)status.TotalPhysicalMemory),
            checked((long)status.AvailablePhysicalMemory));
        return true;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtualMemory;
        public ulong AvailableVirtualMemory;
        public ulong AvailableExtendedVirtualMemory;
    }
}
