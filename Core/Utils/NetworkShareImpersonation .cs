using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utils
{
    public class NetworkConnection : IDisposable
    {
        private string _networkName;
        private bool _disposed = false;

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource netResource,
            string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags,
            bool force);

        public NetworkConnection(string networkName, string userName, string password)
        {
            _networkName = networkName;

            var netResource = new NetResource()
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = networkName
            };

            var result = WNetAddConnection2(
                netResource,
                password,
                userName,
                0);

            if (result != 0)
            {
                throw new Win32Exception(result, $"Error connecting to network resource: {networkName}");
            }
        }

        ~NetworkConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                WNetCancelConnection2(_networkName, 0, true);
                _disposed = true;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public class NetResource
        {
            public ResourceScope Scope;
            public ResourceType ResourceType;
            public ResourceDisplaytype DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        public enum ResourceScope : int
        {
            Connected = 1,
            GlobalNetwork = 2,
            Remembered = 3,
            Recent = 4,
            Context = 5
        }

        public enum ResourceType : int
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8
        }

        public enum ResourceDisplaytype : int
        {
            Generic = 0,
            Domain = 1,
            Server = 2,
            Share = 3,
            File = 4,
            Group = 5,
            Network = 6,
            Root = 7,
            Shareadmin = 8,
            Directory = 9,
            Tree = 10,
            Ndscontainer = 11
        }
    }
}
