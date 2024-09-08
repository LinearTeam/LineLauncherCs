
using LMC.Account.OAuth;
using LMC.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Account
{
    public enum AccountType{MSA, OFFLINE, AUTHLIB};

    public class AccountManager
    {
        private static Logger s_logger = new Logger("AM");
        async public static Task AddAccount(Account account, string refreshToken = null)
        {
            if(account.Type == AccountType.MSA)
            {
                await Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "refresh_token", refreshToken);
                await Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "id", account.Id);
            }
            if (account.Type == AccountType.AUTHLIB) {
                await Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "authServer", account.AuthLib_authServer);
                await Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "authPassword", account.AuthLib_password);
                await Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "authAccount", account.AuthLib_account);
                await Secrets.Write("acc_" + account.Uuid + "_" + account.Type, "id", account.Id);
            }
            if (account.Type == AccountType.OFFLINE)
            {
                await Secrets.Write("acc_" + account.Id + "_" + account.Type, "id", account.Id);
            }
            await AccountPage.RefreshAccounts();
        }

        async public static Task DeleteAccount(Account account)
        {
            string section = null;
            if (account.Type == AccountType.MSA)
            {
                section = "acc_" + account.Uuid + "_" + account.Type;
            }
            if (account.Type == AccountType.AUTHLIB)
            {
                section = "acc_" + account.Uuid + "_" + account.Type;
            }
            if (account.Type == AccountType.OFFLINE)
            {
                section = "acc_" + account.Id + "_" + account.Type;
            }
            Secrets.DeleteSection(section);
        }

        async public static Task DeleteAccount(string id)
        {
            string section = null;
            Secrets.DeleteSection(section);
        }

        async public static Task<List<Account>> GetAccounts(bool refresh)
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
                        account.Id = section.Substring(4).Replace("_OFFLINE","");
                        accounts.Add(account);
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
                        continue;
                    }
                    if(type==AccountType.MSA)
                    {
                        if (refresh)
                        {
                            string refreshToken = await Secrets.Read(section, "refresh_token");
                            OAuth.OAuth oa = new OAuth.OAuth();
                            var tokens = await oa.RefreshToken(refreshToken);
                            await Secrets.Write(section, "refresh_token", tokens.refreshToken);
                            account.AccessToken = tokens.accessToken;
                        }
                        account.Id = await Secrets.Read(section, "id");
                        account.Uuid = section.Substring(4).Replace("_MSA", "");
                        accounts.Add(account);
                    }
                }
            }
            return accounts;
        }
    }
}
