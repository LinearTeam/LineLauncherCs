using System;
using System.Collections.Generic;
using System.Management;
using System.Security.Cryptography;

using System.Text;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class Secrets
    {
        private static string s_path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/DoNotSendThisToAnyone/请勿将此发送给他人/secrets.line";
        private static LineFileParser s_lineFileParser = new LineFileParser();
        private static Logger s_logger = new Logger("SEC");
        private static string s_cachedCpuId = null;
        public static string GetCpuIDAsync()
        {
            if (s_cachedCpuId != null) return s_cachedCpuId;
            string cpuId = string.Empty;
            try
            {
                ManagementClass mc = new ManagementClass("Win32_Processor");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    cpuId = mo.Properties["ProcessorId"].Value.ToString();
                }
                moc = null;
                mc = null;
                s_cachedCpuId=cpuId;
                return cpuId;
            }
            catch(Exception e)
            {
                throw e;
            }
        }
        public static List<string> ReadSections()
        {
            return s_lineFileParser.GetSections(s_path);
        }
        public static void DeleteSection(string section)
        {
            s_lineFileParser.DeleteSection(s_path,section);
        }
        public static void Write(string section, string key, string value)
        {
            int i = 0;
            string strCpuID = GetCpuIDAsync();
            if (strCpuID == "Unknown") { throw new Exception("CPUID获取失败"); }
            string totalStr = Encrypt3Des(value, strCpuID);
            s_logger.Info(i++.ToString());
            s_lineFileParser.Write(s_path, key, totalStr, section);
            s_logger.Info(i++.ToString());
        }
        public static string Read(string section, string key)
        {
            string strCpuID = GetCpuIDAsync();
            if (strCpuID == "Unknown") { throw new Exception("CPUID获取失败"); }
            //            strCpuID = strCpuID.ToCharArray()[2].ToString() + strCpuID.ToCharArray()[4].ToString() + strCpuID.ToCharArray()[1].ToString() + strCpuID;
            string enStr = s_lineFileParser.Read(s_path, key, section);
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
            catch
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
            catch
            {
                return string.Empty;
            }
        }
    }
}
