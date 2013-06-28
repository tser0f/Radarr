﻿using System.Diagnostics;
using System.IO;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Update.Commands;

namespace NzbDrone.Core.Update
{
    public class InstallUpdateService : IExecute<ApplicationUpdateCommand>
    {
        private readonly ICheckUpdateService _checkUpdateService;
        private readonly Logger _logger;
        private readonly IAppDirectoryInfo _appDirectoryInfo;

        private readonly IDiskProvider _diskProvider;
        private readonly IHttpProvider _httpProvider;
        private readonly ArchiveProvider _archiveProvider;
        private readonly IProcessProvider _processProvider;


        public InstallUpdateService(ICheckUpdateService checkUpdateService, IAppDirectoryInfo appDirectoryInfo,
                                    IDiskProvider diskProvider, IHttpProvider httpProvider,
                                    ArchiveProvider archiveProvider, IProcessProvider processProvider, Logger logger)
        {
            _checkUpdateService = checkUpdateService;
            _appDirectoryInfo = appDirectoryInfo;
            _diskProvider = diskProvider;
            _httpProvider = httpProvider;
            _archiveProvider = archiveProvider;
            _processProvider = processProvider;
            _logger = logger;
        }

        public void Execute(ApplicationUpdateCommand message)
        {
            var latestAvailable = _checkUpdateService.AvailableUpdate();

            if (latestAvailable != null)
            {
                InstallUpdate(latestAvailable);
            }
        }

        private void InstallUpdate(UpdatePackage updatePackage)
        {
            var updateSandboxFolder = _appDirectoryInfo.GetUpdateSandboxFolder();

            var packageDestination = Path.Combine(updateSandboxFolder, updatePackage.FileName);

            if (_diskProvider.FolderExists(updateSandboxFolder))
            {
                _logger.Info("Deleting old update files");
                _diskProvider.DeleteFolder(updateSandboxFolder, true);
            }

            _logger.Info("Downloading update package from [{0}] to [{1}]", updatePackage.Url, packageDestination);
            _httpProvider.DownloadFile(updatePackage.Url, packageDestination);
            _logger.Info("Download completed for update package from [{0}]", updatePackage.FileName);

            _logger.Info("Extracting Update package");
            _archiveProvider.ExtractArchive(packageDestination, updateSandboxFolder);
            _logger.Info("Update package extracted successfully");

            _logger.Info("Preparing client");
            _diskProvider.MoveDirectory(_appDirectoryInfo.GetUpdateClientFolder(),
                                        updateSandboxFolder);


            _logger.Info("Starting update client");
            var startInfo = new ProcessStartInfo
                {
                    FileName = _appDirectoryInfo.GetUpdateClientExePath(),
                    Arguments = _processProvider.GetCurrentProcess().Id.ToString()
                };

            var process = _processProvider.Start(startInfo);

            _processProvider.WaitForExit(process);

            _logger.Error("Update process failed");
        }

    }
}