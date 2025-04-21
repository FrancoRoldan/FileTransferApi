using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Dtos.FileTransfer
{
    public class FileTransferTaskResponse
    {
        public int? Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int SourceCredentialId { get; set; }
        public int DestinationCredentialId { get; set; }
        public string SourceFolder { get; set; } = "";
        public string DestinationFolder { get; set; } = "";
        public string? FilePattern { get; set; }
        public bool CreateSubfolders { get; set; } = false;
        public bool DeleteSourceAfterTransfer { get; set; } = false;
        public bool CopySubfolders { get; set; }
        public bool DeleteSourceFolderAfterTransfer { get; set; }

        // Configuración de programación
        public TransferScheduleType ScheduleType { get; set; } = TransferScheduleType.OneTime;

        // Para transferencias únicas
        public DateTime? OneTimeExecutionDate { get; set; }

        // Para transferencias periódicas
        public bool IsMonday { get; set; } = false;
        public bool IsTuesday { get; set; } = false;
        public bool IsWednesday { get; set; } = false;
        public bool IsThursday { get; set; } = false;
        public bool IsFriday { get; set; } = false;
        public bool IsSaturday { get; set; } = false;
        public bool IsSunday { get; set; } = false;

        // Hora(s) de ejecución para transferencias programadas
        public List<TransferTimeSlot?> ExecutionTimes { get; set; } = new List<TransferTimeSlot?>();

        // Opciones avanzadas de programación
        public string? CronExpression { get; set; } // Para programaciones más complejas

        public bool IsActive { get; set; } = true;
    }
}
