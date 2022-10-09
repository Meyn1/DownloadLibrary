using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Text;

namespace DownloaderLibrary.Utilities
{
    internal class IOManager
    {
        /// <summary>
        /// Converts Bytes to Megabytes
        /// InputBytes : 1.048.576
        /// </summary>
        /// <param name="bytes">Bytes to convert</param>
        /// <returns>Convertet megabytes value as double</returns>
        public static double BytesToMegabytes(long bytes) => bytes / 1048576;

        /// <summary>
        /// Removes all invalid Characters for a filename out of a string
        /// </summary>
        /// <param name="name">input filename</param>
        /// <returns>Clreared filename</returns>
        public static string RemoveInvalidFileNameChars(string name)
        {
            StringBuilder fileBuilder = new(name);
            foreach (char c in Path.GetInvalidFileNameChars())
                fileBuilder.Replace(c.ToString(), string.Empty);
            return fileBuilder.ToString();
        }
        /// <summary>
        /// Gets the default extension aof an mimeType
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns>A Extension as string</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="SecurityException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static string GetDefaultExtension(string mimeType)
        {
            string result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                RegistryKey? key;
                object? value;

                key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType, false);
                value = key?.GetValue("Extension", null);
                result = value?.ToString() ?? string.Empty;

            }
            else throw new NotSupportedException("GetDefaultExtension Method");
            return result;
        }


        /// <summary>
        /// Gets a value that indicates whether <paramref name="path"/>
        /// is a valid path.
        /// </summary>
        /// <returns>Returns <c>true</c> if <paramref name="path"/> is a
        /// valid path; <c>false</c> otherwise. Also returns <c>false</c> if
        /// the caller does not have the required permissions to access
        /// <paramref name="path"/>.
        /// </returns>
        /// <seealso cref="Path.GetFullPath(string)"/>
        /// <seealso cref="TryGetFullPath"/>
        public static bool IsValidPath(string path) => TryGetFullPath(path, out _);


        /// <summary>
        /// Returns the absolute path for the specified path string. A return
        /// value indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="path">The file or directory for which to obtain absolute
        /// path information.
        /// </param>
        /// <param name="result">When this method returns, contains the absolute
        /// path representation of <paramref name="path"/>, if the conversion
        /// succeeded, or <see cref="String.Empty"/> if the conversion failed.
        /// The conversion fails if <paramref name="path"/> is null or
        /// <see cref="String.Empty"/>, or is not of the correct format. This
        /// parameter is passed uninitialized; any value originally supplied
        /// in <paramref name="result"/> will be overwritten.
        /// </param>
        /// <returns><c>true</c> if <paramref name="path"/> was converted
        /// to an absolute path successfully; otherwise, false.
        /// </returns>
        /// <seealso cref="Path.GetFullPath(string)"/>
        /// <seealso cref="IsValidPath"/>
        public static bool TryGetFullPath(string path, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || path[1] != ':')
                return false;
            bool status = false;

            try
            {
                result = Path.GetFullPath(path);
                status = true;
            }
            catch (ArgumentException) { }
            catch (SecurityException) { }
            catch (NotSupportedException) { }
            catch (PathTooLongException) { }

            return status;
        }
        /// <summary>
        /// Gets the Home or Desktop path
        /// </summary>
        /// <returns>Returns Path to Desktop</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="SecurityException"></exception>
        public static string? GetHomePath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                return Environment.GetEnvironmentVariable("HOME");
            return Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
        }
        /// <summary>
        /// Gets the download folder path
        /// </summary>
        /// <returns>A path to the download folder</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="IOException"></exception>
        /// <exception cref="SecurityException"></exception>
        public static string? GetDownloadFolderPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string? homePath = GetHomePath();
                if (string.IsNullOrEmpty(homePath))
                    return null;
                return Path.Combine(homePath, "Downloads");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                return Convert.ToString(
                Registry.GetValue(
                     @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"
                    , "{374DE290-123F-4565-9164-39C4925E467B}"
                    , string.Empty));
            }
            else return null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName,
    FileSystemRights dwDesiredAccess, FileShare dwShareMode, IntPtr
    securityAttrs, FileMode dwCreationDisposition, FileOptions
    dwFlagsAndAttributes, IntPtr hTemplateFile);

        private const int ERROR_SHARING_VIOLATION = 32;

        /// <summary>
        /// A Method that is used to indicate if a file is used and locked
        /// If it does not exists it creates one.
        /// Only available to Windows
        /// </summary>
        /// <param name="fileName">Path to file</param>
        /// <returns>A <see cref="bool"/> that indicates if the file is in use</returns>
        /// <exception cref="NotSupportedException"></exception>
        public static bool IsFileInUse(string fileName)
        {
            bool inUse = false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SafeFileHandle fileHandle =
            CreateFile(fileName, FileSystemRights.Modify,
                 FileShare.Write, IntPtr.Zero,
                 FileMode.OpenOrCreate, FileOptions.None, IntPtr.Zero);

                if (fileHandle.IsInvalid)
                    if (Marshal.GetLastWin32Error() == ERROR_SHARING_VIOLATION)
                        inUse = true;

                fileHandle.Close();
            }
            else throw new NotSupportedException("IsFileInUse Method");

            return inUse;
        }
    }
}


