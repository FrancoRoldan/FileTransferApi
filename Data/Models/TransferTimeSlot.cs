using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class TransferTimeSlot : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int FileTransferTaskId { get; set; }

        [ForeignKey("FileTransferTaskId")]
        public FileTransferTask FileTransferTask { get; set; } = null!;

        // Hora del día para ejecutar la transferencia (HH:mm)
        public TimeSpan ExecutionTime { get; set; }
    }
}
