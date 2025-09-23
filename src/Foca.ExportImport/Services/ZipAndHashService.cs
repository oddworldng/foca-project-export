using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Foca.ExportImport.Services
{
	public sealed class ZipAndHashService
	{
		public void CreateZipFromFolder(string sourceFolder, string destinationZip, CompressionLevel compressionLevel)
		{
			if (File.Exists(destinationZip)) File.Delete(destinationZip);
			ZipFile.CreateFromDirectory(sourceFolder, destinationZip, compressionLevel, includeBaseDirectory: false);
		}

		public void ExtractZipToFolder(string sourceZip, string destinationFolder)
		{
			ExtractZipSafely(sourceZip, destinationFolder);
		}

		public void ExtractZipSafely(string sourceZip, string destinationFolder)
		{
			if (!Directory.Exists(destinationFolder)) Directory.CreateDirectory(destinationFolder);
			using (var archive = ZipFile.OpenRead(sourceZip))
			{
				foreach (var entry in archive.Entries)
				{
					var fullPath = Path.GetFullPath(Path.Combine(destinationFolder, entry.FullName));
					if (!fullPath.StartsWith(Path.GetFullPath(destinationFolder), System.StringComparison.OrdinalIgnoreCase))
						throw new IOException("Entrada ZIP con ruta no válida");
					if (entry.FullName.EndsWith("/"))
					{
						Directory.CreateDirectory(fullPath);
						continue;
					}
					Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
					using (var es = entry.Open())
					using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						es.CopyTo(fs);
					}
				}
			}
		}

		public string ComputeSha256(string filePath)
		{
			using (var sha256 = SHA256.Create())
			using (var stream = File.OpenRead(filePath))
			{
				var hash = sha256.ComputeHash(stream);
				return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
			}
		}

		public string ComputeSha256(Stream input)
		{
			using (var sha256 = SHA256.Create())
			{
				var hash = sha256.ComputeHash(input);
				return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
			}
		}
	}
}
