using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public class FileTransferTask : BaseEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        public int SourceCredentialId { get; set; }
        public int DestinationCredentialId { get; set; }

        [ForeignKey("SourceCredentialId")]
        public ServerCredential SourceCredential { get; set; } = null!;

        [ForeignKey("DestinationCredentialId")]
        public ServerCredential DestinationCredential { get; set; } = null!;

        public string SourceFolder { get; set; } = "";
        public string DestinationFolder { get; set; } = "";
        public string? FilePattern { get; set; } // Expresión regular para filtrar archivos
        public bool CreateSubfolders { get; set; } = false;
        public bool CopySubfolders { get; set; } = false;
        public bool DeleteSourceFolderAfterTransfer { get; set; } = false;

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
        public List<TransferTimeSlot> ExecutionTimes { get; set; } = new List<TransferTimeSlot>();

        // Opciones avanzadas de programación
        public string? CronExpression { get; set; } // Para programaciones más complejas

        public bool IsActive { get; set; } = true;
    }
}
