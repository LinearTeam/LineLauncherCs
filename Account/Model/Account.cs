namespace LMC.Account.Model
{
    public class Account
    {
        private string _id = "Steve";
        private string _uuid = "0000000";
        private AccountType _type = AccountType.MSA;
        private string _authLib_authServer = string.Empty;
        private string _authLib_password = string.Empty;
        private string _accessToken = "0000";
        private string _authLib_account = string.Empty;

        public string Id { get { return _id; } set { _id = value; } }
        public string Uuid { get { return _uuid; } set { _uuid = value; } } 
        public AccountType Type { get { return _type; } set { _type = value; } }
        public string AuthLib_authServer { get { return _authLib_authServer; } set { _authLib_authServer = value; } }
        public string AuthLib_password { get { return _authLib_password; } set { _authLib_password = value; } }
        public string AccessToken { get { return _accessToken;} set { _accessToken = value; } }
        public string AuthLib_account { get { return _authLib_account; } set { _authLib_account = value; } }
    }
}
