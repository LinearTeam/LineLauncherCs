using LMC.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Account
{
    public class AccountManager
    {
        private static Logger logger = new Logger("AM");
        async public static Task addAccount(Account account, string refreshToken = null)
        {
            if(account.type == 0)
            {
                await Secrets.write(account.uuid + "_" + account.type, "refresh_token", refreshToken);
            }
            if (account.type == 2) {
                await Secrets.write(account.uuid + "_" + account.type, "authServer", account.AuthLib_authServer);
                await Secrets.write(account.uuid + "_" + account.type, "authPassword", account.AuthLib_password);
                await Secrets.write(account.uuid + "_" + account.type, "authAccount", account.AuthLib_account);
            }
            if (account.type == 1)
            {
                await Secrets.write(account.uuid + "_" + account.type, "id", account.id);
            }
        }
    }
}
