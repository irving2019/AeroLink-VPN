using System;
using System.Diagnostics;
using System.Linq;
using System.Net;


namespace AeroLink.Services
{
    public static class VpnCleanup
    {
        public static void Execute()
        {
            try
            {
                var processes = Process.GetProcesses().Where(p =>
                p.ProcessName.StartsWith("xray", StringComparison.OrdinalIgnoreCase) ||
                p.ProcessName.StartsWith("amneziawg", StringComparison.OrdinalIgnoreCase));

                foreach (var p in processes)
                {
                    try
                    {
                        p.Kill(true);
                        p.WaitForExit(1000);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {

            }
        }
    }
}
