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

using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LMCUI.Pages.SettingsPage.About;

internal static class ProjectLegalDocuments
{
    public static Task<string> ReadLicenseAsync() => ReadAsync("LMCUI.Legal.LICENSE.txt");

    public static Task<string> ReadNoticeAsync() => ReadAsync("LMCUI.Legal.NOTICE");

    private static async Task<string> ReadAsync(string resourceName)
    {
        using var stream = typeof(ProjectLegalDocuments).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Bundled legal document was not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
