using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Funcy.Data;

public static class DatabaseConnectionFactory
{
    public static string CreateConnectionString(IConfiguration configuration, string? subDirectory = null)
    {
        var baseConnectionString = configuration.GetConnectionString("DefaultConnection");
        var builder = new SqliteConnectionStringBuilder(baseConnectionString);

        var fileName = Path.GetFileName(builder.DataSource);
        var baseDirectory = GetDataDirectory(subDirectory);
        Directory.CreateDirectory(baseDirectory);

        builder.DataSource = Path.Combine(baseDirectory, fileName);
        return builder.ToString();
    }

    /// <summary>The per-user data directory. <paramref name="subDirectory"/> nests it one level
    /// deeper, which demo mode uses to keep its database, settings and logs away from the real
    /// ones.</summary>
    public static string GetDataDirectory(string? subDirectory = null)
    {
        var root = GetRootDataDirectory();
        return string.IsNullOrEmpty(subDirectory) ? root : Path.Combine(root, subDirectory);
    }

    private static string GetRootDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                // Fallback if LocalApplicationData is not available
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(root))
                {
                    root = Environment.GetEnvironmentVariable("USERPROFILE") 
                           ?? Environment.GetEnvironmentVariable("HOME")
                           ?? Path.GetTempPath();
                }
                return Path.Combine(root, ".funcy");
            }
            return Path.Combine(root, "Funcy");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(root))
                {
                    root = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
                }
                return Path.Combine(root, ".funcy");
            }
            return Path.Combine(root, "Funcy");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
            return Path.Combine(xdgDataHome, "funcy");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
        }
        return Path.Combine(home, ".local", "share", "funcy");
    }
}
