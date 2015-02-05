using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinDeployer
{
    class Program
    {
        private static bool _skipConfig;


        static void Main(string[] args)
        {
#if DEBUG
            //some test parameters
            args = new[] {"/s", @"c:\dev\ConsoleApplication1", "/t", @"c:\Temp\test_deploy"};
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
                Console.WriteLine("The number of source folders must be at least one and there must be the same number of target folders as source folders");
                return;
            }


            //validate that paths exists and that we can write to the target folders
            foreach (var sourceFolder in sourceFolders.Where(sourceFolder => !Directory.Exists(sourceFolder)))
            {
                Console.WriteLine("The source folder: {0} does not exists.", sourceFolder);
                return;
            }


            //todo: test if it seems to be the correct folders? - perform a test merge...



            //are we excluding "*,config?"
            var feedback = "";
            if (!excludedPatterns.Contains("*.config"))
            {
                Console.WriteLine("You're not excluding *.config files.");
                Console.WriteLine("Do you want to exclude *.config files? (y/n)");
                feedback = Console.ReadLine() ?? "";
                if (feedback.StartsWith("y"))
                {
                    excludedPatterns.Add("*.config");
                }
            }

            Console.WriteLine("Replacing folders: {0}", String.Join(", ", targetFolders));
            Console.WriteLine("with folders: {0}", String.Join(", ", sourceFolders));

            Console.WriteLine("But first we'll do a little backup...");
            Console.WriteLine("Continue? (y/n)");
            feedback = Console.ReadLine() ?? "";
            if (!feedback.ToLower().StartsWith("y")) return;
            //perform the backup
            var backupFolders = DoBackup(targetFolders);





            //perform copy
            Console.WriteLine("");
            Console.WriteLine("The files are backed up, continue with copy into live site? (y/n)");
            feedback = Console.ReadLine() ?? "";
            if (!feedback.ToLower().StartsWith("y")) return;


            Console.WriteLine("Deploying files...");
            DoCopy(sourceFolders, targetFolders, excludedPatterns);

            Console.WriteLine("Are you happy?");
            Console.WriteLine("Being not happy will make me copy the backup up files back over your new files");
            Console.WriteLine("So, are you happy? Are you? (y/n)");
            feedback = Console.ReadLine() ?? "";

            if (feedback.ToLower().StartsWith("y"))
            {
                Console.WriteLine("Pleasure doing business with you");
                Console.WriteLine("Press any key to exit");
            }
            else
            {
                DoCopy(backupFolders, targetFolders, excludedPatterns);

                Console.WriteLine("The files are restored (any new files from your source will remain in the target dirs)");
                Console.WriteLine("Press any key to exit");
            }

            
            Console.ReadKey();


        }

        private static void DoCopy(List<string> sourceFolders, List<string> targetFolders, List<string> excludePatterns )
        {
            for (int i = 0; i < sourceFolders.Count; i++)
            {
                var sourceFolder = sourceFolders[i];
                var targetFolder = targetFolders[i];
                Console.WriteLine("Copying files from {0} to {1}", sourceFolder, targetFolder);
                CopyFolder(sourceFolder, targetFolder, excludePatterns);
            }
        }


        private static List<string> DoBackup(List<string> targetFolders)
        {
            //perform backup
            var backupFolder = Path.Combine(System.IO.Path.GetTempPath(), "BinDeployer", DateTime.Now.ToString("yyMMddhhmmss"), Guid.NewGuid().ToString());
            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);


            Console.WriteLine("Creating backup folder...");
            var backupFolders = new List<string>(targetFolders.Count);

            foreach (var targetFolder in targetFolders)
            {
                var targetBackupPath = Path.Combine(backupFolder, Path.GetFileName(targetFolder));
                if (!Directory.Exists(targetBackupPath)) Directory.CreateDirectory(targetBackupPath);
                
                backupFolders.Add(targetBackupPath);
            }

            Console.WriteLine("Backing up files...");
            DoCopy(targetFolders, backupFolders, null); //don't exclude files when backing up

            return backupFolders;

        }


        private static void CopyFolder(string sourceFolder, string targetFolder, List<string> excludePatterns)
        {
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
                    Console.WriteLine("Excluding file {0}", sourceFile);
                    continue;
                }

                var targetFile = sourceFile.Replace(sourceFolder, targetFolder);
                Console.WriteLine("Copy {0} to {1}", sourceFile, targetFile);
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
    }
}
