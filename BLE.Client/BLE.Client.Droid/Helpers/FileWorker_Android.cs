using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BLE.Client.Helpers;

namespace BLE.Client.Droid.Helpers
{
    public class FileWorker_Android : IFileWorker
    {
        public Task DeleteAsync(string filename)
        {
            File.Delete(GetFilePath(filename));
            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(string filename)
        {
            string filepath = GetFilePath(filename);
            // существует ли файл
            bool exists = File.Exists(filepath);
            return Task<bool>.FromResult(exists);
        }

        public Task<IEnumerable<string>> GetFilesAsync()
        {
            IEnumerable<string> filenames = from filepath in Directory.EnumerateFiles(GetDocsPath())
                                            select Path.GetFileName(filepath);
            return Task<IEnumerable<string>>.FromResult(filenames);
        }

        public async Task<string> LoadTextAsync(string filename)
        {
            string filepath = GetFilePath(filename);
            using (StreamReader reader = File.OpenText(filepath))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public async Task SaveTextAsync(string filename, string text)
        {
            string filepath = GetFilePath(filename);
            using (StreamWriter writer = File.CreateText(filepath))
            {
                await writer.WriteAsync(text);
            }
        }

        public void SaveBytes(string filename, byte[] bytesToWrite)
        {
            if (filename != null && filename.Length > 0 && bytesToWrite != null)
            {
                string filepath = GetFilePath(filename);
                if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filepath));

                FileStream file = File.Create(filepath);

                file.Write(bytesToWrite, 0, bytesToWrite.Length);

                file.Close();
            }
        }

        string GetFilePath(string filename)
        {
            return Path.Combine(GetDocsPath(), filename);
        }

        // Directory MyDocuments
        string GetDocsPath()
        {
            //return Environment.GetFolderPath(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath);
            //return Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            return Android.OS.Environment.ExternalStorageDirectory.AbsolutePath + "/Android/data/com.ble.ticlient/files";
        }
    }
}
