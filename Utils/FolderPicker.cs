using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;


namespace LMC.Utils
{
    public class FolderPicker
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pv);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        private const uint BIF_RETURNONLYFSDIRS = 0x0001;
        private const uint BIF_NEWDIALOGSTYLE = 0x0040;

        public static string SelectFolder(Window owner, string title)
        {
            var bi = new BROWSEINFO
            {
                hwndOwner = new WindowInteropHelper(owner).Handle,
                lpszTitle = title,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE
            };

            IntPtr pidl = SHBrowseForFolder(ref bi);
            if (pidl == IntPtr.Zero)
                return null;

            try
            {
                var path = new StringBuilder(260);
                if (SHGetPathFromIDList(pidl, path))
                {
                    return path.ToString();
                }
                return null;
            }
            finally
            {
                CoTaskMemFree(pidl); // 使用 CoTaskMemFree 正确释放内存
            }
        }
    }
}