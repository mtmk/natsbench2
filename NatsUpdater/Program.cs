using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Linq;
using System.IO.Compression;
using System.Net;

class Program
{
    static async Task Main(string[] args)
    {
        string targetVersion = null;
        bool showHelp = false;
        bool listVersions = false;
        bool doUpdate = false;
        bool doUse = false;

        // Parse command-line arguments
        if (args.Length == 0)
        {
            // No arguments, show help
            ShowHelp();
            return;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--list":
                case "-l":
                    listVersions = true;
                    break;
                case "--update":
                case "-u":
                    doUpdate = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        targetVersion = args[++i];
                    }
                    break;
                case "--use":
                    doUse = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        targetVersion = args[++i];
                    }
                    else
                    {
                        Console.WriteLine("Error: --use requires a version argument.");
                        return;
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown argument: {arg}");
                    return;
            }
        }

        if (showHelp)
        {
            ShowHelp();
            return;
        }

        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string tokenFilePath = Path.Combine(homeDirectory, ".keys", "github_token.txt");

        if (!File.Exists(tokenFilePath))
        {
            Console.WriteLine($"GitHub token file not found at {tokenFilePath}");
            return;
        }

        string token = File.ReadAllText(tokenFilePath).Trim();

        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NatsUpdater", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);

            if (listVersions)
            {
                await ListLatestVersions(httpClient);
                return;
            }

            if (doUpdate)
            {
                if (string.IsNullOrEmpty(targetVersion))
                {
                    await UpdateToLatestVersion(httpClient, homeDirectory);
                }
                else
                {
                    await UpdateToVersion(httpClient, targetVersion, homeDirectory);
                }
                return;
            }

            if (doUse)
            {
                if (string.IsNullOrEmpty(targetVersion))
                {
                    Console.WriteLine("Error: --use requires a version argument.");
                    return;
                }

                await UseCachedVersion(targetVersion, homeDirectory);
                return;
            }

            // If no recognized operation was performed, show help
            ShowHelp();
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  nats-updater [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h                   Show this help message.");
        Console.WriteLine("  --list, -l                   List latest 10 available versions.");
        Console.WriteLine("  --update, -u [version]       Update to the latest version or to the specified version.");
        Console.WriteLine("  --use <version>              Use a previously downloaded version as the default nats-server.exe.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  nats-updater --update                Update to the latest version.");
        Console.WriteLine("  nats-updater --update v2.10.18       Update to a specific version.");
        Console.WriteLine("  nats-updater --use v2.10.18          Use a cached version as the default.");
        Console.WriteLine("  nats-updater --list                  List latest 10 available versions.");
    }

    static async Task ListLatestVersions(HttpClient httpClient)
    {
        var releases = await GetReleases(httpClient, perPage: 10);
        if (releases != null && releases.Count > 0)
        {
            Console.WriteLine("Latest 10 available versions:");
            foreach (var release in releases)
            {
                Console.WriteLine($"- {release["tag_name"]?.GetValue<string>()}");
            }
        }
        else
        {
            Console.WriteLine("No releases found.");
        }
    }

    static async Task UpdateToLatestVersion(HttpClient httpClient, string homeDirectory)
    {
        var releases = await GetReleases(httpClient, perPage: 1);
        if (releases != null && releases.Count > 0)
        {
            var latestRelease = releases[0];
            await DownloadAndUpdateRelease(httpClient, latestRelease, homeDirectory);
        }
        else
        {
            Console.WriteLine("No releases found.");
        }
    }

    static async Task UpdateToVersion(HttpClient httpClient, string version, string homeDirectory)
    {
        var release = await GetReleaseByTagName(httpClient, version);
        if (release == null)
        {
            Console.WriteLine($"Release with version {version} not found.");
            return;
        }

        await DownloadAndUpdateRelease(httpClient, release, homeDirectory);
    }

    static async Task<JsonArray> GetReleases(HttpClient httpClient, int perPage = 10)
    {
        string url = $"https://api.github.com/repos/nats-io/nats-server/releases?per_page={perPage}";

        var response = await httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("Unauthorized access. Check your GitHub token.");
            return null;
        }
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        var jsonNode = JsonNode.Parse(content);

        return jsonNode as JsonArray;
    }

    static async Task<JsonNode> GetReleaseByTagName(HttpClient httpClient, string tagName)
    {
        string url = $"https://api.github.com/repos/nats-io/nats-server/releases/tags/{tagName}";

        var response = await httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("Unauthorized access. Check your GitHub token.");
            return null;
        }

        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        var jsonNode = JsonNode.Parse(content);

        return jsonNode;
    }

    static async Task DownloadAndUpdateRelease(HttpClient httpClient, JsonNode release, string homeDirectory)
    {
        string version = release["tag_name"]?.GetValue<string>();
        Console.WriteLine($"Updating to version {version}");

        // Define bin directory in home directory
        string binDirectory = Path.Combine(homeDirectory, "bin");
        if (!Directory.Exists(binDirectory))
        {
            Directory.CreateDirectory(binDirectory);
        }

        // Define the path for the versioned executable in bin directory
        string versionedExeName = $"nats-server-{version}.exe";
        string versionedExePath = Path.Combine(binDirectory, versionedExeName);

        // Check if the versioned executable already exists
        if (File.Exists(versionedExePath))
        {
            Console.WriteLine($"Found cached version {version} in {binDirectory}");
            // Copy the versioned exe to the current directory as nats-server.exe
            string destinationPath = Path.Combine(Directory.GetCurrentDirectory(), "nats-server.exe");
            File.Copy(versionedExePath, destinationPath, true);
            Console.WriteLine($"nats-server.exe updated to version {version} at {destinationPath}");
            return;
        }

        var assets = release["assets"] as JsonArray;

        if (assets == null)
        {
            Console.WriteLine("No assets found in the release.");
            return;
        }

        JsonNode asset = null;

        foreach (var a in assets)
        {
            string name = a["name"]?.GetValue<string>();
            if (name != null && name.Contains("windows") && name.Contains("amd64") && name.EndsWith(".zip"))
            {
                asset = a;
                break;
            }
        }

        if (asset == null)
        {
            Console.WriteLine("No suitable asset found for Windows amd64.");
            return;
        }

        string downloadUrl = asset["browser_download_url"]?.GetValue<string>();

        if (string.IsNullOrEmpty(downloadUrl))
        {
            Console.WriteLine("Download URL not found for the asset.");
            return;
        }

        Console.WriteLine($"Downloading {downloadUrl}");

        var assetResponse = await httpClient.GetAsync(downloadUrl);
        assetResponse.EnsureSuccessStatusCode();

        byte[] assetData = await assetResponse.Content.ReadAsByteArrayAsync();

        // Save the zip file to temp directory
        string tempPath = Path.GetTempPath();
        string zipFileName = asset["name"]?.GetValue<string>();
        string zipFilePath = Path.Combine(tempPath, zipFileName);

        File.WriteAllBytes(zipFilePath, assetData);

        Console.WriteLine("Download complete. Extracting...");

        // Extract the nats-server.exe from the zip file
        string extractPath = Path.Combine(tempPath, "nats-server-extract");

        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }

        ZipFile.ExtractToDirectory(zipFilePath, extractPath);

        // Find nats-server.exe
        string natsServerExePath = Directory.GetFiles(extractPath, "nats-server.exe", SearchOption.AllDirectories).FirstOrDefault();

        if (natsServerExePath == null)
        {
            Console.WriteLine("nats-server.exe not found in the extracted files.");
            return;
        }

        // Copy the nats-server.exe to the bin directory with versioned name
        File.Copy(natsServerExePath, versionedExePath, true);
        Console.WriteLine($"Saved versioned nats-server.exe to {versionedExePath}");

        // Copy the nats-server.exe to the current directory
        string destinationPathCurrent = Path.Combine(Directory.GetCurrentDirectory(), "nats-server.exe");
        File.Copy(natsServerExePath, destinationPathCurrent, true);
        Console.WriteLine($"nats-server.exe updated to version {version} at {destinationPathCurrent}");

        // Clean up temporary files
        try
        {
            File.Delete(zipFilePath);
            Directory.Delete(extractPath, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to clean up temporary files. {ex.Message}");
        }
    }

    static Task UseCachedVersion(string version, string homeDirectory)
    {
        string binDirectory = Path.Combine(homeDirectory, "bin");
        string versionedExeName = $"nats-server-{version}.exe";
        string versionedExePath = Path.Combine(binDirectory, versionedExeName);

        if (!File.Exists(versionedExePath))
        {
            Console.WriteLine($"Cached version {version} not found in {binDirectory}");
            return Task.CompletedTask;
        }

        // Copy the versioned exe to the current directory as nats-server.exe
        string destinationPath = Path.Combine(binDirectory, "nats-server.exe");
        File.Copy(versionedExePath, destinationPath, true);
        Console.WriteLine($"nats-server.exe updated to version {version} at {destinationPath}");
        return Task.CompletedTask;
    }
}
