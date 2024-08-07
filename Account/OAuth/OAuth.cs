using LMC.Basic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Threading;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using System.Text.Json;

namespace LMC.Account.OAuth
{
    public class OAuth
    {
        private String loginurl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?client_id=1cbfda79-fc84-47f9-8110-f924da9841ec&response_type=code&redirect_uri=http://127.0.0.1:40935&response_mode=query&scope=XboxLive.offline_access";
        private Logger logger = new Logger("OA");
        static private bool isOaIng = false;
        async public Task<(int done, Account account,string refreshtoken)> startOA()
        {

            HttpListener listener = new HttpListener();
            try
            {
                var t = await stepOne(listener);
                listener.Stop();
                return (0, t.account, t.refreshtoken); 
            } catch(Exception e) {
                logger.warn(e.Message);    
                listener.Stop();
                return (1,null,null); 
            };
        }

        async public static Task oa(System.Windows.Controls.Button b)
        {
            if(isOaIng == true)
            {
                var confirmDialogc = new ContentDialog(MainWindow.cdp);

                confirmDialogc.SetCurrentValue(ContentDialog.TitleProperty, MainWindow.i18NTools.getString("lmc.messages.msastart.title"));
                confirmDialogc.SetCurrentValue(ContentControl.ContentProperty, MainWindow.i18NTools.getString("lmc.messages.msastart.logging"));
                confirmDialogc.SetCurrentValue(ContentDialog.CloseButtonTextProperty, MainWindow.i18NTools.getString("lmc.messages.continue"));

                await confirmDialogc.ShowAsync();
                return;
            }
            isOaIng = true;
            var confirmDialog = new ContentDialog(MainWindow.cdp);

            confirmDialog.SetCurrentValue(ContentDialog.TitleProperty, MainWindow.i18NTools.getString("lmc.messages.msastart.title"));
            confirmDialog.SetCurrentValue(ContentControl.ContentProperty, MainWindow.i18NTools.getString("lmc.messages.msastart.msg"));
            confirmDialog.SetCurrentValue(ContentDialog.CloseButtonTextProperty, MainWindow.i18NTools.getString("lmc.messages.continue"));

            await confirmDialog.ShowAsync();

            MainWindow.infobar.Message = MainWindow.i18NTools.getString("lmc.messages.logging");
            MainWindow.infobar.IsOpen = true;
            MainWindow.infobar.IsClosable = false;
            MainWindow.infobar.Severity = InfoBarSeverity.Warning;
            MainWindow.mnv.Navigate(typeof(AccountPage));
            OAuth oa = new OAuth();
            var t = await oa.startOA();
            if (t.done == 0)
            {
                string rt = t.refreshtoken;
                LMC.Account.Account a = t.account;
                await AccountManager.addAccount(a, rt);
                var doneLoginDialog = new ContentDialog(MainWindow.cdp);
                MainWindow.infobar.Severity = InfoBarSeverity.Success;
                MainWindow.infobar.Message = MainWindow.i18NTools.getString("lmc.messages.msastart.done").Replace("${uuid}", a.uuid).Replace("${id}", a.id);
                MainWindow.infobar.IsClosable = true;
                isOaIng = false;
                return;
            }
            MainWindow.infobar.Severity = InfoBarSeverity.Error;
            MainWindow.infobar.Message = MainWindow.i18NTools.getString("lmc.messages.msastart.error");
            MainWindow.infobar.IsClosable = true;
            isOaIng = false;
        }

        async public Task<(Account account,string refreshtoken)> stepOne(HttpListener listener)
        {
            logger.info("MSL step 1");
            string url = "http://127.0.0.1:40935/";
            System.Diagnostics.Process.Start("explorer.exe", $"\"{loginurl}\"");

            listener.Prefixes.Add(url);
            listener.Start();
            logger.info("Listening...");

            HttpListenerContext context = await listener.GetContextAsync();
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (request.QueryString["code"] != null)
            {
                string code = request.QueryString["code"];
                string responseString = $"<html><body><center><h1>{MainWindow.i18NTools.getString("lmc.msastart.responecodedone")}</h1></center></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=UTF-8";
                response.ContentEncoding = Encoding.UTF8;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                logger.info("Server stopped.");
                logger.info("MSL step 2");
                var t = await stepTwo(code);
                if (!t.haveMc)
                {
                    throw new Exception("Do not have mc");
                }
                Account a = new Account();
                a.accessToken = t.mcatoken;
                a.uuid = t.uuid;
                a.id = t.name;
                return (a, t.refreshtoken);
            }
            else
            {
                string responseString = $"<html><body><center><h1>{MainWindow.i18NTools.getString("lmc.msastart.responecodeerror")}</h1><center></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=UTF-8";
                response.ContentEncoding = Encoding.UTF8;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                logger.warn("MSL Error:no code");
                throw new Exception("No code : " + request.ToString());
            }
        }
        async public Task<(string uuid, string name, string mcatoken, string refreshtoken,bool haveMc)> stepTwo(string code)
        {
            string url = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            var parameters = new Dictionary<string, string>
            {
                { "client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec" },
                { "scope", "XboxLive.signin offline_access" },
                { "code", code},
                { "redirect_uri", "http://127.0.0.1:40935"},
                { "grant_type", "authorization_code"}
            };
            string context = await PostWithParameters(parameters, url, "application/json", "application/x-www-form-urlencoded");
            string accesstoken = GetValueFromJson(context, "access_token");
            string refreshtoken = GetValueFromJson(context, "refresh_token");
            
            logger.info($"MSL step 3");
            var t = await stepThree(accesstoken);
            if(t.name != null)
            {
                return (t.uuid, t.name, t.mcatoken, refreshtoken, true);
            }
            return (null, null, null, null, false);
        }
        async public Task<List<string>> refreshToken(string rtoken)
        {
            string url = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            var parameters = new Dictionary<string, string>
            {
                { "client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec" },
                { "scope", "XboxLive.signin offline_access" },
                { "refresh_token", rtoken},
                { "grant_type", "refresh_token"}
            };
            string context = await PostWithParameters(parameters, url, "application/json", "application/x-www-form-urlencoded");
            List<string> res = new List<string>();
            res.Add(GetValueFromJson(context, "refresh_token"));
            res.Add(GetValueFromJson(context, "access_token"));
            return res;
        }
        async public Task<(string uuid, string name, string mcatoken)> stepThree(string token)
        {
            string json = "{" +
                "\"Properties\": {" +
                    "\"AuthMethod\": \"RPS\"," +
                    "\"SiteName\": \"user.auth.xboxlive.com\"," +
                    "\"RpsTicket\": \"d=" + token + "\"}," +
                "\"RelyingParty\": \"http://auth.xboxlive.com\"," +
                "\"TokenType\": \"JWT\"}";
            string contenttype = "application/json";
            string url = "https://user.auth.xboxlive.com/user/authenticate";
            string xblres = await PostWithJson(json, url, contenttype, contenttype);
            string t = GetValueFromJson(xblres, "Token");
            logger.info("MSL step 4");
            return await stepFour(t);
        }

