using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models
{
    public enum TransferScheduleType
    {
        OneTime,       // Transferencia única
        Daily,         // Todos los días a las horas especificadas
        Weekly,        // Días específicos de la semana
        Monthly,       // Días específicos del mes
        Custom         // Usando expresión cron personalizada
    }
}
