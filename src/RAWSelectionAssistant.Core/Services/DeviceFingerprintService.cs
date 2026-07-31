using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace RAWSelectionAssistant.Core.Services;

public sealed class DeviceFingerprintService : IDeviceFingerprintService
{
    public string DeviceName => Environment.MachineName;

    public string GetAnonymousFingerprint()
    {
        var machineGuid = ReadMachineGuid();
        var input = $"KitaoPhotoSelector|{machineGuid}|{Environment.MachineName}|{Environment.Is64BitOperatingSystem}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static string ReadMachineGuid()
    {
        if (!OperatingSystem.IsWindows()) return Environment.MachineName;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
            return key?.GetValue("MachineGuid") as string ?? Environment.MachineName;
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
