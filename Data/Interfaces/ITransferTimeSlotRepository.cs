using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Interfaces
{
    public interface ITransferTimeSlotRepository : IRepository<TransferTimeSlot>
    {
        Task<IEnumerable<TransferTimeSlot>> GetAllByTaskId(int idTask);
    }
}
