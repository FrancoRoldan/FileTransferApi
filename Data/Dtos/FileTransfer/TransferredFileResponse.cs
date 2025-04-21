using Data.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Dtos.FileTransfer
{
    public class TransferredFileResponse
    {
        public int Id { get; set; }
        public int TransferExecutionId { get; set; }
        public string FileName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string DestinationPath { get; set; } = "";
        public long FileSize { get; set; }
        public bool TransferSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
