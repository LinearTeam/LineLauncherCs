using LMC.Utils;
using LMC.Basic;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.ComponentModel.Design;
using Common;

namespace LMC.Account.OAuth
{
    public class OAuth
    {
        private String _loginUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?client_id=1cbfda79-fc84-47f9-8110-f924da9841ec&response_type=code&redirect_uri=http://127.0.0.1:40935&response_mode=query&scope=XboxLive.offline_access";
        private Logger _logger = new Logger("OA");
        private static bool s_isOaIng = false;
        private static HttpListener s_listener;
        private static bool s_cancel = false;

        //0 - success, 1 - pending, 2 - expired, 3 - deny
        async public Task<(string refreshToken, string accessToken, int result)> CheckResult(string deviceCode)
        {
            _logger.Info("正在检查设备代码流验证结果");
            string url = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            string contentType = "application/x-www-form-urlencoded";
            string accept = "application/json";
            var parameters = new Dictionary<string, string> {
                { "grant_type" , "urn:ietf:params:oauth:grant-type:device_code" },
                { "client_id" , "1cbfda79-fc84-47f9-8110-f924da9841ec"},
                { "device_code" , deviceCode }
            };
            var res = await HttpUtils.PostWithParameters(parameters, url, accept, contentType);
            if (res.Contains("authorization_pending"))
            {
                _logger.Info("结果：pending");
                return (null, null, 1);
            }
            if (res.Contains("authorization_declined"))
            {
                _logger.Info("结果：deny");
                return (null, null, 3);
            }
            if (res.Contains("expired_token"))
            {
                _logger.Info("结果：expired");
                return (null, null, 2);
            }
            _logger.Info("结果：success");
            return (JsonUtils.GetValueFromJson(res, "refresh_token"), JsonUtils.GetValueFromJson(res, "access_token"), 0);
        }

        async public Task<(string usercode, string msg, string devicecode, string verificationurl, int interval)> DeviceCodeOA()
        {
            _logger.Info("开始进行微软登录，方式设备代码流");
            var parameters = new Dictionary<string, string>
            {
                { "client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec" },
                { "scope", "XboxLive.signin offline_access" }
            };
            var res = await HttpUtils.PostWithParameters(parameters, "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode?mkt=zh-CN", "application/json", "application/x-www-form-urlencoded");
            string usercode = JsonUtils.GetValueFromJson(res, "user_code");
            string devicecode = JsonUtils.GetValueFromJson(res, "device_code");
            string msg = JsonUtils.GetValueFromJson(res, "message");
            string url = JsonUtils.GetValueFromJson(res, "verification_url");
            int interval = int.Parse(JsonUtils.GetValueFromJson(res, "interval"));
            _logger.Info("获取到设备代码");
            return (usercode, msg, devicecode, url, interval);
        }

        public async Task<(int done, Account account)> StartOA(string accessToken)
        {
            _logger.Info("开始进行设备代码流验证");
            try
            {
                var t = await StepThree(accessToken);
                Account account = new Account();
                account.Type = AccountType.MSA;
                account.AccessToken = t.mcatoken;
                account.Id = t.name;
                account.Uuid = t.uuid;
                //0 - done;1 - unknown exception;2 - nomc;3 - no code
                return (0, account);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Do not have mc"))
                {
                    _logger.Warn("登录失败，原因：用户没有购买MC");
                    return (2, null);
                }
                _logger.Warn(e.Message);
                _logger.Warn(e.StackTrace);
                if (e.InnerException != null)
                {
                    _logger.Warn(e.InnerException.Message);
                    _logger.Warn(e.InnerException.StackTrace);
                }
                return (1, null);
            };
        }

