using iNKORE.UI.WPF.Modern.Controls;
using LMC.Minecraft;
using LMC.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Security.Cryptography;

using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LMC.Basic
{
    public class Secrets
    {
        private static string s_path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/DoNotSendThisToAnyone/请勿将此发送给他人/secrets.line";
        private static LineFileParser s_lineFileParser = new LineFileParser();
        private static Logger s_logger = new Logger("SEC");
        private static string s_cachedCpuId = null;
        private static string s_cachedBiosId = null;

        public static string GetWmiInfo(string c, string p)
        {
            string res = string.Empty;
            try
            {
                ManagementClass mc = new ManagementClass(c);
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    try
                    {
                        res = mo.Properties[p].Value.ToString();
                        break;
                    }
                    catch { }
                }
                moc = null;
                mc = null;
                return res;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static string GetDeviceCode()
        {
            s_logger.Info("正在获取设备标识符");
            string cpuId = string.IsNullOrEmpty(s_cachedCpuId) ? GetWmiInfo("Win32_Processor","ProcessorId") : s_cachedCpuId;
            string biosId = string.IsNullOrEmpty(s_cachedBiosId) ? GetWmiInfo("Win32_BIOS", "SerialNumber") + GetWmiInfo("Win32_BIOS", "ReleaseDate") + GetWmiInfo("Win32_BIOS", "SMBIOSBIOSVersion") : s_cachedBiosId;
            string hash = string.Empty;
            s_cachedCpuId = cpuId;
            s_cachedBiosId = biosId;
            var all = new ASCIIEncoding().GetBytes("CML" + cpuId /*+ macAddress*/ + biosId + "LMC");
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hashBytes = sha1.ComputeHash(all);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                hash = sb.ToString();
            }
            int i = 0;
            while(hash.Length < 16)
            {
                hash += hash[i];
                i++;
            }
            return hash.Substring(0,16);
        }

        public async static Task<string> Export(string cause)
        {
            s_logger.Info("正在导出隐私文件，原因:" + cause);
            string enKey = string.Empty;
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hashBytes = sha1.ComputeHash(new ASCIIEncoding().GetBytes(MainWindow.LauncherVersion));

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                enKey = sb.ToString().Substring(0,16);
            }
            var sections = ReadSections();
            string path = Directory.GetParent(s_path).FullName + $"/decrypted_secret.line";
            File.Create(path).Close();
            foreach (var section in sections)
            {
                if (!string.IsNullOrEmpty(section))
                {
                    var keys = ReadKeySet(section);
                    foreach (var key in keys)
                    {
                        var value = await Read(section, key);
                        s_lineFileParser.Write(path, key, EncryptAes(value, enKey), section);
                    }
                }
            }
            Directory.CreateDirectory("./LMC/temp/exp");
            Directory.CreateDirectory("./LMC/export");
            File.Create($"./LMC/temp/exp/mf.line").Close();
            s_lineFileParser.Write($"./LMC/temp/exp/mf.line", "fromVer", MainWindow.LauncherVersion, "base");
            string totalZip = $"{Directory.GetParent("./LMC/export/").FullName}/{new Random().Next(1000,9999)}.linesec";
            File.Delete("./LMC/temp/exp/scs.line");
            File.Move(path, $"./LMC/temp/exp/scs.line");
            ZipFile.CreateFromDirectory($"./LMC/temp/exp",totalZip);
            File.Delete("./LMC/temp/exp/scs.line");
            File.Delete($"./LMC/temp/exp/mf.line");
            Directory.Delete($"./LMC/temp/exp");
            return totalZip;
        }

        public static List<string> ReadKeySet(string section)
        {
            return s_lineFileParser.GetKeySet(s_path, section);
        }

        public static List<string> ReadSections()
        {
            return s_lineFileParser.GetSections(s_path);
        }
        public static void DeleteSection(string section)
        {
            s_lineFileParser.DeleteSection(s_path,section);
        }
        public static async Task Backup(string cause)
        {
            s_logger.Info("正在备份隐私文件，原因:" + cause);

            await Task.Run(() => {File.Delete(Directory.GetParent(s_path).FullName + "/secret_backup.line"); File.Copy(s_path, Directory.GetParent(s_path).FullName + "/secret_backup.line"); });
            s_lineFileParser.Write(Directory.GetParent(s_path).FullName + "/secret_backup.line", "fromVersion", MainWindow.LauncherVersion, "backup");
        }

        public static void Write(string section, string key, string value)
        {
            int i = 0;
            string strCpuID = GetDeviceCode();
            if (strCpuID == "Unknown") { throw new Exception("CPUID获取失败"); }
            string totalStr = EncryptAes(value, strCpuID);
            s_logger.Info(i++.ToString());
            s_lineFileParser.Write(s_path, key, totalStr, section);
            s_logger.Info(i++.ToString());
        }
        public async static Task<string> Read(string section, string key)
        {
            string strCpuID = GetDeviceCode();
            if (strCpuID == "Unknown") { throw new Exception("CPUID获取失败"); }
            //            strCpuID = strCpuID.ToCharArray()[2].ToString() + strCpuID.ToCharArray()[4].ToString() + strCpuID.ToCharArray()[1].ToString() + strCpuID;
            string enStr = s_lineFileParser.Read(s_path, key, section);
            if (!string.IsNullOrEmpty(enStr))
            {
                try
                {
                    string totalStr = DecryptAes(enStr, strCpuID);
                    return totalStr;
                }
                catch (Exception ex) {
                    s_logger.Warn($"无法解密隐私文件：\n{ex.Message}\n{ex.StackTrace}");
                    string content = $"在解密您的隐私文件（如账号）时出现了一些问题，这可能是由于您重新安装了系统或更换了设备所导致的，可以通过更换回原先的硬件并在 设置 -> 安全设置 -> 导出隐私文件 中导出隐私文件，并在更换至新硬件后"
                        + $"在 设置 -> 安全设置 -> 导入隐私文件 中导入。是否要将目前未解密（也无法解密）的隐私文件备份？\n（解密时报错）\n{ex.Message}\n{ex.StackTrace}";
                    string title = "提示";
                    string backup = "备份";
                    string close = "不备份";
                    var res = await MainWindow.ShowDialog(close, content, title, ContentDialogButton.Primary, backup);
                    if (res == ContentDialogResult.Primary)
                    {
                        await Backup("无法解密");
                        System.Diagnostics.Process.Start("explorer", "/select," + Directory.GetParent(s_path).FullName + "\\secret_backup.line");
                    }
                }
            }
            return null;
        }
        public static string EncryptAes(string aStrString, string aStrKey)
        {

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(aStrKey);
                aesAlg.IV = new byte[16]; 

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(aStrString);
                        }
                    }

                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        public static string DecryptAes(string aStrString, string aStrKey)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Encoding.UTF8.GetBytes(aStrKey);
                aesAlg.IV = new byte[16];

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(aStrString)))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