        async public Task<(string uuid, string name, string mcatoken)> stepFour(string tokenth)
        {
            string json = "{" +
                "\"Properties\": {" +
                    "\"SandboxId\": \"RETAIL\"," +
                    "\"UserTokens\": [\"" + tokenth + "\"]}," +
                "\"RelyingParty\": \"rp://api.minecraftservices.com/\"," +
                "\"TokenType\": \"JWT\"}";
            string url = "https://xsts.auth.xboxlive.com/xsts/authorize";
            string contenttype = "application/json";
            string xstsres = await PostWithJson(json, url, contenttype, contenttype);
            string token = GetValueFromJson(xstsres, "Token");
            string uhs = GetValueFromJson(xstsres, "DisplayClaims.xui[0].uhs");
            logger.info("MSL step 5");
            var t = await stepFive(token, uhs);
            return t;
        }
        async public Task<(string uuid,string name,string mcatoken)> stepFive(string tokenf, string uhs)
        {
            string json = "{ \"identityToken\": \"XBL3.0 x=" + uhs + $";{tokenf}\"" + "}";
            string url = "https://api.minecraftservices.com/authentication/login_with_xbox";
            string contenttype = "application/json";
            string mjapires = await PostWithJson(json, url, contenttype, contenttype);
            string token = GetValueFromJson(mjapires, "access_token");
            logger.info("MSL step 6");
            var t = await stepSix(token);
            if (t.Item1)
            {
                return (t.Item2, t.Item3, token);
            }
            else return (null, null,null);
        }
        async public Task<(bool, string, string)> stepSix(string tokenf)
        {
            string url = "https://api.minecraftservices.com/entitlements/mcstore";
            string accept = "application/json";
            string checkres = await GetWithAuth($"Bearer {tokenf}", url, accept);

            using (JsonDocument document = JsonDocument.Parse(checkres))
            {
                JsonElement itemsElement;
                bool haveItems = document.RootElement.TryGetProperty("items", out itemsElement) && itemsElement.ValueKind == JsonValueKind.Array;
                url = "https://api.minecraftservices.com/minecraft/profile";
                string profileres = await GetWithAuth($"Bearer {tokenf}", url, accept);
                bool haveMc = haveItems && itemsElement.GetArrayLength() > 0 && !profileres.Contains("NOT_FOUND");
                Console.WriteLine("Does Minecraft have : " + haveMc.ToString());
                if (haveMc)
                {
                    string uuid = GetValueFromJson(profileres, "id");
                    string name = GetValueFromJson(profileres, "name");
                    return (true, uuid, name);
                }
                return (false, null, null);
            }
        }

        private string GetValueFromJson(string jsonString, string path)
        {
            using (JsonDocument document = JsonDocument.Parse(jsonString))
            {
                string[] keys = path.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                JsonElement element = document.RootElement;
                foreach (var key in keys)
                {
                    if (key.Contains("["))
                    {
                        var parts = key.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                        var arrayKey = parts[0];
                        var index = int.Parse(parts[1]);

                        if (element.TryGetProperty(arrayKey, out JsonElement arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
                        {
                            element = arrayElement[index];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        if (element.TryGetProperty(key, out JsonElement nextElement))
                        {
                            element = nextElement;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                return element.ToString();
            }
        }


        async public Task<string> PostWithParameters(Dictionary<string, string> parameters, string url, string accept, string contentType)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

                var content = new FormUrlEncodedContent(parameters);

                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseContent = response.Content.ReadAsStringAsync().Result;
                return responseContent;
            }
        }

        async public Task<string> GetWithAuth(string auth, string url, string accept)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
                client.DefaultRequestHeaders.Add("Authorization", auth);



                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                return responseContent;
            }
        }

        async public Task<string> PostWithJson(string json, string url, string accept, string contentType)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

                var content = new StringContent(json);

                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                return responseContent;
            }
        }
    }
}
