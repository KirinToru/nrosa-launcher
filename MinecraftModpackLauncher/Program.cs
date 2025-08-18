using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.IO.Compression;

namespace MinecraftModpackLauncher
{
    public partial class LauncherForm : Form
    {
        // GitHub Repository info - replace with your data
        private const string GITHUB_USER = "KirinToru";
        private const string GITHUB_REPO = "minecraft-modpack";
        private const string VERSION_FILE_URL = $"https://api.github.com/repos/{GITHUB_USER}/{GITHUB_REPO}/releases/latest";

        // Configuration - set to true only for the modpack creator
        private const bool IS_ADMIN_MODE = false; // Change to true for your version
        private string githubToken = ""; // Optional token for private repos

        private string minecraftDirectory = ""; // Main Minecraft folder
        private string gameDirectory = ""; // game subfolder where mods are located
        private string tlauncherExe = ""; // path to TL.exe
        private string[] trackingFolders = { "mods", "resourcepacks", "datapacks", "shaderpacks" };

        private TextBox gameFolderTextBox;
        private Button browseFolderButton;
        private Button updateButton;
        private Button playButton;
        private Button uploadButton; // For modpack creator
        private ProgressBar progressBar;
        private RichTextBox logTextBox;
        private Label statusLabel;

        public LauncherForm()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Size = new System.Drawing.Size(600, 500);
            this.Text = "Minecraft Modpack Launcher";
            this.StartPosition = FormStartPosition.CenterScreen;

            // Game folder selection
            var folderLabel = new Label
            {
                Text = "Legacy Launcher Minecraft folder:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(180, 20)
            };

            gameFolderTextBox = new TextBox
            {
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(400, 25),
                ReadOnly = true
            };

            browseFolderButton = new Button
            {
                Text = "Browse folder",
                Location = new System.Drawing.Point(430, 43),
                Size = new System.Drawing.Size(100, 27)
            };
            browseFolderButton.Click += BrowseFolderButton_Click;

            // Status label
            statusLabel = new Label
            {
                Text = "Ready",
                Location = new System.Drawing.Point(20, 80),
                Size = new System.Drawing.Size(500, 20),
                ForeColor = System.Drawing.Color.Green
            };

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(20, 105),
                Size = new System.Drawing.Size(510, 25),
                Style = ProgressBarStyle.Continuous
            };

            // Buttons
            updateButton = new Button
            {
                Text = "CHECK FOR UPDATES",
                Location = new System.Drawing.Point(20, 140),
                Size = new System.Drawing.Size(160, 40),
                BackColor = System.Drawing.Color.Orange,
                ForeColor = System.Drawing.Color.White
            };
            updateButton.Click += UpdateButton_Click;

            playButton = new Button
            {
                Text = "PLAY",
                Location = new System.Drawing.Point(200, 140),
                Size = new System.Drawing.Size(160, 40),
                BackColor = System.Drawing.Color.Green,
                ForeColor = System.Drawing.Color.White
            };
            playButton.Click += PlayButton_Click;

            uploadButton = new Button
            {
                Text = "UPLOAD (Admin)",
                Location = new System.Drawing.Point(380, 140),
                Size = new System.Drawing.Size(150, 40),
                BackColor = System.Drawing.Color.DarkBlue,
                ForeColor = System.Drawing.Color.White,
                Visible = IS_ADMIN_MODE // Only visible in admin mode
            };
            uploadButton.Click += UploadButton_Click;

            // Log text box
            var logLabel = new Label
            {
                Text = "Logs:",
                Location = new System.Drawing.Point(20, 195),
                Size = new System.Drawing.Size(100, 20)
            };

