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
    public class ServerCredentialRepository : Repository<ServerCredential>, IServerCredentialRepository
    {
        public ServerCredentialRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<ServerCredential>> GetPaginatedAsync(int pageIndex, int pageSize, string searchTerm)
        {
            IQueryable<ServerCredential> query = _dbSet;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(searchTerm) ||
                    c.ServerType.ToLower().Contains(searchTerm) ||
                    c.Host.ToLower().Contains(searchTerm) ||
                    c.Port.ToString().Contains(searchTerm) ||
                    (c.Username != null && c.Username.ToLower().Contains(searchTerm))
                );
            }

            return await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        public async Task<int> CountAsync(string searchTerm = "")
        {
            IQueryable<ServerCredential> query = _dbSet;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(searchTerm) ||
                    c.ServerType.ToLower().Contains(searchTerm) ||
                    c.Host.ToLower().Contains(searchTerm) ||
                    c.Port.ToString().Contains(searchTerm) ||
                    (c.Username != null && c.Username.ToLower().Contains(searchTerm))
                );
            }

            return await query.CountAsync();
        }
    }
}
