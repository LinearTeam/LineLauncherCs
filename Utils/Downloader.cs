using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace LMC.Utils
{
    public class Downloader : WebClient
    {
        public TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private Uri _url;
        private string _path;
        private bool _done = false;
        private DispatcherTimer _timer = new DispatcherTimer();
        public Downloader(Uri url, string path)
        {
            Headers.Add("User-Agent", $"LMC/C{App.LauncherVersion}");           
            this._url = url;
            this._path = path;
            _timer.Tick += TimeoutCheck;
        }
        
        public Downloader(string url, string path)
        {
            Headers.Add("User-Agent", $"LMC/C{App.LauncherVersion}");
            this._url = new Uri(url);
            this._path = path;
            _timer.Tick += TimeoutCheck;
        }

        private void TimeoutCheck(object sender, EventArgs args)
        {
            if (Timeout.TotalSeconds == 0) {
                return;
            }
            if (!_done) { throw new TimeoutException($"在下载文件{_url}到{_path}时，耗时超过{Timeout.TotalSeconds}秒。"); }
            else { _timer.Stop(); _timer.IsEnabled = false; }
        }

        public async Task DownloadFileAsync()
        {
            _timer.Interval = Timeout;
            _timer.Start();
            _timer.IsEnabled = true;
            Directory.CreateDirectory(Directory.GetParent(this._path).FullName);
            if (File.Exists(this._path)) { 
                File.Delete(this._path);
            }
            byte[] buffer = await DownloadDataTaskAsync(this._url);
            File.WriteAllBytes(this._path, buffer);
            _done = true;
            _timer.Stop();
            _timer.IsEnabled= false;
            buffer = null;
        }
    }
}
