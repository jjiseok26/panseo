using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;

namespace Panseo.Helpers
{
    internal sealed class AutoUpdater
    {
        internal const string Repository = "jjiseok26/panseo";
        private readonly string _target;
        private readonly string _folder;
        private bool _checking;
        private static readonly Version CurrentVersion = typeof(AutoUpdater).Assembly.GetName().Version;

        public AutoUpdater(string executablePath)
        {
            _target = Path.GetFullPath(executablePath);
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(_target.ToUpperInvariant()))).Replace("-", "").Substring(0, 16);
            _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Panseo", "Updates", key);
        }

        public async Task<string> CheckAsync()
        {
            if (_checking) return null;
            _checking = true;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new UpdateClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Panseo/" + CurrentVersion;
                    client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    string json = await client.DownloadStringTaskAsync("https://api.github.com/repos/" + Repository + "/releases/latest");
                    var release = ParseRelease(json, CurrentVersion);
                    if (release == null) return "최신 버전입니다. (" + CurrentVersion.ToString(3) + ")";
                    Directory.CreateDirectory(_folder);
                    string staged = Path.Combine(_folder, "Panseo.exe");
                    string partial = Path.Combine(_folder, "Panseo.download");
                    if (!VerifyFile(staged, release))
                    {
                        await client.DownloadFileTaskAsync(release.assets[0].browser_download_url, partial);
                        if (!VerifyFile(partial, release)) throw new InvalidDataException("업데이트 파일의 버전 또는 SHA-256 검증에 실패했습니다.");
                        File.Copy(partial, staged, true);
                        File.Delete(partial);
                    }
                    File.WriteAllText(Path.Combine(_folder, "release.json"), json, Encoding.UTF8);
                    return "새 버전 " + release.tag_name + " 다운로드 완료. 프로그램을 종료하면 교체되며 다음 실행부터 적용됩니다.";
                }
            }
            finally { _checking = false; }
        }

        internal static ReleaseInfo ParseRelease(string json, Version installed)
        {
            var release = new JavaScriptSerializer().Deserialize<ReleaseInfo>(json);
            if (release == null || release.draft || release.prerelease) return null;
            Version version;
            if (release.tag_name == null || !Regex.IsMatch(release.tag_name, @"^v\d+\.\d+\.\d+$") ||
                !Version.TryParse(release.tag_name.Substring(1) + ".0", out version))
                throw new InvalidDataException("지원하지 않는 업데이트 버전입니다.");
            if (version <= installed) return null;
            if (release.assets == null) throw new InvalidDataException("업데이트 파일이 없습니다.");
            foreach (var asset in release.assets)
            {
                if (asset.name != "Panseo.exe") continue;
                string expectedUrl = "https://github.com/" + Repository + "/releases/download/" + release.tag_name + "/Panseo.exe";
                if (asset.browser_download_url != expectedUrl || asset.digest == null ||
                    !Regex.IsMatch(asset.digest, @"^sha256:[0-9a-fA-F]{64}$") || asset.size < 1024 || asset.size > 25 * 1024 * 1024)
                    throw new InvalidDataException("업데이트 주소, 크기 또는 SHA-256 정보가 올바르지 않습니다.");
                release.assets = new[] { asset };
                return release;
            }
            throw new InvalidDataException("Panseo.exe 업데이트 파일이 없습니다.");
        }

        internal static bool VerifyFile(string path, ReleaseInfo release)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length != release.assets[0].size) return false;
                string digest;
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    digest = "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return string.Equals(digest, release.assets[0].digest, StringComparison.OrdinalIgnoreCase) &&
                    AssemblyName.GetAssemblyName(path).Version == new Version(release.tag_name.Substring(1) + ".0");
            }
            catch (IOException) { return false; }
            catch (BadImageFormatException) { return false; }
        }

        public void ApplyOnExit()
        {
            try
            {
                string metadata = Path.Combine(_folder, "release.json");
                if (!File.Exists(metadata)) return;
                var release = ParseRelease(File.ReadAllText(metadata), CurrentVersion);
                string staged = Path.Combine(_folder, "Panseo.exe");
                if (release == null || !VerifyFile(staged, release)) return;
                // Run the verified on-disk application in its visible update-install mode.
                var info = new ProcessStartInfo(staged,
                    "--apply-update " + Process.GetCurrentProcess().Id + " " + QuoteExecutablePath(_target))
                { UseShellExecute = false, WorkingDirectory = _folder, WindowStyle = ProcessWindowStyle.Normal };
                using (Process.Start(info)) { }
            }
            catch (Exception ex) { Log(ex); }
        }

        internal static string QuoteExecutablePath(string path)
        {
            if (path.IndexOf('"') >= 0 || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid executable path.");
            return "\"" + path + "\"";
        }

        internal static void ApplyUpdateFromArguments(string[] args)
        {
            int processId;
            if (args.Length != 3 || args[0] != "--apply-update" || !int.TryParse(args[1], out processId) ||
                processId <= 0 || processId == Process.GetCurrentProcess().Id)
                throw new ArgumentException("Invalid update arguments.");
            var updater = new AutoUpdater(args[2]);
            try
            {
                string staged = Path.Combine(updater._folder, "Panseo.exe");
                string current = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.Equals(Path.GetFullPath(current), staged, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Updater must run from its verified update folder.");
                Process previous = null;
                try { previous = Process.GetProcessById(processId); }
                catch (ArgumentException) { }
                if (previous != null)
                {
                    using (previous)
                        if (!previous.WaitForExit(120000)) throw new IOException("Program is still running; update deferred.");
                }
                string metadata = Path.Combine(updater._folder, "release.json");
                var installed = AssemblyName.GetAssemblyName(updater._target).Version;
                var release = ParseRelease(File.ReadAllText(metadata), installed);
                if (release == null) return;
                InstallVerifiedUpdate(staged, updater._target, release);
                File.Delete(metadata);
                updater.WriteLog("Updated to " + release.tag_name);
            }
            catch (Exception ex) { updater.Log(ex); throw; }
        }

        internal static void InstallVerifiedUpdate(string source, string target, ReleaseInfo release)
        {
            if (!VerifyFile(source, release)) throw new InvalidDataException("Update version or SHA-256 mismatch.");
            var incoming = AssemblyName.GetAssemblyName(source);
            var installed = AssemblyName.GetAssemblyName(target);
            string product = typeof(AutoUpdater).Assembly.GetName().Name;
            if (incoming.Name != product || installed.Name != product)
                throw new InvalidDataException("Update target is not Panseo.");
            if (installed.Version >= incoming.Version) return;
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update source and destination must differ.");
            string replacement = target + ".update-new";
            string backup = target + ".update-backup";
            File.Copy(source, replacement, true);
            if (!VerifyFile(replacement, release)) throw new InvalidDataException("Copied update failed verification.");
            for (int attempt = 0; ; attempt++)
            {
                try { File.Replace(replacement, target, backup, true); break; }
                catch (IOException) { if (attempt >= 29) throw; Thread.Sleep(200); }
            }
        }

        public void Log(Exception error) { WriteLog(error.Message); }

        private void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                File.AppendAllText(Path.Combine(_folder, "update.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private sealed class UpdateClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                request.Timeout = 30000;
                var http = request as HttpWebRequest;
                if (http != null) http.ReadWriteTimeout = 30000;
                return request;
            }
        }

        public sealed class ReleaseInfo
        {
            public string tag_name { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public ReleaseAsset[] assets { get; set; }
        }

        public sealed class ReleaseAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
            public string digest { get; set; }
            public long size { get; set; }
        }
    }
}
