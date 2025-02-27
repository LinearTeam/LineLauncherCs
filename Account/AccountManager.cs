using LMC.Basic;
using LMC.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LMC.Account
{
    public enum AccountType{MSA, OFFLINE, AUTHLIB};
    public class AccountManager
    {

        public static Dictionary<string, Account> RefreshedAccounts = new Dictionary<string, Account>();
        public static Dictionary<string, BitmapImage> Avatars = new Dictionary<string, BitmapImage>();
        private static Logger s_logger = new Logger("AM");

        public static string GetKey(Account account)
        {
            if (account.Type == AccountType.MSA)
            {
                return $"{account.Uuid}_MSA";
            }
            if (account.Type == AccountType.AUTHLIB)
            {
                return $"{account.Uuid}_AUTHLIB";
            }
            else
            {
                return $"{account.Id}_Offline";
            }
        }
        public static void AddAccount(Account account, string refreshToken = null, bool onlyAddToList = false)
        {
            if(account.Type == AccountType.MSA)
            {
                if (RefreshedAccounts.ContainsKey(GetKey(account))) { RefreshedAccounts.Remove(GetKey(account)); }
                RefreshedAccounts.Add(GetKey(account), account);
                GetAvatarAsync(account, 128);
                if (onlyAddToList) {
                    return;
                }
                if (refreshToken != null)
                {
                    Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "refresh_token", refreshToken);
                }
                Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "id", account.Id);
            }
            if (account.Type == AccountType.AUTHLIB) {
                Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "authServer", account.AuthLib_authServer);
                Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "authPassword", account.AuthLib_password);
                Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "authAccount", account.AuthLib_account);
                Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "id", account.Id);
                if (RefreshedAccounts.ContainsKey(GetKey(account))) { RefreshedAccounts.Remove(GetKey(account)); }
                RefreshedAccounts.Add(GetKey(account), account);
            }
            if (account.Type == AccountType.OFFLINE)
            {
                Secrets.Write("acc_" + account.Id + "_" + account.Type, "id", account.Id);
                if (RefreshedAccounts.ContainsKey(GetKey(account))) { RefreshedAccounts.Remove(GetKey(account)); }
                RefreshedAccounts.Add(GetKey(account), account);
            }

        }

        public static void DeleteAccount(Account account)
        {
            string section = null;
            if (account.Type == AccountType.MSA)
            {
                section = "acc_" + account.Uuid + "_" + account.Type;
                if (Directory.Exists("./LMC/cache/" + account.Uuid)) { Directory.Delete("./LMC/cache/" + account.Uuid, true);}
                RefreshedAccounts.Remove(GetKey(account));
            }
            if (account.Type == AccountType.AUTHLIB)
            {
                section = "acc_" + account.Uuid + "_" + account.Type;
                RefreshedAccounts.Remove(GetKey(account));
            }
            if (account.Type == AccountType.OFFLINE)
            {
                section = "acc_" + account.Id + "_" + account.Type;
                RefreshedAccounts.Remove(GetKey(account));
            }
            Secrets.DeleteSection(section);

        }

        private static async Task DownloadAvatar(Account account, int size)
        {
            
            Directory.CreateDirectory("./LMC/cache/" + account.Uuid);
            int i = 0;

            while (true)
            {
                i++;
                try
                {
                    Downloader downloader = new Downloader("https://crafatar.com/avatars/" + account.Uuid + "?size=" + size, "./LMC/cache/" + account.Uuid + $"/avat-{size}.png");
                    await downloader.DownloadFileAsync();
                }
                catch (Exception ex)
                {
                    s_logger.Warn("下载头像失败：" + ex.Message + "\n" + ex.StackTrace);
                    if(i > 9) {
                        return;
                    }
                }
            }
        }

        public static async Task<BitmapImage> GetAvatarAsync(Account account, int size)
        {
            string avatarPath = "./LMC/cache/" + account.Uuid + $"/avat-{size}.png";
            if (!File.Exists(avatarPath) && !Avatars.ContainsKey(account.Uuid))
            {
                await DownloadAvatar(account, size);
                await Task.Delay(100);
            }

            if (Avatars.ContainsKey(account.Uuid))
            {
                return Avatars[account.Uuid];
            }
            
            BitmapImage avatarImage = new BitmapImage();
            try
            {
                using (FileStream fs = new FileStream(avatarPath, FileMode.Open, FileAccess.Read))
                {
                    avatarImage.BeginInit();
                    avatarImage.CacheOption = BitmapCacheOption.OnLoad;
                    avatarImage.StreamSource = fs;
                    avatarImage.EndInit();
                    Avatars.Add(account.Uuid, avatarImage);
                }
            }
            catch (Exception ex)
            {
                s_logger.Warn("读取头像失败：" + ex.Message + "\n" + ex.StackTrace);
            }

            return avatarImage;
        }




        public static async Task<List<Account>> GetAccounts(bool refresh)
        {
            List<Account> accounts = new List<Account>();
            var sections = Secrets.ReadSections(); 
            foreach (var section in sections)
            {
                if (section.StartsWith("acc_"))
                {
                    var arr = section.Split('_');
                    string typeStr = arr[arr.Length-1];
                    AccountType type = AccountType.OFFLINE;
                    switch(typeStr)
                    {
                        case "MSA": type= AccountType.MSA; break;
                        case "OFFLINE": type = AccountType.OFFLINE; break;
                        case "AUTHLIB": type = AccountType.AUTHLIB; break;
                    }
                    Account account = new Account();
                    account.Type = type; 
                    if (type==AccountType.OFFLINE)
                    {
                        account.Id = await Secrets.Read(section, "id");
                        accounts.Add(account);
                        AddAccount(account);
                        continue;
                    }
                    if(type==AccountType.AUTHLIB)
                    {
                        account.AuthLib_authServer = await Secrets.Read(section, "authServer");
                        account.AuthLib_password = await Secrets.Read(section, "authPassword");
                        account.AuthLib_account= await Secrets.Read(section, "authAccount");
                        account.Id = await Secrets.Read(section, "id");
                        account.Uuid = section.Substring(4).Replace("_AUTHLIB", "");
                        accounts.Add(account);
                        AddAccount(account);
                        continue;
                    }
                    if(type==AccountType.MSA)
                    {
                        if (refresh)
                        {
                            string refreshToken = await Secrets.Read(section, "refresh_token");
                            OAuth.OAuth oa = new OAuth.OAuth();
                            var tokens = await oa.RefreshToken(refreshToken);
                            Secrets.Write(section, "refresh_token", tokens.refreshToken);
                            account.AccessToken = tokens.accessToken;
                        };
                        account.Id = await Secrets.Read(section, "id");
                        account.Uuid = section.Substring(4).Replace("_MSA", "");
//                        DownloadSkin(account);
                        try
                        {
                            accounts.Add(account);
                        }catch{ }
                        AddAccount(account);
                    }
                }
            }
            return accounts;
        }

        public static async Task SetSelectedAccount(Account account)
        {
            string secStr = "acc_" + (account.Type == AccountType.OFFLINE ? account.Id : account.Uuid) + "_" + account.Type;
            Secrets.Write("Main", "Selected", secStr);
        }
        
        public static async Task<Account> GetSelectedAccount()
        {
            if (string.IsNullOrEmpty(await Secrets.Read("Main", "Selected")))
            {
                var selected = await GetAccounts(false);
                if (selected.Count > 0)
                {
                    var first = selected.First();
                    return first;
                }
                else
                {
                    Account account = new Account();
                    account.Id = "未添加账号";
                    account.Type = AccountType.OFFLINE;
                    return account;
                }
            }
            else
            {
                var section = await Secrets.Read("Main", "Selected");
                if(!(Secrets.ReadSections().Contains(section)))
                {
                    Secrets.DeleteKey("Main", "Selected");
                    return await GetSelectedAccount();
                }
                var arr = section.Split('_');
                string typeStr = arr[arr.Length - 1];
                AccountType type = AccountType.OFFLINE;
                switch (typeStr)
                {
                    case "MSA": type = AccountType.MSA; break;
                    case "OFFLINE": type = AccountType.OFFLINE; break;
                    case "AUTHLIB": type = AccountType.AUTHLIB; break;
                }

                Account account = new Account();
                account.Type = type;
                if (type == AccountType.OFFLINE)
                {
                    account.Id = await Secrets.Read(section, "id");
                    return account;
                }

                if (type == AccountType.AUTHLIB)
                {
                    account.AuthLib_authServer = await Secrets.Read(section, "authServer");
                    account.AuthLib_password = await Secrets.Read(section, "authPassword");
                    account.AuthLib_account = await Secrets.Read(section, "authAccount");
                    account.Id = await Secrets.Read(section, "id");
                    account.Uuid = section.Substring(4).Replace("_AUTHLIB", "");
                    return account;
                }

                if (type == AccountType.MSA)
                {
                    account.Id = await Secrets.Read(section, "id");
                    account.Uuid = section.Substring(4).Replace("_MSA", "");
//                        DownloadSkin(account);
                    return account;
                }
            }
            return null;
        }
    }
}
