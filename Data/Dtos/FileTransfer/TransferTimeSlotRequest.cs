using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Dtos.FileTransfer
{
    public class TransferTimeSlotRequest
    {
        public int? Id { get; set; }
        public int? FileTransferTaskId { get; set; }
        public string ExecutionTime { get; set; } = "00:00:00";
    }
}
