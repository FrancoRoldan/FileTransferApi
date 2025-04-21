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
    public class LoginAttemptRepository : Repository<LoginAttempt>, ILoginAttemptRepository
    {
        public LoginAttemptRepository(AppDbContext context) : base(context) { }

        public async Task<LoginAttempt?> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<bool> IsLockedOutAsync(string email)
        {
            var attempt = await GetByEmailAsync(email);
            return attempt?.IsLockedOut ?? false;
        }

        public async Task ClearAttemptsAsync(string email)
        {
            var attempt = await GetByEmailAsync(email);
            if (attempt != null)
            {
                _dbSet.Remove(attempt);
                await _context.SaveChangesAsync();
            }
        }
    }
}
