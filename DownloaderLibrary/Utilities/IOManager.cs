using DownloaderLibrary.Web;
using DownloaderLibrary.Web.Request;
using Microsoft.Win32;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace DownloaderLibrary.Utilities
{
    internal static class IOManager
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

        /// <summary>
        /// Moves a file to another destination. If it existst it will be overwritten
        /// </summary>
        /// <param name="path">Source file path</param>
        /// <param name="destination">Destination file path</param>
        public static void Move(string path, string destination) => File.Move(path, destination, true);

        /// <summary>
        /// Creates a file or clears an existing one
        /// </summary>
        /// <param name="path">Path to the file that should be created</param>
        public static void Create(string path) => File.Create(path).Close();

        /// <summary>
        /// Gets the guessed FileName from a given url and response
        /// </summary>
        /// <param name="headers">ContentHeaders of the request</param>
        /// <param name="preSetFilename">Name that was pre set</param>
        /// <param name="uri">Given Uri/Url</param>
        /// <returns></returns>
        public static string GetFileName(HttpContentHeaders headers, string preSetFilename, Uri uri)
        {
            string fileName = preSetFilename;
            if (fileName == string.Empty)
            {
                fileName = headers.ContentDisposition?.FileNameStar ?? string.Empty;
                if (fileName == string.Empty)
                    fileName = RemoveInvalidFileNameChars(uri.Segments.Last() ?? string.Empty);
                if (fileName == string.Empty)
                    fileName = RemoveInvalidFileNameChars(Path.GetFileName(uri.AbsoluteUri) ?? string.Empty);
                if (fileName == string.Empty)
                    fileName = "requested_download";
            }

            if (!fileName.Contains('.'))
            {
                string ext = string.Empty;
                if (headers.ContentType?.MediaType != null)
                    ext = GetDefaultExtension(headers.ContentType.MediaType);
                if (ext == string.Empty)
                    ext = Path.GetExtension(uri.AbsoluteUri);
                fileName += ext;
            }
            return fileName;
        }

        /// <summary>
        /// Merges all chunked parts of a file into one big file.
        /// </summary>
        /// <returns>A awaitable Task</returns>
        public static async Task MergeChunks<T>(ChunkInfo<T> chunkInfo)
        {
            if (chunkInfo.IsCopying)
                return;
            chunkInfo.IsCopying = true;

            Chunk[] chunks = chunkInfo.Chunks;
            LoadRequest[] requests = chunkInfo.Requests;

            //Check if the fist part was downloaded
            if (!chunks[0].IsFinished)
            {
                chunkInfo.IsCopying = false;
                return;
            }

            //FileStream to merge the chunked files
            FileStream? outputStream = null;
            try
            {
                outputStream = new(requests[0].Destination, FileMode.Append);
                for (int i = 0; i < chunks.Length; i++)
                {
                    if (!chunks[i].IsFinished)
                        break;
                    if (chunks[i].IsCopied)
                        continue;
                    string path = requests[i].TmpDestination;
                    if (!File.Exists(path))
                        break;

                    FileStream inputStream = File.OpenRead(path);
                    await inputStream.CopyToAsync(outputStream);
                    await inputStream.FlushAsync();
                    await inputStream.DisposeAsync();
                    File.Delete(path);

                    chunks[i].IsCopied = true;
                }
            }
            catch (Exception) { }
            finally
            {
                if (outputStream != null)
                {
                    await outputStream.FlushAsync();
                    await outputStream.DisposeAsync();
                }
                chunkInfo.IsCopying = false;
            }
        }
    }
}


