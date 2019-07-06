using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLE.Client.Helpers
{
    public interface IFileWorker
    {
        Task DeleteAsync(string filename);
        Task<bool> ExistsAsync(string filename);
        Task<IEnumerable<string>> GetFilesAsync();
        Task<string> LoadTextAsync(string filename);
        Task SaveTextAsync(string filename, string text);
    }
}
