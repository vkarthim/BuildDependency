// Copyright (c) 2014-2016 Eberhard Beilharz
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BuildDependency.Artifacts;
using BuildDependency.Tasks.Tools;
using BuildDependency.Tools;
using Microsoft.Build.Framework;

namespace BuildDependency.Tasks
{
	public class Dependencies: Microsoft.Build.Utilities.Task
	{
		private class LogHelper: ILog
		{
			private readonly object _syncObj = new object();

			public void LogError(string message, params object[] messageArgs)
			{
				lock (_syncObj)
				{
					Console.Write("ERROR: ");
					Console.WriteLine(message, messageArgs);
				}
			}

			public void LogMessage(string message, params object[] messageArgs)
			{
				lock (_syncObj)
				{
					Console.WriteLine(message, messageArgs);
				}
			}
		}

		public Dependencies()
		{
			ExceptionLogging.Initialize("4bae82b8c647df7fea786dbaecb4b351");

			WorkingDir = string.Empty;
			RunAsync = true;
		}

		public string DependencyFile
		{ get; set; }

		public string JobsFile
		{ get; set; }

		[Required]
		public bool UseDependencyFile
		{ get; set; }

		public string WorkingDir
		{ get; set; }

		public bool RunAsync
		{ get; set; }

		/// <summary>
		/// Only relevant when <see cref="UseDependencyFile"/> is <c>true</c>.
		/// If <c>true</c> don't delete the generated JobsFile after finishing (and re-use
		/// on the next run); otherwise delete the generated JobsFile at the end of the task
		/// (and re-generate on the next build).
		/// </summary>
		/// <value><c>true</c> if keep jobs file; otherwise, <c>false</c>.</value>
		public bool KeepJobsFile
		{ get; set; }

		public override bool Execute()
		{
			ILog logHelper;
			if (Log != null)
				logHelper = new LogWrapper(Log);
			else
				logHelper = new LogHelper();

			var version = Utils.GetVersion("BuildDependency.Tasks");
			logHelper.LogMessage("Dependencies task version {0} ({1}):", version.Item1,
				version.Item2);

			if (UseDependencyFile && string.IsNullOrEmpty(DependencyFile))
			{
				logHelper.LogError("DependencyFile is not specified, but I was told to use it.");
				return false;
			}
			if (UseDependencyFile && !File.Exists(DependencyFile))
			{
				logHelper.LogError("Can't find DependencyFile {0}", DependencyFile);
				return false;
			}
			if (!UseDependencyFile && string.IsNullOrEmpty(JobsFile))
			{
				logHelper.LogError("JobsFile is not specified, but I was told to use it.");
				return false;
			}
			if (!UseDependencyFile && !File.Exists(JobsFile))
			{
				logHelper.LogError("Can't find JobsFile {0}", JobsFile);
				return false;
			}

			if (string.IsNullOrEmpty(JobsFile))
				JobsFile = Path.ChangeExtension(DependencyFile, ".files");

			var replaceJobsFile = UseDependencyFile && !(KeepJobsFile && File.Exists(JobsFile)) ||
				!File.Exists(JobsFile);
			if (UseDependencyFile && DependencyFile != null && JobsFile != null && File.Exists(JobsFile))
			{
				var depFile = new FileInfo(DependencyFile);
				var jobsFile = new FileInfo(JobsFile);
				if (depFile.LastWriteTimeUtc > jobsFile.LastWriteTimeUtc)
					replaceJobsFile = true;
			}
			var retVal = true;
			if (RunAsync)
			{
				retVal = ExecuteAsync(replaceJobsFile, logHelper).Result;
			}
			else
			{
				if (replaceJobsFile)
				{
					var artifactTemplates = BuildDependency.DependencyFile.LoadFile(DependencyFile, logHelper);
					BuildDependency.JobsFile.WriteJobsFile(JobsFile, artifactTemplates);
				}

				var jobs = BuildDependency.JobsFile.ReadJobsFile(JobsFile);

				foreach (var job in jobs.OfType<DownloadFileJob>())
				{
					retVal &= job.Execute(logHelper, WorkingDir).WaitAndUnwrapException();
				}
				foreach (var job in jobs.OfType<UnzipFilesJob>())
				{
					retVal &= job.Execute(logHelper, WorkingDir).WaitAndUnwrapException();
				}
			}

			if (UseDependencyFile && !KeepJobsFile && JobsFile != null)
				File.Delete(JobsFile);

			return retVal;
		}

		private async Task<bool> ExecuteAsync(bool replaceJobsFile, ILog logHelper)
		{
			var retVal = true;
			if (replaceJobsFile)
			{
				var artifactTemplates = BuildDependency.DependencyFile.LoadFile(DependencyFile, logHelper);
				await BuildDependency.JobsFile.WriteJobsFileAsync(JobsFile, artifactTemplates);
			}

			var jobs = BuildDependency.JobsFile.ReadJobsFile(JobsFile);

			var tasks = jobs.OfType<DownloadFileJob>().Select(job => job.Execute(logHelper, WorkingDir)).ToArray();
			foreach (var task in tasks)
			{
				retVal &= await task;
			}
			tasks = jobs.OfType<UnzipFilesJob>().Select(job => job.Execute(logHelper, WorkingDir)).ToArray();
			foreach (var task in tasks)
			{
				retVal &= await task;
			}
			return retVal;
		}
	}
}

