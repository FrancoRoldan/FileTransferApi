using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Interfaces
{
    public interface ILoginAttemptRepository : IRepository<LoginAttempt>
    {
        Task<LoginAttempt?> GetByEmailAsync(string email);
        Task<bool> IsLockedOutAsync(string email);
        Task ClearAttemptsAsync(string email);
    }
}
