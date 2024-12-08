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
        public TimeSpan Timeout = TimeSpan.FromSeconds(60);
        private Uri _url;
        private string _path;
        private bool _done = false;
        private bool _exception = false;
        private Exception _ex;

        public Downloader(Uri url, string path)
        {
            Headers.Add("User-Agent", $"LMC/C{App.LauncherVersion}-{App.LauncherBuildVersion} (Mozilla/5.0)");           
            this._url = url;
            this._path = path;
        }
        
        public Downloader(string url, string path)
        {
            Headers.Add("User-Agent", $"LMC/C{App.LauncherVersion}-{App.LauncherBuildVersion} (Mozilla/5.0)");
            this._url = new Uri(url);
            this._path = path;
        }

        private async Task Download()
        {
            try
            {
                byte[] buffer = await DownloadDataTaskAsync(this._url);
                File.WriteAllBytes(this._path, buffer);
                _done = true;
                buffer = null;
            }
            catch (Exception ex) {
                _exception = true;
                _ex = ex;
            }
        }

        public async Task DownloadFileAsync()
        {
            Directory.CreateDirectory(Directory.GetParent(this._path).FullName);
            if (File.Exists(this._path)) {
                File.Delete(this._path);
            }
            var start = DateTime.Now;
            Download();
            while (true)
            {
                await Task.Delay(20);
                if (_done)
                {
                    return;
                }
                else if(_exception) {
                    throw _ex;
                }else if(DateTime.Now.Subtract(start).TotalMilliseconds >= Timeout.TotalMilliseconds)
                {
                    this.Dispose();
                    throw new TimeoutException($"下载文件{_url}到{_path}时超时");
                }
            }
        }
    }
}
