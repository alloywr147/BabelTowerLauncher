using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BabelManager
{
    public partial class MainWindow
    {
        bool startupCheckStarted, startupModAfterSetup;
        CancellationTokenSource startupCancellation;
        StartupUpdateReport startupReport;
        readonly StartupUpdateNotice startupNotice = new StartupUpdateNotice();

        async void CheckStartupUpdates()
        {
            if (startupCheckStarted || IsDisposed) return;
            startupCheckStarted = true;
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            startupCancellation = cancellation;
            try
            {
                startupReport = await StartupUpdates.CheckAsync(OfficialUpdates.GetLatest, OfficialUpdates.GetLatestLauncher, cancellation.Token);
                if (IsDisposed || Disposing) return;
                if (startupReport.ModError != "") Log("启动时 Mod 更新检查未完成：" + startupReport.ModError);
                if (startupReport.LauncherError != "") Log("启动时软件更新检查未完成：" + startupReport.LauncherError);
                OfferStartupUpdates();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed && !Disposing) Log("启动更新检查未完成：" + error.Message); }
            finally
            {
                if (startupCancellation == cancellation) startupCancellation = null;
                cancellation.Dispose();
            }
        }

        void OfferStartupUpdates()
        {
            // Keep the notification pending while the user is in a game, another dialog,
            // an active operation or path detection. Activation/completion retries it.
            if (busy || detecting || IsDisposed || Disposing || !Visible || !ContainsFocus ||
                WindowState == FormWindowState.Minimized || OwnedForms.Length != 0) return;
            if (!startupNotice.TryClaim(startupReport, settings, SelfUpdater.CurrentVersion)) return;
            using (var dialog = new StartupUpdateDialog(startupReport, settings, SelfUpdater.CurrentVersion))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (dialog.Choice == StartupUpdateChoice.Mod)
                {
                    officialRelease = startupReport.Mod;
                    officialArchive = "";
                    downloadedTag = "";
                    if (!Core.IsGame(settings.GameRoot) || !Directory.Exists(settings.InstallRoot))
                    {
                        startupModAfterSetup = true;
                        ShowSetup();
                    }
                    else BeginStartupModUpdate();
                }
                else if (dialog.Choice == StartupUpdateChoice.Launcher)
                {
                    launcherRelease = startupReport.Launcher;
                    ShowSoftwareUpdate();
                    DownloadSoftware();
                }
            }
        }

        async void BeginStartupModUpdate()
        {
            ShowImportPage();
            SetBusy(true);
            try
            {
                officialArchive = await Task.Run(() => DownloadCache.Find(Path.Combine(Core.DataDir, "downloads"), officialRelease));
                downloadedTag = String.IsNullOrEmpty(officialArchive) ? "" : officialRelease.Tag;
            }
            catch (Exception error) { Log("已有下载缓存未能读取：" + error.Message); }
            finally { SetBusy(false); }
            DownloadOfficial();
        }

        void StartupSettingsSaved()
        {
            if (startupModAfterSetup)
            {
                startupModAfterSetup = false;
                BeginStartupModUpdate();
            }
            else OfferStartupUpdates();
        }

        void QueueStartupOffer()
        {
            if (startupReport != null && IsHandleCreated && !IsDisposed && !Disposing)
                BeginInvoke(new Action(OfferStartupUpdates));
        }
    }
}
