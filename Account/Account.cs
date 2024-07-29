using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Account
{
    public class Account
    {
        private string _id = "Steve";
        private string _uuid = "s0000000";
        private int _type = 0; //0 - msa, 1 - offline, 2 - authlib(mojang)
        private string _AuthLib_authServer = string.Empty;
        private string _AuthLib_password = string.Empty;
        private string _accessToken = "0000";
        private string _AuthLib_account = string.Empty;

        public string id { get { return _id; } set { _id = value; } }
        public string uuid { get { return _uuid; } set { _uuid = value; } } 
        public int type { get { return _type; } set { _type = value; } }
        public string AuthLib_authServer { get { return _AuthLib_authServer; } set { _AuthLib_authServer = value; } }
        public string AuthLib_password { get { return _AuthLib_password; } set { _AuthLib_password = value; } }
        public string accessToken { get { return _accessToken;} set { _accessToken = value; } }
        public string AuthLib_account { get { return _AuthLib_account; } set { _AuthLib_account = value; } }
    }
}
