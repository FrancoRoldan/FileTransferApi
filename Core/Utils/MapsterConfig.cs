using Data.Dtos.FileTransfer;
using Data.Models;
using Mapster;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utils
{
    public class MapsterConfig
    {
        public static void Configure()
        {

            TypeAdapterConfig<FileTransferTask, FileTransferTask>
                .NewConfig()
                .Ignore(dest => dest.ExecutionTimes);
        }
    }
}
