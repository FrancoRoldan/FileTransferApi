using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class TransferredFile : BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public int TransferExecutionId { get; set; }

        [ForeignKey("TransferExecutionId")]
        public TransferExecution TransferExecution { get; set; } = null!;

        public string FileName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string DestinationPath { get; set; } = "";
        public long FileSize { get; set; }
        public bool TransferSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
