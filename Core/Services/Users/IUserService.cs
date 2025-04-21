using Data.Dtos.Users;
using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services
{
    public interface IUserService
    {
        Task<(User? user, string token)> AuthenticateAsync(string email, string password);
        Task<User> RegisterAsync(AddUserRequest user, string password);
        Task<List<GetUserResponse>> GetAllAsync();
    }
}
