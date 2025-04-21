using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class TransferExecution : BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public int FileTransferTaskId { get; set; }

        [ForeignKey("FileTransferTaskId")]
        public FileTransferTask FileTransferTask { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = ""; // En progreso, Completado, Error
        public string? ErrorMessage { get; set; }
        public int FilesTransferred { get; set; } = 0;
        public int FilesSkipped { get; set; } = 0;
        public int ErrorCount { get; set; } = 0;
    }
}