        async public Task<(int done, Account account,string refreshtoken)> StartOA(Action whenGotCode)
        {
            _logger.Info("开始进行微软登录，方式授权代码流");
            HttpListener listener = new HttpListener();
            s_listener = listener;
            try
            {
                var t = await StepOne(listener, whenGotCode);
                listener.Stop();
                //0 - done;1 - unknown exception;2 - nomc;3 - no code
                return (0, t.account, t.refreshtoken); 
            } catch(Exception e) {
                if(e.Message.Contains("Do not have mc"))
                {
                    _logger.Warn("登录失败，原因：用户没有购买MC");
                    return (2, null, null);
                }
                if (e.Message.Contains("No code"))
                {
                    _logger.Warn($"登录失败，原因：无法提取授权码");
                    return (3, null, null);
                }
                _logger.Warn(e.Message);
                _logger.Warn(e.StackTrace);
                if (e.InnerException != null)
                {
                    _logger.Warn(e.InnerException.Message);
                    _logger.Warn(e.InnerException.StackTrace);
                }
                listener.Stop();
                return (1,null,null); 
            };
        }
        public static bool CanOA()
        {
            return !s_isOaIng;
        }
        public static void CancelOA()
        {
            s_cancel = true;
            new Logger("OA").Info("正在终止微软登录请求");
            try
            {
                s_listener.Stop();
            }
            catch { }
        }
        public async static Task OA(Action<(int done, Account account, string refreshToken)> whenDone, Action whenGotCode)
        {
            if(s_isOaIng == true)
            {
                return;
            }
            s_isOaIng = true;
            s_cancel = false;
            
            OAuth oa = new OAuth();
            var t = await oa.StartOA(whenGotCode);
            whenDone(t);
            s_isOaIng = false;
        }

        async private Task<(Account account,string refreshtoken)> StepOne(HttpListener listener, Action whenGotCode)
        {
            if (s_cancel) { return (null, null); }
            _logger.Info("MSL step 1");
            string url = "http://127.0.0.1:40935/";
            System.Diagnostics.Process.Start("explorer.exe", $"\"{_loginUrl}\"");

            listener.Prefixes.Add(url);
            listener.Start();
            _logger.Info("监听中");

            HttpListenerContext context = await listener.GetContextAsync();
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string rawUrl = request.RawUrl;

            if (request.QueryString["code"] != null)
            {
                string code = request.QueryString["code"];
                _logger.Info("已获取用户返回值，正在重定向");
                response.Redirect("done");
                response.StatusCode = (int)HttpStatusCode.Redirect;
                response.Close();
                context = await listener.GetContextAsync();
                request = context.Request;
                response = context.Response;
                string responseString = "<html><body><center><h1>您已登录您的微软账号至Line Launcher，可以关闭此界面。</h1></center></body></html><!--" + rawUrl + "-->";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=UTF-8";
                response.ContentEncoding = Encoding.UTF8;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                whenGotCode();
                _logger.Info("服务器已停止，监听成功 ");
                _logger.Info("MSL step 2");
                var t = await StepTwo(code);
                if (!t.haveMc && t.name != null)
                {
                    throw new Exception("Do not have mc");
                }
                Account a = new Account();
                a.AccessToken = t.mcatoken;
                a.Uuid = t.uuid;
                a.Id = t.name;
                return (a, t.refreshtoken);
            }
            else
            {
                response.Redirect("error");
                response.StatusCode = (int)HttpStatusCode.Redirect;
                response.Close();
                context = await listener.GetContextAsync();
                request = context.Request;
                response = context.Response;
                string responseString = "<html><body><center><h1>登录失败，请重试或提交反馈！</h1><center></body></html><!--" + rawUrl + "-->";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=UTF-8";
                response.ContentEncoding = Encoding.UTF8;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                _logger.Warn("MSL失败，没有检测到Code");
                throw new Exception("No code : " + rawUrl);
            }
        }
        async public Task<(string uuid, string name, string mcatoken, string refreshtoken,bool haveMc)> StepTwo(string code)
        {
            if (s_cancel) { return (null, null, null, null, false); }
            string url = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            var parameters = new Dictionary<string, string>
            {
                { "client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec" },
                { "scope", "XboxLive.signin offline_access" },
                { "code", code},
                { "redirect_uri", "http://127.0.0.1:40935"},
                { "grant_type", "authorization_code"}
            };
            string context = await HttpUtils.PostWithParameters(parameters, url, "application/json", "application/x-www-form-urlencoded");
            string accesstoken = JsonUtils.GetValueFromJson(context, "access_token");
            string refreshtoken = JsonUtils.GetValueFromJson(context, "refresh_token");
            
            _logger.Info($"MSL step 3");
            var t = await StepThree(accesstoken);
            if(t.name != null)
            {
                return (t.uuid, t.name, t.mcatoken, refreshtoken, true);
            }
            return (null, null, null, null, false);
        }
        async public Task<(string accessToken, string refreshToken)> RefreshToken(string rtoken)
        {
            string url = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            var parameters = new Dictionary<string, string>
            {
                { "client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec" },
                { "scope", "XboxLive.signin offline_access" },
                { "refresh_token", rtoken},
                { "grant_type", "refresh_token"}
            };
            string context = await HttpUtils.PostWithParameters(parameters, url, "application/json", "application/x-www-form-urlencoded");
            return (JsonUtils.GetValueFromJson(context, "access_token"), JsonUtils.GetValueFromJson(context, "refresh_token"));
        }
        async public Task<(string uuid, string name, string mcatoken)> StepThree(string token)
        {
            if (s_cancel) { return (null, null, null); }
            string json = "{" +
                "\"Properties\": {" +
                    "\"AuthMethod\": \"RPS\"," +
                    "\"SiteName\": \"user.auth.xboxlive.com\"," +
                    "\"RpsTicket\": \"d=" + token + "\"}," +
                "\"RelyingParty\": \"http://auth.xboxlive.com\"," +
                "\"TokenType\": \"JWT\"}";
            string contenttype = "application/json";
            string url = "https://user.auth.xboxlive.com/user/authenticate";
            string xblres = await HttpUtils.PostWithJson(json, url, contenttype, contenttype);
            string t = JsonUtils.GetValueFromJson(xblres, "Token");
            _logger.Info("MSL step 4");
            return await StepFour(t);
        }

