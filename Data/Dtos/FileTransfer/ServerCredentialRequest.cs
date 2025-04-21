using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Dtos.FileTransfer
{
    public class ServerCredentialRequest
    {
        public string Name { get; set; } = "";
        public string ServerType { get; set; } = ""; // FTP, SFTP, etc.
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Username { get; set; } = "";
        public string EncryptedPassword { get; set; } = ""; // Almacenar contraseña cifrada
        public string? PrivateKeyPath { get; set; } // Para conexiones SFTP con clave privada
        public bool IsActive { get; set; } = true;
    }
}
