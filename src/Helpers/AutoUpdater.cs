using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
                string script;
                using (var stream = typeof(AutoUpdater).Assembly.GetManifestResourceStream("Panseo.ApplyUpdate.ps1"))
                using (var reader = new StreamReader(stream)) script = reader.ReadToEnd();
                string command = "& {" + script + "} -ProcessId " + Process.GetCurrentProcess().Id +
                    " -SourcePath " + Quote(staged) + " -TargetPath " + Quote(_target) +
                    " -ExpectedHash " + Quote(release.assets[0].digest.Substring(7)) +
                    " -MetadataPath " + Quote(metadata) + " -LogPath " + Quote(Path.Combine(_folder, "update.log"));
                var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                    "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " +
                    Convert.ToBase64String(Encoding.Unicode.GetBytes(command)))
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                using (Process.Start(info)) { }
            }
            catch (Exception ex) { Log(ex); }
        }

        internal static string Quote(string value) { return "'" + value.Replace("'", "''") + "'"; }

        public void Log(Exception error)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                File.AppendAllText(Path.Combine(_folder, "update.log"), DateTime.Now.ToString("s") + " " + error.Message + Environment.NewLine);
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
