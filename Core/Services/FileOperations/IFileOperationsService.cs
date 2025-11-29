
using Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Services.FileOperations
{
    public interface IFileOperationsService
    {
        Task<IEnumerable<string>> GetFilesFromServerAsync(ServerCredential credential, string folder, ProcessedPattern processedPattern, bool includeSubfolders = false);
        Task<TransferredFile> TransferFileAsync(int executionId, ServerCredential source, ServerCredential destination, string sourceFilePath, string sourceBaseFolder, string destinationBaseFolder, bool createSubfolders, bool deleteSource);
        Task DeleteFoldersRecursivelyAsync(ServerCredential credential, string baseFolder, HashSet<string> processedFolders);
        Task DeleteNetworkFoldersRecursivelyAsync(string baseFolder, HashSet<string> processedFolders);
    }
}
