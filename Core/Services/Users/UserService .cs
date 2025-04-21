using Core.Security;
using Core.Services.Login;
using Data.Dtos.Users;
using Data.Interfaces;
using Data.Models;
using Mapster;
using System.ComponentModel.DataAnnotations;

namespace Core.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtService _jwtService;
        private readonly ILoginAttemptService _loginAttemptService;

        public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher, IJwtService jwtService, ILoginAttemptService loginAttemptService)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _loginAttemptService = loginAttemptService;
        }

        public async Task<(User? user, string token)> AuthenticateAsync(string email, string password)
        {
            if (await _loginAttemptService.IsLockedOutAsync(email))
            {
                throw new ValidationException("La cuenta está temporalmente bloqueada. Por favor, intente más tarde.");
            }

            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null) return (null, "");

            var isAuthenticated = user != null && _passwordHasher.VerifyPassword(password, user.Password);

            await _loginAttemptService.RecordAttemptAsync(email, isAuthenticated);

            if (!isAuthenticated)
            {
                if (await _loginAttemptService.IsLockedOutAsync(email))
                {
                    throw new ValidationException("Ha excedido el número máximo de intentos. La cuenta ha sido bloqueada por 5 minutos.");
                }

                return (null, "");
            }

            var token = _jwtService.GenerateToken(user!);

            return (user,token);
        }

        public async Task<User> RegisterAsync(AddUserRequest user, string password)
        {
            User userCreate = user.Adapt<User>();
            User? existUserMail = await _userRepository.GetByEmailAsync(userCreate.Email);
            if (existUserMail != null) throw new ValidationException("Ya existe el correo registrado.");
            userCreate.Password = _passwordHasher.HashPassword(password);
               
            return await _userRepository.AddAsync(userCreate);
        }

        public async Task<List<GetUserResponse>> GetAllAsync()
        {
            IEnumerable<User> users =  await _userRepository.GetAllAsync();
            return users.Adapt<List<GetUserResponse>>();
        }
    }
}
