using Chatex.Data;
using Chatex.Data.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chatex.Core.Services
{
    public class UserService(DataContext context)
    {
        private readonly PasswordHasher<string> hasher = new();

        public User Create(string name, string email, string password)
        {            
            email = email.Trim().ToLower();
            var user = context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
                return user;
            user = new User
            {
                Id = Guid.CreateVersion7(),
                Name = name,
                Email = email,
                PasswordHash = hasher.HashPassword(email, password),
                Created = DateTime.UtcNow
            };
            context.Users.Add(user);
            context.SaveChanges();
            return user;
        }

        public User? ValidateCredentials(string email, string password)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == email);            
            if (user == null)
                return null;
            var res = hasher.VerifyHashedPassword(user.Email, user.PasswordHash, password);
            return res == PasswordVerificationResult.Success ? user : null;
        }
    }
}
