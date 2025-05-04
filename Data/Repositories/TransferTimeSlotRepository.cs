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
    public class TransferTimeSlotRepository : Repository<TransferTimeSlot>, ITransferTimeSlotRepository
    {
        public TransferTimeSlotRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<TransferTimeSlot>> GetAllByTaskId(int idTask)
        {
            return await _dbSet.Where( t => t.FileTransferTaskId == idTask).ToListAsync();
        }
    }
}
