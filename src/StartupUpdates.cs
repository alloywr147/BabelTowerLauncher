using System;
using System.Threading;
using System.Threading.Tasks;

namespace BabelManager
{
    public sealed class StartupUpdateReport
    {
        public OfficialRelease Mod, Launcher;
        public string ModError = "", LauncherError = "";

        public bool ModAvailable(Settings settings)
        {
            Version installed = OfficialUpdates.CurrentVersion(settings.CurrentFolder);
            return Mod != null && installed != null && Mod.Version > installed;
        }
        public bool LauncherAvailable(Version installed)
        {
            return Launcher != null && Launcher.Version > installed;
        }
    }

    public sealed class StartupUpdateNotice
    {
        bool notified;
        public bool TryClaim(StartupUpdateReport report, Settings settings, Version launcherVersion)
        {
            if (notified || report == null || !(report.ModAvailable(settings) || report.LauncherAvailable(launcherVersion))) return false;
            notified = true;
            return true;
        }
    }

    public static class StartupUpdates
    {
        sealed class FetchResult { public OfficialRelease Release; public string Error = ""; }

        static FetchResult Fetch(Func<CancellationToken, OfficialRelease> source, UpdateSource expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var release = source(token);
                token.ThrowIfCancellationRequested();
                OfficialUpdates.Validate(release);
                if (release.Source != expected) throw new Exception("更新来源与检查项目不一致。");
                return new FetchResult { Release = release };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { token.ThrowIfCancellationRequested(); return new FetchResult { Error = error.Message }; }
        }

        public static async Task<StartupUpdateReport> CheckAsync(
            Func<CancellationToken, OfficialRelease> modSource,
            Func<CancellationToken, OfficialRelease> launcherSource,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // Both read-only checks run independently: an unavailable source cannot hide the other.
            var mod = Task.Run(() => Fetch(modSource, UpdateSource.Mod, token), token);
            var launcher = Task.Run(() => Fetch(launcherSource, UpdateSource.Launcher, token), token);
            await Task.WhenAll(mod, launcher).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new StartupUpdateReport
            {
                Mod = mod.Result.Release, ModError = mod.Result.Error,
                Launcher = launcher.Result.Release, LauncherError = launcher.Result.Error
            };
        }
    }
}
