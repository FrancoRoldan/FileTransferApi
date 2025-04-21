using Data.Interfaces;
using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services.Login
{
    public class LoginAttemptService : ILoginAttemptService
    {
        private readonly ILoginAttemptRepository _repository;
        private const int MaxAttempts = 3;
        private const int LockoutMinutes = 5;
        private const int TimeWindowMinutes = 5;
        public LoginAttemptService(ILoginAttemptRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> IsLockedOutAsync(string email)
        {
            var attempt = await _repository.GetByEmailAsync(email);

            if (attempt == null) return false;

            if (attempt.LockoutEnd.HasValue)
            {
                if (DateTime.Now >= attempt.LockoutEnd)
                {
                    await _repository.ClearAttemptsAsync(email);
                    return false;
                }
                return true;
            }

            return false;
        }

        public async Task RecordAttemptAsync(string email, bool wasSuccessful)
        {
            if (wasSuccessful)
            {
                await _repository.ClearAttemptsAsync(email);
                return;
            }

            var attempt = await _repository.GetByEmailAsync(email);

            if (attempt == null)
            {
                attempt = new LoginAttempt
                {
                    Email = email,
                    Attempts = 1,
                    LastAttempt = DateTime.Now,
                    CreatedUser = "System",
                    CreatedAt = DateTime.Now
                };
                await _repository.AddAsync(attempt);
            }
            else
            {
                var timeSinceLastAttempt = DateTime.Now - attempt.LastAttempt;
                if (timeSinceLastAttempt.TotalMinutes > TimeWindowMinutes)
                {
                    attempt.Attempts = 1;
                }
                else
                {
                    attempt.Attempts++;
                }

                attempt.LastAttempt = DateTime.Now;
                attempt.UpdatedAt = DateTime.Now;
                attempt.UpdatedUser = "System";

                if (attempt.Attempts > MaxAttempts)
                {
                    attempt.LockoutEnd = DateTime.Now.AddMinutes(LockoutMinutes);
                }

                await _repository.UpdateAsync(attempt);
            }
        }
    }


}
