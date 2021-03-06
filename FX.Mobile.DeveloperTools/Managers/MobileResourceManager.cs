﻿#region License Information
/* 

	SalesLogix Mobile Developer Tools
	Copyright (C) 2012  Customer FX Corporation - http://customerfx.com/

	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either GetResourceVersion() 3 of the License, or
	(at your option) any later GetResourceVersion.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <http://www.gnu.org/licenses/>.
	
	ADDITIONAL TERMS: 
	The "Customer FX" link to customerfx.com on the main form must 
	remain visible and in-tact on any forks or use of this source code.

	Contact Information:
	
	Ryan Farley 
	Customer FX Corporation
	http://customerfx.com/
	2324 University Avenue West, Suite 115
	Saint Paul, Minnesota 55114
	Tel: 651.646.7777  Fax: 651.846.5185
	
	This copyright must remain intact
	
*/
#endregion

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using Ionic.Zip;
using FX.Mobile.DeveloperTools.Utility;
using FX.Mobile.DeveloperTools.Model;
using System.Collections.Generic;

namespace FX.Mobile.DeveloperTools.Managers
{
	public class MobileResourceManager
	{
		public event EventHandler<MobileResourceInstallEventArgs> ResourceInstallInitializing;
		public event EventHandler<MobileResourceInstallEventArgs> ResourceInstallProgress;
		public event EventHandler<MobileResourceInstallEventArgs> ResourceInstallStepUpdate;
		public event EventHandler<MobileResourceInstallEventArgs> ResourceInstallComplete;

		public MobileResourceManager(string MobilePath, MobileVersion Version)
		{
			this.MobilePath = MobilePath;
			this.Version = Version;
		}

		public MobileVersion Version { get; set; }
		public string MobilePath { get; set; }
		public bool IncludeArgosSample { get; set; }
		public bool IncludeArgos754Compatability { get; set; }

		private Queue<ResourcePackage> _packages;
		private List<ResourcePackage> _completedPackageList;

		private ResourcePackage CurrentPackage { get; set; }
		private int CurrentStep { get; set; }

		public void Install()
		{
			_packages = new Queue<ResourcePackage>();

			_packages.Enqueue(new ResourcePackage
			{
				Name = "argos-sdk " + GetResourceVersion(),
				File = "argos-sdk-" + GetResourceVersionBare() + ".zip",
				Path = Path.Combine(MobilePath, "argos-sdk"),
				Account = "Saleslogix",
				Repository = "argos-sdk",
				Archive = GetResourceVersion() + ".zip",
				PostAction = () =>
				{
					if (!Directory.Exists(Path.Combine(MobilePath, "products")))
						Directory.CreateDirectory(Path.Combine(MobilePath, "products"));

					Action postAction = GetResourcePostAction(Path.Combine(MobilePath, "argos-sdk"));
					postAction.Invoke();
				}
			});

			_packages.Enqueue(new ResourcePackage
			{
				Name = "argos-saleslogix " + GetResourceVersion(),
				File = "argos-saleslogix-" + GetResourceVersionBare() + "-gold.zip",
				Path = Path.Combine(Path.Combine(MobilePath, "products"), "argos-saleslogix"),
				Account = "Saleslogix",
				Repository = "argos-saleslogix",
				Archive = GetResourceVersion() + "-gold.zip",
				PostAction = GetResourcePostAction(Path.Combine(Path.Combine(MobilePath, "products"), "argos-saleslogix"))
			});

			if (IncludeArgosSample)
			{
				_packages.Enqueue(new ResourcePackage
				{
					Name = "argos-sample " + GetResourceVersion(),
					File = Path.Combine(MobilePath, "argos-sample-" + (GetResourceVersionBare() == "2.0" ? "master" : GetResourceVersion()) + ".zip"),
					Path = Path.Combine(Path.Combine(MobilePath, "products"), "argos-sample"),
					Account = "Saleslogix",
					Repository = "argos-sample",
					Archive = (GetResourceVersion() == "2.0" ? "master" : "v" + GetResourceVersion()) + ".zip", // TODO: Create tags on argos-sample to match the right versions (same as SDK)
					PostAction = () => File.Move(Path.Combine(MobilePath, @"products\argos-sample\index-dev-sample.html"), Path.Combine(MobilePath, @"products\argos-saleslogix\index-dev-sample.html"))
				});
			}

			if (IncludeArgos754Compatability)
			{
				_packages.Enqueue(new ResourcePackage
				{
					Name = "argos-saleslogix-20_for_754 " + GetResourceVersion(),
					File = Path.Combine(MobilePath, "argos-saleslogix-20_for_754-" + (GetResourceVersionBare() == "2.0" ? "master" : GetResourceVersion()) + ".zip"),
					Path = Path.Combine(Path.Combine(MobilePath, "products"), "argos-saleslogix-20_for_754"),
					Account = "Saleslogix",
					Repository = "argos-saleslogix-754",
					Archive = (GetResourceVersion() == "2.0" ? "master" : GetResourceVersion()) + ".zip", // TODO: Create tags on argos-saleslogix-754 to match the right version (same as SDK)
					PostAction = () => File.Move(Path.Combine(MobilePath, @"products\argos-saleslogix-20_for_754\index-dev-20_for_754.html"), Path.Combine(MobilePath, @"products\argos-saleslogix\index-dev-saleslogix-20_for_754.html"))
				});
			}

			CurrentStep = 1;
			_completedPackageList = new List<ResourcePackage>();

			if (ResourceInstallInitializing != null)
				ResourceInstallInitializing(this, new MobileResourceInstallEventArgs { Action = "Initializing downloads", StepNumber = CurrentStep, StepTotal = (_packages.Count * 2) });

			InstallPackage(_packages.Dequeue());
		}

