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

        public async Task<IEnumerable<ServerCredential>> GetPaginatedAsync(int pageIndex, int pageSize)
        {
            return await _dbSet
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();
        }

        public async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }
    }
}
