﻿using System;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using OpenMod.API;
using OpenMod.Common.Hotloading;
using Serilog;

namespace OpenMod.Core.Helpers
{
    [OpenModInternal]
    public static class AssemblyHelper
    {
        public static void CopyAssemblyResources(Assembly assembly, string baseDir, bool overwrite = false)
        {
            baseDir ??= string.Empty;

            var resourceNames = assembly.GetManifestResourceNames();

            if (resourceNames.Length > 0 && !Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            foreach (var resourceName in resourceNames)
            {
                if (resourceName.EndsWith("..directory"))
                {
                    continue;
                }

                var assemblyName = Hotloader.GetRealAssemblyName(assembly);

                if (!resourceName.Contains(assemblyName.Name + "."))
                {
                    Log.Warning($"{resourceName} does not contain assembly name in assembly: {assemblyName.Name}. <AssemblyName> and <RootNamespace> must be equal inside your plugins .csproj file.");
                }

                var regex = new Regex(Regex.Escape(assemblyName.Name + "."));
                var fileName = regex.Replace(resourceName, string.Empty, 1);

                var parts = fileName.Split('.');
                fileName = "";
                var path = assemblyName.Name + ".";
                foreach (var part in parts)
                {
                    path += part + ".";
                    using var tmpStream = assembly.GetManifestResourceStream(path + ".directory");

                    var isDirectory = tmpStream != null;
                    if (isDirectory)
                    {
                        fileName += part + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        fileName += part + ".";
                    }
                }

                var directory = Path.GetDirectoryName(fileName);

                if(directory != null)
                {
                    directory = Path.Combine(baseDir, directory);
                }

                if(directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var filePath = Path.Combine(baseDir, fileName);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                using var reader = new StreamReader(stream ?? throw new MissingManifestResourceException($"Couldn't find resource: {resourceName}"));
                var fileContent = reader.ReadToEnd();
                
                if (File.Exists(filePath) && !overwrite)
                {
                    continue;
                }
                
                File.WriteAllText(filePath, fileContent);
            }
        }
    }
}