using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class Secrets
    {
        private static string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/DoNotSendThisToAnyone/请勿将此发送给他人/secrets.line";
        private static LineFileParser lfp = new LineFileParser();
        private static Logger logger = new Logger("SEC");
        static async Task<string> GetCpuIDAsync()
        {
            
            return await Task.Run(() =>
            {
                string cpuID = string.Empty;
                try
                {
                    ManagementClass mc = new ManagementClass("Win32_Processor");
                    ManagementObjectCollection moc = mc.GetInstances();
                    foreach (ManagementObject mo in moc)
                    {
                        cpuID = mo.Properties["ProcessorId"].Value.ToString();
                    }
                    moc = null;
                    mc = null;
                    return cpuID;
                }
                catch
                {
                    return "Unknown";
                }
            });
        }
        async public static Task write(string section, string key, string value)
        {
            int i = 0;
            string strCpuID = await GetCpuIDAsync();
            string totalStr = Encrypt3Des(value, strCpuID);
            logger.info(i++.ToString());
            lfp.WriteFile(path, key, totalStr, section);
            logger.info(i++.ToString());
        }
        async public static Task<string> read(string section, string key)
        {
            string strCpuID = await GetCpuIDAsync();
            strCpuID = strCpuID.ToCharArray()[2].ToString() + strCpuID.ToCharArray()[4].ToString() + strCpuID.ToCharArray()[1].ToString() + strCpuID;
            string enStr = lfp.ReadFile(path, key, section);
            if (enStr != string.Empty || enStr != null)
            {
                string totalStr = Decrypt3Des(enStr, strCpuID);
                return totalStr;
            }
            return null;
        }
        public static string Encrypt3Des(string aStrString, string aStrKey, CipherMode mode = CipherMode.ECB, string iv = "13654774")
        {
            try
            {
                var des = new TripleDESCryptoServiceProvider
                {
                    Key = Encoding.UTF8.GetBytes(aStrKey),
                    Mode = mode
                };
                if (mode == CipherMode.CBC)
                {
                    des.IV = Encoding.UTF8.GetBytes(iv);
                }
                var desEncrypt = des.CreateEncryptor();
                byte[] buffer = Encoding.UTF8.GetBytes(aStrString);
                return Convert.ToBase64String(desEncrypt.TransformFinalBlock(buffer, 0, buffer.Length));
            }
            catch (Exception e)
            {
                return string.Empty;
            }
        }

        public static string Decrypt3Des(string aStrString, string aStrKey, CipherMode mode = CipherMode.ECB, string iv = "13654774")
        {
            try
            {
                var des = new TripleDESCryptoServiceProvider
                {
                    Key = Encoding.UTF8.GetBytes(aStrKey),
                    Mode = mode,
                    Padding = PaddingMode.PKCS7
                };
                if (mode == CipherMode.CBC)
                {
                    des.IV = Encoding.UTF8.GetBytes(iv);
                }
                var desDecrypt = des.CreateDecryptor();
                var result = "";
                byte[] buffer = Convert.FromBase64String(aStrString);
                result = Encoding.UTF8.GetString(desDecrypt.TransformFinalBlock(buffer, 0, buffer.Length));
                return result;
            }
            catch (Exception e)
            {
                return string.Empty;
            }
        }
    }
}
