using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services.Login
{
    public interface ILoginAttemptService
    {
        Task<bool> IsLockedOutAsync(string email);
        Task RecordAttemptAsync(string email, bool wasSuccessful);
    }
}
