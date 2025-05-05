using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Interfaces
{
    public interface IFileTransferTaskRepository : IRepository<FileTransferTask>
    {
        Task<IEnumerable<FileTransferTask>> GetPaginatedAsync(int pageIndex, int pageSize, string searchTerm);
        Task<int> CountAsync(string searchTerm);
    }
}
