using Data.Context;
using Data.Interfaces;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Repositories
{
    public class FileTransferTaskRepository : Repository<FileTransferTask>, IFileTransferTaskRepository
    {
        public FileTransferTaskRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<FileTransferTask>> GetPaginatedAsync(int pageIndex, int pageSize, string searchTerm = "")
        {
            IQueryable<FileTransferTask> query = _dbSet;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();

                query = query.Where(ft =>
                    ft.Name.ToLower().Contains(searchTerm) ||
                    ft.Description.ToLower().Contains(searchTerm) ||
                    ft.SourceFolder.ToLower().Contains(searchTerm) ||
                    ft.DestinationFolder.ToLower().Contains(searchTerm) ||
                    (ft.FilePattern != null && ft.FilePattern.ToLower().Contains(searchTerm)) ||
                    (ft.CronExpression != null && ft.CronExpression.Contains(searchTerm)) 
                );
            }

            return await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAsync(string searchTerm = "")
        {
            IQueryable<FileTransferTask> query = _dbSet;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();

                query = query.Where(ft =>
                    ft.Name.ToLower().Contains(searchTerm) ||
                    ft.Description.ToLower().Contains(searchTerm) ||
                    ft.SourceFolder.ToLower().Contains(searchTerm) ||
                    ft.DestinationFolder.ToLower().Contains(searchTerm) ||
                    (ft.FilePattern != null && ft.FilePattern.ToLower().Contains(searchTerm)) ||
                    (ft.CronExpression != null && ft.CronExpression.Contains(searchTerm))
                );
            }

            return await query.CountAsync();
        }
    }
}
