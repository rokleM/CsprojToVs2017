using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Project2015To2017.Reading;
using Project2015To2017.Transforms;

[assembly: InternalsVisibleTo("Project2015To2017Tests")]

namespace Project2015To2017
{
	public class ProjectConverter
	{
		private static readonly IReadOnlyList<ITransformation> _transformationsToApply = new ITransformation[]
		{
			new PackageReferenceTransformation(),
			new AssemblyReferenceTransformation(),
			new RemovePackageAssemblyReferencesTransformation(),
			new RemovePackageImportsTransformation(),
			new FileTransformation(),
			new NugetPackageTransformation()
		};

		public static IEnumerable<Definition.Project> Convert(string target, IProgress<string> progress)
		{
			if (Path.GetExtension(target).Equals(".sln", StringComparison.OrdinalIgnoreCase))
			{
				progress.Report("Solution parsing started.");
				using (var reader = new StreamReader(File.OpenRead(target)))
				{
					var file = new FileInfo(target);
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						if(!line.Trim().StartsWith("Project(")) {
							continue;	
						}
						
						var projectPath = line.Split('"').FirstOrDefault(x => x.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
						if (projectPath != null)
						{
							progress.Report("Project found: " + projectPath);
							var fullPath = Path.Combine(file.Directory.FullName, projectPath);
							if (!File.Exists(fullPath))
							{
								progress.Report("Project file not found at: " + fullPath);
							}
							else
							{
								yield return ProcessFile(fullPath, progress);
							}
						}
					}
				}

				yield break;
			}

			// Process all csprojs found in given directory
			if (!Path.GetExtension(target).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
			{
				var projectFiles = Directory.EnumerateFiles(target, "*.csproj", SearchOption.AllDirectories).ToArray();
				if (projectFiles.Length == 0)
				{
					progress.Report($"Please specify a project file.");
					yield break;
				}
				progress.Report($"Multiple project files found under directory {target}:");
				progress.Report(string.Join(Environment.NewLine, projectFiles));
				foreach (var projectFile in projectFiles)
				{
					yield return ProcessFile(projectFile, progress);
				}

				yield break;
			}

			// Process only the given project file
			yield return ProcessFile(target, progress);
		}

		private static Definition.Project ProcessFile(string filePath, IProgress<string> progress)
		{
			var file = new FileInfo(filePath);
			if (!Validate(file, progress))
			{
				return null;
			}

			var project = new ProjectReader().Read(filePath, progress);
			if (project == null)
			{
				return null;
			}

			foreach (var transform in _transformationsToApply)
			{
				transform.Transform(project, progress);
			}

			return project;
		}

		internal static bool Validate(FileInfo file, IProgress<string> progress)
		{
			if (!file.Exists)
			{
				progress.Report($"File {file.FullName} could not be found.");
				return false;
			}

			if (file.IsReadOnly)
			{
				progress.Report($"File {file.FullName} is readonly, please make the file writable first (checkout from source control?).");
				return false;
			}

			return true;
		}
	}
}
