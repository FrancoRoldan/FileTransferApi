using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class LoginAttempt: BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public required string Email { get; set; }
        public int Attempts { get; set; }
        public DateTime LastAttempt { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public bool IsLockedOut => LockoutEnd.HasValue && DateTime.Now < LockoutEnd;
    }
}
