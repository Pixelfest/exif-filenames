using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ExifFilenames
{
	class Program
	{
		private static bool _go;
		private static Regex _fileRegex;

		static void Main(string[] args)
		{
			// Initialize file replace stuff
			string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
			_fileRegex = new Regex($"[{Regex.Escape(regexSearch)}]");

			Option<DirectoryInfo> dirOption = new("--dir")
			{
				Description = "The path to rename the files in.",
				Required = false
			};

			Option<bool> goOption = new("--go")
			{
				Description = "Whether the files should actually be renamed.",
				Required = false
			};

			RootCommand rootCommand = new("Renames files to include date taken and camera details.");
			rootCommand.Options.Add(goOption);
			rootCommand.Options.Add(dirOption);

			rootCommand.SetAction(parseResult =>
			{
				if (parseResult.Errors.Count > 0)
				{
					foreach (var parseError in parseResult.Errors)
					{
						Log.Error(parseError.Message);
					}

					return 1;
				}

				DirectoryInfo directoryInfo;

				if (parseResult.GetValue(dirOption) is { } dirInfo)
				{
					directoryInfo = dirInfo;
				}
				else
				{
					directoryInfo = new DirectoryInfo(Environment.CurrentDirectory);
				}

				if (parseResult.GetValue(goOption))
					_go = true;

				if (_go)
				{
					Log.Warning("Filenames in the folder below will be changed:");
					Log.Warning($"{directoryInfo.FullName}");
					Console.ReadLine();
				}
				else
				{
					Log.Success("Filenames in this folder will NOT actually be changed:");
					Log.Success($"{directoryInfo.FullName}");
					Console.ReadLine();
				}

				SetFileNames(directoryInfo);

				Log.Info("Done");

				return 0;
			});

			ParseResult parseResult = rootCommand.Parse(args);
			parseResult.Invoke();
		}

		private static void SetFileNames(DirectoryInfo parent)
		{
			int count = 0;

			foreach (FileInfo file in parent.GetFiles())
			{
				Log.Info();

				switch (file.Extension.ToLowerInvariant())
				{
					case ".jpg":
						count += MoveImage(file);
						break;
					case ".png":
						count += MoveImage(file);
						break;
					default:
						Log.Warning($"Unsupported format ({file.Extension}): {file.Name}");
						break;
				}
			}

			Log.Success($"A total of {count} files were renamed.");

			/*
			if (recursive)
			{
				foreach (var child in parent.EnumerateDirectories())
				{
					Log.Info($"DIRECTORY: {child.FullName}");

					SetFileNames(child);
				}
			}
		*/
		}

		private static int MoveImage(FileInfo file)
		{
			Log.Trace("========================================");
			Log.Info($"Processing file: {file.Name}");

			if (file.Length < 30 * 1024)
			{
				Log.Warning("Skipping, file too small");
				return 0;
			}

			DateTime dateTimeOriginal = DateTime.MinValue;
			DateTime dateTime = DateTime.MinValue;
			string make = null;
			string model = null;

			var directories = ImageMetadataReader.ReadMetadata(file.FullName);
			var exifSubIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
			var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

			if (exifSubIfd != null)
			{
				exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out dateTimeOriginal);
				Log.Trace($"- TagDateTimeOriginal: {dateTimeOriginal}");

				exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTime, out dateTime);
				Log.Trace($"- TagDateTime: {dateTime}");

				make = exifSubIfd0.GetDescription(ExifDirectoryBase.TagMake);
				Log.Trace($"- Make: {make}");

				model = exifSubIfd0.GetDescription(ExifDirectoryBase.TagModel);
				Log.Trace($"- Model: {model}");
			}

			DateTime dateCreated = file.CreationTime;
			Log.Trace($"- CreationTime: {dateCreated}");
			DateTime dateModified = file.LastWriteTime;
			Log.Trace($"- LastWriteTime: {dateModified}");

			DateTime fileDateTime = dateModified < dateCreated ? dateModified : dateCreated;

			StringBuilder fileNameBuilder = new StringBuilder();

			DateTime date = DateTime.MinValue;
			if (dateTime != DateTime.MinValue)
				date = dateTime;
			if (dateTimeOriginal != DateTime.MinValue)
				date = dateTimeOriginal;
			if (date == DateTime.MinValue)
				date = fileDateTime;

			fileNameBuilder.Append($"{date:yyyy-MM-dd HH.mm.ss}");

			if (!String.IsNullOrWhiteSpace(make) && !String.IsNullOrWhiteSpace(model))
			{
				fileNameBuilder.Append($" - {String.Join(' ', make, model)}");
			}

			// Set the target directory
			//var targetFolder = Path.Join(pathTarget, date.Year.ToString("0000"), date.Month.ToString("00"));

			// Create the directory if it doesn't exist
			//if (!Directory.Exists(targetFolder))
			//	Directory.CreateDirectory(targetFolder);

			int number = 1;
			string newFullFileName = NewFileName(file.DirectoryName, fileNameBuilder, number, file.Extension);
			string onlyFileName = Path.GetFileName(newFullFileName);

			Log.Info($"Saving file as: {onlyFileName}");

			if (onlyFileName.Equals(file.Name))
			{
				Log.Warning($"File was already renamed, no change: {onlyFileName}");
				return 0;
			}

			while (File.Exists(newFullFileName))
			{
				Log.Info($"File exists: {newFullFileName}");

				// File already has the correct name
				if(file.FullName == newFullFileName)
					return 0;

				number++;
				newFullFileName = NewFileName(file.DirectoryName, fileNameBuilder, number, file.Extension);

				if (number > 9999)
					throw new Exception("Too many similar files");

				onlyFileName = Path.GetFileName(newFullFileName);
				Log.Info($"Saving file as: {onlyFileName}");
			}

			var oldFileName = file.Name;

			if (_go)
			{
				file.MoveTo(newFullFileName);
				Log.Success($"Moved: {oldFileName} => {onlyFileName}");
			}
			else
			{
				Log.Success($"Not moved: {oldFileName} => {onlyFileName}");
			}

			return 1;
		}

		private static string NewFileName(string targetFolder, StringBuilder fileNameBuilder, int number, string extension)
		{
			string fileName = $"{fileNameBuilder} - {number:00000}.{extension.TrimStart('.')}";
			fileName = _fileRegex.Replace(fileName, "");

			fileName = fileName.Replace("NIKON CORPORATION NIKON D5200", "Nikon D5200");
			fileName = fileName.Replace("samsung", "Samsung");

			return Path.Join(targetFolder, fileName);
		}
	}
}