        async public Task<(string uuid, string name, string mcatoken)> StepFour(string tokenth)
        {
            if (s_cancel) { return (null, null, null); }
            string json = "{" +
                "\"Properties\": {" +
                    "\"SandboxId\": \"RETAIL\"," +
                    "\"UserTokens\": [\"" + tokenth + "\"]}," +
                "\"RelyingParty\": \"rp://api.minecraftservices.com/\"," +
                "\"TokenType\": \"JWT\"}";
            string url = "https://xsts.auth.xboxlive.com/xsts/authorize";
            string contenttype = "application/json";
            string xstsres = await HttpUtils.PostWithJson(json, url, contenttype, contenttype);
            string token = JsonUtils.GetValueFromJson(xstsres, "Token");
            string uhs = JsonUtils.GetValueFromJson(xstsres, "DisplayClaims.xui[0].uhs");
            _logger.Info("MSL step 5");
            var t = await StepFive(token, uhs);
            return t;
        }
        async public Task<(string uuid,string name,string mcatoken)> StepFive(string tokenf, string uhs)
        {
            if (s_cancel) { return (null, null, null); }
            string json = "{ \"identityToken\": \"XBL3.0 x=" + uhs + $";{tokenf}\"" + "}";
            string url = "https://api.minecraftservices.com/authentication/login_with_xbox";
            string contenttype = "application/json";
            string mjapires = await HttpUtils.PostWithJson(json, url, contenttype, contenttype);
            string token = JsonUtils.GetValueFromJson(mjapires, "access_token");
            _logger.Info("MSL step 6");
            var t = await StepSix(token);
            if (t.Item1)
            {
                return (t.Item2, t.Item3, token);
            }
            else return (null, null,null);
        }
        async public Task<(bool, string, string)> StepSix(string tokenf)
        {
            if (s_cancel) { return (false, null, null); }
            string url = "https://api.minecraftservices.com/entitlements/mcstore";
            string accept = "application/json";
            string checkres = await HttpUtils.GetWithAuth($"Bearer {tokenf}", url, accept);

            using (JsonDocument document = JsonDocument.Parse(checkres))
            {
                JsonElement itemsElement;
                bool haveItems = document.RootElement.TryGetProperty("items", out itemsElement) && itemsElement.ValueKind == JsonValueKind.Array;
                url = "https://api.minecraftservices.com/minecraft/profile";
                string profileres = await HttpUtils.GetWithAuth($"Bearer {tokenf}", url, accept);
                bool haveMc = haveItems && itemsElement.GetArrayLength() > 0 && !profileres.Contains("NOT_FOUND");
                Console.WriteLine("Does Minecraft have : " + haveMc.ToString());
                if (haveMc)
                {
                    string uuid = JsonUtils.GetValueFromJson(profileres, "id");
                    string name = JsonUtils.GetValueFromJson(profileres, "name");
                    return (true, uuid, name);
                }
                return (false, null, null);
            }
        }
    }
}
