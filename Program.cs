using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace BinDeployer
{
    class Program
    {
        private static bool _skipConfig;


        static void Main(string[] args)
        {
#if DEBUG
            //some test parameters
            args = new[] { "/s", @"C:\Temp\test_deploy\live1", "/t", @"C:\Temp\test_deploy\new1" };
#endif

            //todo: test required arguments and show help text if bad args


            //get arguments
            var sourceFolders = new List<string>(4);
            var targetFolders = new List<string>(4);
            var validationIssues = new List<string>(4);
            var excludedPatterns = new List<string>(4);

            var argSection = "";
            foreach (var s in args)
            {
                if (s.StartsWith("/"))
                {
                    argSection = s.Replace("/", "").ToLower();
                    continue;
                }

                if (argSection.StartsWith("s"))
                {
                    sourceFolders.Add(s);
                    continue;
                }

                if (argSection.StartsWith("t"))
                {
                    targetFolders.Add(s);
                }

                if (argSection.StartsWith("x"))
                {
                    excludedPatterns.Add(s);
                }
            }

            //validate number of source and target folders
            if (sourceFolders.Count < 1 || sourceFolders.Count != targetFolders.Count)
            {
                WriteLine("The number of sources must be at least one and there must be the same number of targets as sources", ConsoleStatus.Alert);
                return;
            }


            //relative paths in the source?
            string assemblyPath = null;
            for (var i = 0; i < sourceFolders.Count; i++)
            {
                if (sourceFolders[i].Contains(":")) continue; //assume absolute path

                assemblyPath = assemblyPath ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                sourceFolders[i] = Path.Combine(assemblyPath,  sourceFolders[i]);
            }

            //validate that paths exists and that we can write to the target folders
            foreach (var sourceFolder in sourceFolders.Where(sourceFolder => !Directory.Exists(sourceFolder) && !File.Exists(sourceFolder)))
            {
                WriteLine(String.Format("The source: {0} does not exists.", sourceFolder), ConsoleStatus.Warning);
                return;
            }


            //if a source is a file and the corresponding target is a folder - adjust the target to the corresponding file (so we don't back up the full folder)
            for (var i = 0; i < sourceFolders.Count; i++)
            {
                if (!File.Exists(sourceFolders[i])) continue; //not a file

                if (File.Exists(targetFolders[i])) continue; //ok - the corresponging target is a corresponding file

                if (Directory.Exists(targetFolders[i]))
                {
                    var targetFile = Path.Combine(targetFolders[i], Path.GetFileName(sourceFolders[i]));
                    //keep the target as a folder if the file doesn't exists i.e new file
                    if (File.Exists(targetFile)) targetFolders[i] = targetFile;
                }
            }



            //todo: test if it seems to be the correct folders?



            //are we excluding "*,config?"
            var feedback = "";
            if (!excludedPatterns.Contains("*.config"))
            {
                WriteLine("You're not excluding *.config files.", ConsoleStatus.Warning);
                WriteLine("Do you want to exclude *.config files? (y/n)", ConsoleStatus.Warning);
                feedback = Console.ReadLine() ?? "";
                if (feedback.StartsWith("y"))
                {
                    excludedPatterns.Add("*.config");
                }
            }

            WriteLine(String.Format("Replacing: {0}", String.Join(", ", targetFolders)));
            WriteLine(String.Format("with: {0}", String.Join(", ", sourceFolders)));

            WriteLine("But first we'll do a little backup...");
            WriteLine("Continue? (y/n)");
            feedback = Console.ReadLine() ?? "";
            if (!feedback.ToLower().StartsWith("y")) return;
            //perform the backup
            var backupFolders = DoBackup(targetFolders);





            //perform copy
            WriteLine("");
            WriteLine("The files are backed up, continue with copy into live site? (y/n)", ConsoleStatus.Success);
            feedback = Console.ReadLine() ?? "";
            if (!feedback.ToLower().StartsWith("y")) return;


            WriteLine("Deploying files...");
            DoCopy(sourceFolders, targetFolders, excludedPatterns);

            WriteLine("Are you happy?", ConsoleStatus.Success);
            WriteLine("Being not happy will make me copy the backup up files back over your new files", ConsoleStatus.Success);
            WriteLine("So, are you happy? Are you? (y/n)", ConsoleStatus.Success);
            feedback = Console.ReadLine() ?? "";

            if (feedback.ToLower().StartsWith("y"))
            {
                WriteLine("Pleasure doing business with you", ConsoleStatus.Success);
                WriteLine("Press any key to exit");
            }
            else
            {
                DoCopy(backupFolders, targetFolders, excludedPatterns);

                WriteLine("The files are restored (any new files from your source will remain in the target dirs)", ConsoleStatus.Success);
                WriteLine("Press any key to exit");
            }

            
            Console.ReadKey();


        }

        private static void DoCopy(List<string> sourceFolders, List<string> targetFolders, List<string> excludePatterns )
        {
            for (int i = 0; i < sourceFolders.Count; i++)
            {
                var sourceFolder = sourceFolders[i];
                var targetFolder = targetFolders[i];
                WriteLine(String.Format("Copying files from {0} to {1}", sourceFolder, targetFolder));
                CopyFolder(sourceFolder, targetFolder, excludePatterns);
            }
        }


        private static List<string> DoBackup(List<string> targetFolders)
        {
            //perform backup
            //var backupFolder = Path.Combine(System.IO.Path.GetTempPath(), "BinDeployer", DateTime.Now.ToString("yyMMddhhmmss"), Guid.NewGuid().ToString());
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var backupFolder = Path.Combine(assemblyPath, "backup");
            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);


            WriteLine("Creating backup folder...");
            var backupFolders = new List<string>(targetFolders.Count);
            var multitarget = targetFolders.Count > 1;
            foreach (var targetFolder in targetFolders)
            {
                //todo:if just files - no need to create folders for the targets...

                var targetBackupPath = multitarget ? Path.Combine(backupFolder, Path.GetFileName(targetFolder))
                    : backupFolder;
                if (!Directory.Exists(targetBackupPath)) Directory.CreateDirectory(targetBackupPath);

                if (File.Exists(targetFolder)) targetBackupPath = Path.Combine(targetBackupPath, Path.GetFileName(targetFolder));

                backupFolders.Add(targetBackupPath);
            }

            WriteLine("Backing up files...");
            DoCopy(targetFolders, backupFolders, null); //don't exclude files when backing up

            return backupFolders;

        }


        private static void CopyFolder(string sourceFolder, string targetFolder, List<string> excludePatterns)
        {
            //files?
            if (File.Exists(sourceFolder))
            {
                WriteLine(String.Format("Copy {0} to {1}", sourceFolder, targetFolder));
                File.Copy(sourceFolder, targetFolder, true);
                return;
            }

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourceFolder, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourceFolder, targetFolder));

            //Copy all the files & Replaces any files with the same name
            foreach (string sourceFile in Directory.GetFiles(sourceFolder, "*",
                SearchOption.AllDirectories))
            {

                if (excludePatterns != null && ExcludeFile(sourceFile, excludePatterns))
                {
                    WriteLine(String.Format("Excluding file {0}", sourceFile));
                    continue;
                }

                var targetFile = sourceFile.Replace(sourceFolder, targetFolder);
                WriteLine(String.Format("Copy {0} to {1}", sourceFile, targetFile));
                File.Copy(sourceFile, targetFile, true);

            }
                
        }

        private static bool ExcludeFile(string fileName, List<string> excludePatterns)
        {
            foreach (var excludePattern in excludePatterns)
            {
                //todo, take into account start of etc
                if (fileName.Contains(excludePattern.Replace("*", ""))) return true;
            }

            return false;
        }


        private enum ConsoleStatus
        {
            Default,
            Warning,
            Alert,
            Success
        }


        private static void WriteLine(string text, ConsoleStatus status = ConsoleStatus.Default)
        {
            switch (status)
            {
                    case ConsoleStatus.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case ConsoleStatus.Alert:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case ConsoleStatus.Success:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
            }

            Console.WriteLine(text);

            Console.ResetColor();
        }
    }
}
