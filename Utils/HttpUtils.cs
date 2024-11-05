using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Threading.Tasks;

namespace LMC.Utils
{
    public static class HttpUtils
    {
        private static HttpClient _httpClient = new HttpClient();
       
        static HttpUtils(){
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"LMC/C{App.LauncherVersion} ({App.LauncherBuildVersion})");
        }

        public async static Task<string> GetWithAuth(string auth, string url, string accept)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", auth);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        public async static Task<string> GetString(string url)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }
                
        public async static Task<string> GetString(Uri url)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        public async static Task<string> PostWithJson(string json, string url, string accept, string contentType)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

            var content = new StringContent(json);

            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        public async static Task<string> PostWithParameters(Dictionary<string, string> parameters, string url, string accept, string contentType)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

            var content = new FormUrlEncodedContent(parameters);

            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);


            var response = await _httpClient.PostAsync(url, content);
            var responseContent = response.Content.ReadAsStringAsync().Result;
            return responseContent;
            
            
        }

        public async static Task<string> PostWithoutParameters(string url, string accept, string contentType)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            var content = new StringContent("");

            var response = await _httpClient.PostAsync(url, content);
            return response.Content.ReadAsStringAsync().Result;
        }
    }
}