using Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Context
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }


        public DbSet<User> Users { get; set; }
        public DbSet<LoginAttempt> Attempts { get; set; }
        public DbSet<ServerCredential> ServerCredentials { get; set; }
        public DbSet<FileTransferTask> FileTransferTasks { get; set; }
        public DbSet<TransferExecution> TransferExecutions { get; set; }
        public DbSet<TransferredFile> TransferredFiles { get; set; }
        public DbSet<TransferTimeSlot> TransferTimeSlots { get; set; }

    }
}