		private void InstallPackage(ResourcePackage package)
		{
			CurrentPackage = package;
			_completedPackageList.Add(package);

			if (ResourceInstallStepUpdate != null)
				ResourceInstallStepUpdate(this, new MobileResourceInstallEventArgs { StepNumber = CurrentStep++ });

			var client = new WebClient();
			client.DownloadProgressChanged += ResourceDownloadProgressChanged;
			client.DownloadFileCompleted += ResourceDownloadFileCompleted;
			client.DownloadFileAsync(new Uri(string.Format("https://github.com/{0}/{1}/archive/{2}", package.Account, package.Repository, package.Archive)), Path.Combine(MobilePath, package.File));
		}

		private void ResourceDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			if (ResourceInstallProgress != null)
				ResourceInstallProgress(this, new MobileResourceInstallEventArgs { Action = "Downloading", CurrentPackage = CurrentPackage.Name, ProgressPercentage = e.ProgressPercentage });
		}

		private void ResourceDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			if (ResourceInstallStepUpdate != null)
				ResourceInstallStepUpdate(this, new MobileResourceInstallEventArgs { StepNumber = CurrentStep++ });

			if (ResourceInstallProgress != null)
				ResourceInstallProgress(this, new MobileResourceInstallEventArgs { Action = "Extracting", CurrentPackage = CurrentPackage.Name, ProgressCurrentFile = 0, ProgressTotalFiles = 0 });

			ExtractResource(Path.Combine(MobilePath, CurrentPackage.File), Path.Combine(MobilePath, CurrentPackage.Path));

			if (CurrentPackage.PostAction != null)
				CurrentPackage.PostAction();