            logTextBox = new RichTextBox
            {
                Location = new System.Drawing.Point(20, 220),
                Size = new System.Drawing.Size(510, 200),
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9)
            };

            this.Controls.AddRange(new Control[] {
                folderLabel, gameFolderTextBox, browseFolderButton,
                statusLabel, progressBar, updateButton, playButton, uploadButton,
                logLabel, logTextBox
            });

            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.SizeGripStyle = SizeGripStyle.Hide;

            this.MinimumSize = new System.Drawing.Size(600, 500);
            this.MaximumSize = new System.Drawing.Size(600, 500);
        }

        private void BrowseFolderButton_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select Legacy Launcher Minecraft folder (contains TL.exe and game subfolder)";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    minecraftDirectory = folderDialog.SelectedPath;
                    gameDirectory = Path.Combine(minecraftDirectory, "game");
                    tlauncherExe = Path.Combine(minecraftDirectory, "TL.exe");

                    gameFolderTextBox.Text = minecraftDirectory;
                    SaveSettings();
                    LogMessage($"Selected Minecraft folder: {minecraftDirectory}");
                    LogMessage($"Game folder: {gameDirectory}");
                    LogMessage($"Legacy Launcher executable: {tlauncherExe}");

                    // Check if folder structure is correct
                    ValidateMinecraftDirectory();
                }
            }
        }

        private void ValidateMinecraftDirectory()
        {
            if (string.IsNullOrEmpty(minecraftDirectory)) return;

            bool isValid = true;
            List<string> issues = new List<string>();

            // Check if TL.exe exists
            if (!File.Exists(tlauncherExe))
            {
                issues.Add("TL.exe not found");
                LogMessage($"Warning: TL.exe not found at {tlauncherExe}");
            }

            // Check if game folder exists
            if (!Directory.Exists(gameDirectory))
            {
                try
                {
                    Directory.CreateDirectory(gameDirectory);
                    LogMessage($"Created game folder: {gameDirectory}");
                }
                catch (Exception ex)
                {
                    issues.Add("Cannot create game folder");
                    LogMessage($"Error creating game folder: {ex.Message}");
                    isValid = false;
                }
            }

            // Check/create tracking folders
            foreach (var folder in trackingFolders)
            {
                var folderPath = Path.Combine(gameDirectory, folder);
                if (!Directory.Exists(folderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(folderPath);
                        LogMessage($"Created folder: {folder}");
                    }
                    catch (Exception ex)
                    {
                        issues.Add($"Cannot create {folder} folder");
                        LogMessage($"Error creating folder {folder}: {ex.Message}");
                        isValid = false;
                    }
                }
            }

            // Update UI status
            if (isValid)
            {
                statusLabel.Text = "Minecraft folder is ready";
                statusLabel.ForeColor = System.Drawing.Color.Green;
                updateButton.Enabled = true;
                playButton.Enabled = true;

                if (issues.Count > 0)
                {
                    LogMessage("Folder setup complete with warnings:");
                    foreach (var issue in issues)
                    {
                        LogMessage($"  - {issue}");
                    }
                }
            }
            else
            {
                statusLabel.Text = "Issues with Minecraft folder";
                statusLabel.ForeColor = System.Drawing.Color.Red;
                LogMessage("Cannot proceed due to folder issues");
            }
        }

        private async void UpdateButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(minecraftDirectory))
            {
                MessageBox.Show("Please select the Minecraft folder first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            updateButton.Enabled = false;
            statusLabel.Text = "Checking for updates...";
            statusLabel.ForeColor = System.Drawing.Color.Blue;
            progressBar.Value = 0;

            try
            {
                await CheckAndDownloadUpdates();
                statusLabel.Text = "Update completed";
                statusLabel.ForeColor = System.Drawing.Color.Green;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Error during update";
                statusLabel.ForeColor = System.Drawing.Color.Red;
                LogMessage($"Error: {ex.Message}");
            }
            finally
            {
                updateButton.Enabled = true;
                progressBar.Value = 0;
            }
        }

        private async Task CheckAndDownloadUpdates()
        {
            LogMessage("Fetching version information...");

            // Get current version info from server
            var versionInfo = await GetVersionInfo();
            if (versionInfo == null)
            {
                LogMessage("Cannot fetch version information from server");
                return;
            }

            var localVersion = GetLocalVersion();

            LogMessage($"Local version: {localVersion}");
            LogMessage($"Server version: {versionInfo.Version}");

            if (localVersion == versionInfo.Version)
            {
                LogMessage("You have the latest version!");
                return;
            }

            LogMessage("Starting update...");
            progressBar.Maximum = versionInfo.Files.Count;

            foreach (var fileInfo in versionInfo.Files)
            {
                await DownloadAndUpdateFile(fileInfo);
                progressBar.Value++;
            }

            SaveLocalVersion(versionInfo.Version);
            LogMessage("Update completed successfully!");
        }

        private async Task<VersionInfo> GetVersionInfo()
        {
            try
            {
                LogMessage($"Requesting: {VERSION_FILE_URL}");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "MinecraftModpackLauncher");

                    // Add token if available
                    if (!string.IsNullOrEmpty(githubToken))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"token {githubToken}");
                    }

                    // Get latest release info from GitHub API
                    var response = await client.GetAsync(VERSION_FILE_URL);
                    LogMessage($"GitHub API Response Status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        LogMessage($"GitHub API Error: {errorContent}");
                        return null;
                    }

                    var releaseJson = await response.Content.ReadAsStringAsync();
                    LogMessage($"Release JSON received: {releaseJson.Substring(0, Math.Min(200, releaseJson.Length))}...");

                    var release = JsonSerializer.Deserialize<GitHubRelease>(releaseJson);
                    LogMessage($"Found release: {release.tag_name} with {release.assets.Length} assets");

                    var versionInfo = new VersionInfo
                    {
                        Version = release.tag_name
                    };

                    // Find version.json file in assets
                    var versionAsset = release.assets.FirstOrDefault(a => a.name == "version.json");
                    if (versionAsset != null)
                    {
                        LogMessage("Found version.json in assets, downloading...");
                        var versionJson = await client.GetStringAsync(versionAsset.browser_download_url);
                        var detailedInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);

                        // Add download links from GitHub
                        foreach (var file in detailedInfo.Files)
                        {
                            var asset = release.assets.FirstOrDefault(a => a.name == Path.GetFileName(file.RelativePath) ||
                                                                          a.name == file.RelativePath.Replace('\\', '_').Replace('/', '_'));
                            if (asset != null)
                            {
                                file.DownloadUrl = asset.browser_download_url;
                                LogMessage($"Mapped file {file.RelativePath} to {asset.name}");
                            }
                            else
                            {
                                LogMessage($"Warning: No asset found for {file.RelativePath}");
                            }
                        }

                        versionInfo.Files = detailedInfo.Files;
                    }
                    else
                    {
                        LogMessage("Warning: version.json not found in release assets");
                        LogMessage("Available assets:");
                        foreach (var asset in release.assets)
                        {
                            LogMessage($"  - {asset.name}");
                        }
                    }

                    return versionInfo;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error fetching version: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task DownloadAndUpdateFile(ModpackFileInfo fileInfo)
        {
            var localFilePath = Path.Combine(gameDirectory, fileInfo.RelativePath);

            // Check if file exists and has same hash
            if (File.Exists(localFilePath))
            {
                var localHash = CalculateFileHash(localFilePath);
                if (localHash == fileInfo.Hash)
                {
                    LogMessage($"File {fileInfo.RelativePath} is up to date");
                    return;
                }
            }

            try
            {
                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(localFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                LogMessage($"Downloading: {fileInfo.RelativePath}");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "MinecraftModpackLauncher");

                    // If it's an archive, download and extract
                    if (fileInfo.IsArchive)
                    {
                        await DownloadAndExtractArchive(fileInfo, localFilePath, client);
                    }
                    else
                    {
                        // Download file directly
                        var fileBytes = await client.GetByteArrayAsync(fileInfo.DownloadUrl);
                        await File.WriteAllBytesAsync(localFilePath, fileBytes);
                    }
                }

                LogMessage($"Updated: {fileInfo.RelativePath}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error downloading {fileInfo.RelativePath}: {ex.Message}");
            }
        }

        private async Task DownloadAndExtractArchive(ModpackFileInfo fileInfo, string targetPath, HttpClient client)
        {
            // Download archive to temp
            var tempZipPath = Path.GetTempFileName();
            var zipBytes = await client.GetByteArrayAsync(fileInfo.DownloadUrl);
            await File.WriteAllBytesAsync(tempZipPath, zipBytes);

            // Determine target folder based on archive name
            var targetFolder = Path.GetDirectoryName(targetPath);
            if (fileInfo.RelativePath.Contains("mods"))
                targetFolder = Path.Combine(gameDirectory, "mods");
            else if (fileInfo.RelativePath.Contains("resourcepacks"))
                targetFolder = Path.Combine(gameDirectory, "resourcepacks");
            else if (fileInfo.RelativePath.Contains("datapacks"))
                targetFolder = Path.Combine(gameDirectory, "datapacks");
            else if (fileInfo.RelativePath.Contains("shaderpacks"))
                targetFolder = Path.Combine(gameDirectory, "shaderpacks");

            // Remove old files in folder
            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, true);
            }
            Directory.CreateDirectory(targetFolder);

            // Extract archive
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, targetFolder);

            // Delete temp file
            File.Delete(tempZipPath);
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(minecraftDirectory))
            {
                MessageBox.Show("Please select the Minecraft folder first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Try to launch TL.exe if it exists
                if (File.Exists(tlauncherExe))
                {
                    Process.Start(tlauncherExe);
                    LogMessage($"Launched Legacy Launcher: {tlauncherExe}");
                }
                else
                {
                    // Fallback: try to find Legacy Launcher in default location
                    var defaultTLauncher = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "AppData\\Roaming\\.tlauncher\\tlauncher.exe");

                    if (File.Exists(defaultTLauncher))
                    {
                        Process.Start(defaultTLauncher);
                        LogMessage("Launched Legacy Launcher from default location");
                    }
                    else
                    {
                        // Last resort: open Minecraft folder
                        Process.Start("explorer.exe", minecraftDirectory);
                        LogMessage("Opened Minecraft folder - launch Legacy Launcher manually");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error launching game: {ex.Message}");
                // Try opening folder as fallback
                try
                {
                    Process.Start("explorer.exe", minecraftDirectory);
                    LogMessage("Opened Minecraft folder as fallback");
                }
                catch
                {
                    MessageBox.Show("Cannot launch Legacy Launcher. Please run it manually.", "Launch Error");
                }
            }
        }

        private async void UploadButton_Click(object sender, EventArgs e)
        {
            // This function is for the modpack creator
            if (string.IsNullOrEmpty(minecraftDirectory))
            {
                MessageBox.Show("Please select the Minecraft folder first!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            uploadButton.Enabled = false;
            statusLabel.Text = "Creating update package...";
            statusLabel.ForeColor = System.Drawing.Color.Blue;

            try
            {
                await CreateUpdatePackage();
                statusLabel.Text = "Package created - upload files to server";
                statusLabel.ForeColor = System.Drawing.Color.Green;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Error creating package";
                statusLabel.ForeColor = System.Drawing.Color.Red;
                LogMessage($"Error: {ex.Message}");
            }
            finally
            {
                uploadButton.Enabled = true;
            }
        }

        private async Task CreateUpdatePackage()
        {
            var newVersion = DateTime.Now.ToString("yyyyMMdd.HHmm");
            var files = new List<ModpackFileInfo>();
            var tempDir = Path.Combine(Path.GetTempPath(), "ModpackRelease");

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            LogMessage($"Creating version: {newVersion}");
            LogMessage($"Temporary folder: {tempDir}");

            // For each folder create ZIP archive
            foreach (var folder in trackingFolders)
            {
                var folderPath = Path.Combine(gameDirectory, folder);
                if (Directory.Exists(folderPath) && Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories).Length > 0)
                {
                    var zipFileName = $"{folder}.zip";
                    var zipPath = Path.Combine(tempDir, zipFileName);

                    LogMessage($"Creating archive: {zipFileName}");
                    System.IO.Compression.ZipFile.CreateFromDirectory(folderPath, zipPath);

                    var hash = CalculateFileHash(zipPath);
                    files.Add(new ModpackFileInfo
                    {
                        RelativePath = zipFileName,
                        Hash = hash,
                        IsArchive = true,
                        ArchiveTargetFolder = folder
                    });
                }
            }

            // Create version.json file
            var versionInfo = new VersionInfo
            {
                Version = newVersion,
                Files = files
            };

            var versionJson = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions { WriteIndented = true });
            var versionFilePath = Path.Combine(tempDir, "version.json");
            await File.WriteAllTextAsync(versionFilePath, versionJson);

            LogMessage($"Created {files.Count} archives in folder:");
            LogMessage($"{tempDir}");
            LogMessage("");
            LogMessage("=== GITHUB UPLOAD INSTRUCTIONS ===");
            LogMessage("1. Go to https://github.com/YOUR_NAME/minecraft-modpack/releases");
            LogMessage("2. Click 'Create a new release'");
            LogMessage($"3. Tag version: {newVersion}");
            LogMessage($"4. Release title: Modpack {newVersion}");
            LogMessage("5. Drag ALL files from the folder above");
            LogMessage("6. Click 'Publish release'");
            LogMessage("====================================");

            // Open folder in explorer
            Process.Start("explorer.exe", tempDir);
        }

        private async Task ScanFolderForFiles(string folderPath, string relativeFolderPath, List<ModpackFileInfo> files)
        {
            var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                var relativePath = Path.GetRelativePath(gameDirectory, filePath);
                var hash = CalculateFileHash(filePath);

                files.Add(new ModpackFileInfo
                {
                    RelativePath = relativePath,
                    Hash = hash
                });
            }
        }

        private string CalculateFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string GetLocalVersion()
        {
            var versionFile = Path.Combine(gameDirectory, ".version");
            return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "0";
        }

        private void SaveLocalVersion(string version)
        {
            var versionFile = Path.Combine(gameDirectory, ".version");
            File.WriteAllText(versionFile, version);
        }

        private void LoadSettings()
        {
            var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinecraftLauncher", "settings.json");

            if (File.Exists(settingsFile))
            {
                try
                {
                    var json = File.ReadAllText(settingsFile);
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    minecraftDirectory = settings.MinecraftDirectory ?? "";

                    if (!string.IsNullOrEmpty(minecraftDirectory))
                    {
                        gameDirectory = Path.Combine(minecraftDirectory, "game");
                        tlauncherExe = Path.Combine(minecraftDirectory, "TL.exe");
                        gameFolderTextBox.Text = minecraftDirectory;
                        ValidateMinecraftDirectory();
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error loading settings: {ex.Message}");
                }
            }
        }

        private void SaveSettings()
        {
            var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinecraftLauncher");

            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            var settingsFile = Path.Combine(settingsDir, "settings.json");
            var settings = new Settings { MinecraftDirectory = minecraftDirectory };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFile, json);
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";

            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() => {
                    logTextBox.AppendText(logEntry + Environment.NewLine);
                    logTextBox.ScrollToCaret();
                }));
            }
            else
            {
                logTextBox.AppendText(logEntry + Environment.NewLine);
                logTextBox.ScrollToCaret();
            }
        }
    }

    public class VersionInfo
    {
        public string Version { get; set; }
        public List<ModpackFileInfo> Files { get; set; } = new List<ModpackFileInfo>();
    }

    public class ModpackFileInfo
    {
        public string RelativePath { get; set; }
        public string Hash { get; set; }
        public string DownloadUrl { get; set; } // URL to download from GitHub
        public bool IsArchive { get; set; } // Whether it's an archive to extract
        public string ArchiveTargetFolder { get; set; } // Target folder for archive
    }

    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public GitHubAsset[] assets { get; set; }
    }

    public class GitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    public class Settings
    {
        public string MinecraftDirectory { get; set; }
    }

    class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }
}