using Core.Services;
using Data.Context;
using Data.Dtos.Users;
using Data.Models;
using Mapster;

namespace Core.Helpers
{
    public  class DatabaseSeeder
    {
        public static void SeedAdminUser(AppDbContext context, IUserService userService)
        {
            if (!context.Users.Any())
            {
                var admin = new User
                {
                    Email = "test@test.com",
                    Nombre = "Test",
                    CreatedUser = "system"
                };

                userService.RegisterAsync(admin.Adapt<AddUserRequest>(), "123456").Wait(); 
            }
        }
    }
}