			if (_packages.Count > 0)
				InstallPackage(_packages.Dequeue());
			else
			{
				CreateReadme();
				CreateStartShortcut();
				CreateIndex();

				if (ResourceInstallComplete != null)
					ResourceInstallComplete(this, new MobileResourceInstallEventArgs());
			}
		}

		private void ExtractResource(string ResourceFile, string Location)
		{
			using (var zip = ZipFile.Read(ResourceFile))
			{
				int total = zip.Count;
				int count = 1;

				foreach (ZipEntry file in zip)
				{
					file.Extract(MobilePath, ExtractExistingFileAction.OverwriteSilently);

					if (ResourceInstallProgress != null)
						ResourceInstallProgress(this, new MobileResourceInstallEventArgs { Action = "Extracting", CurrentPackage = CurrentPackage.Name, ProgressCurrentFile = count, ProgressTotalFiles = total });

					count++;
				}
			}

			var dir = new DirectoryInfo(Path.Combine(MobilePath, new FileInfo(ResourceFile).Name.Replace(".zip", "")));
			dir.MoveTo(Location);
			File.Delete(ResourceFile);
		}

		private void CreateReadme()
		{
			using (var writer = new StreamWriter(Path.Combine(MobilePath, "SalesLogix Mobile Version " + GetResourceVersion() + ".txt")))
			{
				writer.WriteLine("SalesLogix Mobile Version " + GetResourceVersion());
				writer.WriteLine("Development Environment");
				writer.WriteLine("------------------------------------------------");
				writer.WriteLine("\r\n");
				writer.WriteLine("Use the 'Start Mobile Website' link to start the website in  IIS Express");
				writer.WriteLine("or open IIS and add a new website to point to this directory.");
				writer.WriteLine("\r\n");
				writer.WriteLine("To access production configuration visit:");
				writer.WriteLine("http://MobileWebsiteRoot/products/argos-saleslogix/index.html");
				writer.WriteLine("\r\n");
				writer.WriteLine("To access development configuration visit:");
				writer.WriteLine("http://MobileWebsiteRoot/products/argos-saleslogix/index-dev.html");
				writer.WriteLine("\r\n");
				writer.WriteLine("------------------------------------------------");
				writer.WriteLine("SalesLogix Mobile Development Tools");
				writer.WriteLine("http://customerfx.com");
			}
		}

		private void CreateStartShortcut()
		{
			using (var link = new ShellLink())
			{
				link.Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FX.Mobile.DeveloperTools.Launcher.exe");
				link.Description = "Start Mobile Website";
				link.DisplayMode = ShellLink.LinkDisplayMode.edmNormal;
				link.Arguments = "\"/path:" + MobilePath + "\"";
				link.Save(Path.Combine(MobilePath, link.Description + ".lnk"));
			}
		}

		private void CreateIndex()
		{
			var indexManager = new IndexFileManager();
			indexManager.Packages = _completedPackageList;
			indexManager.CreateIndex(MobilePath);
		}

		private string GetResourceVersion()
		{
			switch (this.Version)
			{
				case MobileVersion.Version12:
					return "v1.2";
				case MobileVersion.Version20:
					return "v2.0.1";
				case MobileVersion.Version30:
					return "v3.0.4";
				case MobileVersion.Version31:
					return "v3.1.1";
				case MobileVersion.Version32:
					return "v3.2.1";
				case MobileVersion.Version33:
					return "v3.3.2";
				case MobileVersion.Version34:
					return "v3.4.2";
				case MobileVersion.Version35:
					return "v3.5";
				default:
					return "v3.5";
			}
		}

		private Action GetResourcePostAction(string projectDir)
		{
			// Mobile 3.4 and 3.5 require transpiling src into src-out. Requirements are node/npm and grunt-cli (npm install -g grunt-cli).
			// Open a command prompt and run "npm" or "grunt" to ensure they are on the PATH. The node installer should do this.
			Action noop = () => { };
			switch (this.Version)
			{
				case MobileVersion.Version34:
					return () =>
					{
						var startInfo = new ProcessStartInfo
						{
							WorkingDirectory = projectDir,
							FileName = "npm",
							Arguments = "install"
						};
						Process p = Process.Start(startInfo);
						p.WaitForExit();

						// grunt babel less
						startInfo.FileName = "grunt";
						startInfo.Arguments = "babel less";
						p = Process.Start(startInfo);
						p.WaitForExit();
					};
				case MobileVersion.Version35:
					return () =>
					{
						var startInfo = new ProcessStartInfo
						{
							WorkingDirectory = projectDir,
							FileName = "npm",
							Arguments = "install"
						};
						Process p = Process.Start(startInfo);
						p.WaitForExit();

						// npm run build
						startInfo.Arguments = "run build";
						p = Process.Start(startInfo);
						p.WaitForExit();
					};
				default:
					return noop;
			}
		}

		private string GetResourceVersionBare()
		{
			// Strips out the v:
			//  For some reason when you download a tag of "v3.1" from github, it strips out the v and the resulting download is 3.1.zip
			string version = this.GetResourceVersion().Replace("v", "");
			return version;
		}
	}

	public class MobileResourceInstallEventArgs : EventArgs
	{
		public string CurrentPackage { get; set; }
		public string Action { get; set; }
		public int ProgressPercentage { get; set; }
		public int ProgressCurrentFile { get; set; }
		public int ProgressTotalFiles { get; set; }
		public int StepNumber { get; set; }
		public int StepTotal { get; set; }
	}
}
