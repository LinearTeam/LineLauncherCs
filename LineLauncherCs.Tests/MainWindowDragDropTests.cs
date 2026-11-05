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

using Avalonia;
using Avalonia.Input;
using LMCUI;

namespace LineLauncherCs.Tests;

public class MainWindowDragDropTests
{
    [Fact]
    public Task FileDrag_UsesBubbleRoutingAndObservesHandledEvents() =>
        TestAppBuilder.RunOnUiThreadAsync(() =>
        {
            var window = new MainWindow();
            try
            {
                Assert.True(DragDrop.GetAllowDrop(window));
                var data = new DataTransfer();
                using var lifetime = (IDisposable)data;
                foreach (var routedEvent in new[]
                         { DragDrop.DragEnterEvent, DragDrop.DragOverEvent, DragDrop.DropEvent })
                foreach (var handled in new[] { false, true })
                {
                    var args = new DragEventArgs(routedEvent, data, window, default(Point), KeyModifiers.None)
                    {
                        Handled = handled,
                        DragEffects = DragDropEffects.Copy
                    };

                    window.RaiseEvent(args);

                    // The unopened window rejects input before startup, proving its handler ran.
                    Assert.True(args.Handled);
                    Assert.Equal(DragDropEffects.None, args.DragEffects);
                }
            }
            finally
            {
                window.Close();
            }
        });
}
