using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DuplicateDestroyer
{
    static class Program
    {
        class FileSizeComparerDescending : IComparer<long>
        {
            public int Compare(long x, long y)
            {
                if (x < y) return 1;
                else if (x > y) return -1;
                else return 0;
            }
        }

        static bool Verbose;
        static bool DryRun;
        static bool AutoOldest;
        static bool AutoNewest;
        static bool FileRemoveException;
        static Dictionary<string, string> Files;
        static Dictionary<string, long> Sizes;
        static int FileCount;
        static string TargetDirectory;


        static void Main(string[] args)
        {
            Console.WriteLine("Duplicate Destroyer");
            Console.WriteLine("'Catastrophic Canon'");
            Console.WriteLine("Licenced under Tiny Driplet Licence (can be found at cloudchiller.net)");
            Console.WriteLine("Copyright, Copydrunk, Copypone (c) 2012-2014, Cloud Chiller");
            Console.WriteLine();

            if (args.Contains("-h"))
            {
                Console.WriteLine("HELP:");
                Console.WriteLine("-h       Show this help text.");
                Console.WriteLine("-v       Verbose mode");
                Console.WriteLine("-d       Dry run. Only check for duplicates, but don't actually remove them.");
                Console.WriteLine("-o       Automatically schedule the OLDEST file for keeping.");
                Console.WriteLine("-n       Automatically schedule the NEWEST file for keeping.");
                Console.WriteLine();
                Console.WriteLine("Omitting both -o and -n results in the user being queried about which file to keep.");
                Console.WriteLine("Using both -o and -n throws an error.");
                Console.WriteLine();

                Environment.Exit(0);
            }

            Verbose = args.Contains("-v");
            DryRun = args.Contains("-d");
            AutoOldest = args.Contains("-o");
            AutoNewest = args.Contains("-n");

            if (AutoOldest == true && AutoNewest == true)
            {
                Console.WriteLine("ERROR: Conflicting arguments.");
                Console.WriteLine("Please use either -o or -n, not both.");
                Console.WriteLine();

                Environment.Exit(3);
            }

            FileStream SizesFileStream = new FileStream(".dd_sizes", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            SizesFileStream.SetLength(0);
            SizesFile = new SizeFile(SizesFileStream);

            FileRemoveException = false;
            TargetDirectory = "..\\..";
            Directory.SetCurrentDirectory(TargetDirectory);
            TargetDirectory = Directory.GetCurrentDirectory();

            Console.WriteLine("Counting files and registering sizes...");
            Sizes = new Dictionary<string, long>(FileCount);
            List<string> Subfolders = new List<string>();
            Subfolders.Add(TargetDirectory);
            while (Subfolders.Count != 0)
            {
                // Read the files in the subfolders.
                ReadFileSizes(Subfolders[0], ref Subfolders);

                // The on-the-fly detected subfolders are added to the list while reading.
            }
            //ReadFileSizes(TargetDirectory, ref Subfolders);
            //StreamDump(SizesFile);
            foreach (SizeEntry se in SizesFile.GetRecords())
                Console.WriteLine("\tSize: " + se.Size + "\tCount: " + se.Count);

            Console.WriteLine(SizesFile.GetRecords().Sum(se => (long)se.Count).ToString() + " files found.");



            SizesFileStream.Dispose();
            Console.ReadLine();
            Environment.Exit(0);

            Console.Write("Counting files... ");
            FileCount = CountFiles(TargetDirectory);
            Console.WriteLine(Convert.ToString(FileCount) + " files found.");
            Files = new Dictionary<string, string>(FileCount);
            Console.WriteLine();

            Console.WriteLine("Measuring file sizes...");
            SearchSizes(TargetDirectory);
            Console.WriteLine();

            Console.Write("Analyzing sizes... ");
            SortedList<long, List<string>> PossibleDuplicates;
            AnalyzeSizes(out PossibleDuplicates);

            Console.Write(Convert.ToString(PossibleDuplicates.Count) + " unique file size");
            int duplicate_size_amount = 0;
            foreach (List<string> duplicates_of_a_size in PossibleDuplicates.Values)
            {
                foreach (string file in duplicates_of_a_size)
                {
                    duplicate_size_amount++;
                }
            }
            Console.WriteLine(" found for " + Convert.ToString(duplicate_size_amount) + " files.");
            Console.WriteLine();

            Console.WriteLine("Reading file contents...");
            System.Security.Cryptography.MD5CryptoServiceProvider mcsp = new System.Security.Cryptography.MD5CryptoServiceProvider();

            foreach (List<string> duplicated_size in PossibleDuplicates.Values)
            {
                foreach (string file in duplicated_size)
                {
                    Files.Add(file, CalculateHash(ref mcsp, file));
                }
            }
            Console.WriteLine();

            Console.WriteLine("Searching for true duplication... ");
            SortedList<string, List<string>> DuplicateHashesList;
            AnalyzeFilelist(out DuplicateHashesList);

            Console.Write(Convert.ToString(DuplicateHashesList.Count) + " unique content");
            int duplicate_file_amount = 0;
            foreach (List<string> duplicates_of_a_hash in DuplicateHashesList.Values)
            {
                foreach (string file in duplicates_of_a_hash)
                {
                    duplicate_file_amount++;
                }
            }
            Console.WriteLine(" duplicated across " + Convert.ToString(duplicate_file_amount) + " files.");
            Console.WriteLine();

            SortedList<string, List<string>> RemoveLists = new SortedList<string, List<string>>(DuplicateHashesList.Count);
            foreach (List<string> duplicate_hash_files in DuplicateHashesList.Values)
            {
                SortedList<DateTime, string> ordered = new SortedList<DateTime, string>(duplicate_hash_files.Count);

                foreach (string file in duplicate_hash_files)
                {
                    FileInfo fi = new FileInfo(file);
                    DateTime lastAccessTime = fi.LastAccessTime;

                    // Well this is a hack.
                    // Whatever. It will just stop throwing those
                    // System.ArgumentException: An entry with the same key already exists.
                    while (ordered.ContainsKey(lastAccessTime))
                    {
                        lastAccessTime = lastAccessTime.AddMilliseconds(1);
                    }

                    ordered.Add(lastAccessTime, file);
                }

                List<string> files_to_remove = null;

                if (AutoOldest == true && AutoNewest == false)
                {
                    files_to_remove = ordered.Values.ToList();

                    Console.Write("Automatically scheduled to keep OLDEST file: ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(files_to_remove[0]);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(files_to_remove[0]));
                    }

                    files_to_remove.RemoveAt(0);
                }
                else if (AutoOldest == false && AutoNewest == true)
                {
                    files_to_remove = ordered.Values.ToList();

                    Console.Write("Automatically scheduled to keep NEWEST file: ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(files_to_remove[files_to_remove.Count - 1]);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(files_to_remove[files_to_remove.Count - 1]));
                    }

                    files_to_remove.RemoveAt(files_to_remove.Count - 1);
                }
                else if (AutoOldest == false && AutoNewest == false)
                {
                    files_to_remove = SelectFileToKeep(
                        DuplicateHashesList.Keys[DuplicateHashesList.IndexOfValue(duplicate_hash_files)], ordered);
                }

                if (files_to_remove.Count != 0)
                {
                    RemoveLists.Add(DuplicateHashesList.Keys[DuplicateHashesList.IndexOfValue(duplicate_hash_files)], files_to_remove);
                }
            }
            Console.WriteLine();

            if (DryRun == false)
            {
                foreach (List<string> removes in RemoveLists.Values)
                {
                    if (Verbose == true)
                    {
                        Console.WriteLine("Removing duplicates of hash: " + RemoveLists.Keys[RemoveLists.IndexOfValue(removes)]);
                    }

                    foreach (string file in removes)
                    {
                        RemoveFile(file);
                    }

                    if (Verbose == true)
                    {
                        Console.WriteLine();
                    }
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            if (FileRemoveException == true)
            {
                Environment.Exit(2);
            }
            else if (FileRemoveException == false)
            {
                Environment.Exit(0);
            }
        }

        static void AnalyzeSizes(out SortedList<long, List<string>> possibleDuplicates)
        {
            IEnumerable<long> duplicate_sizes =
                Sizes.GroupBy(f => f.Value).Where(v => v.Count() > 1).Select(s => s.Key);

            possibleDuplicates = new SortedList<long, List<string>>(new FileSizeComparerDescending());

            foreach (long size in duplicate_sizes)
            {
                IEnumerable<string> duplicates = Sizes.Where(d => d.Value == size).Select(s => s.Key);
                possibleDuplicates.Add(size, duplicates.ToList());
            }
        }

        static void AnalyzeFilelist(out SortedList<string, List<string>> duplicateLists)
        {
            IEnumerable<string> duplicate_hashes =
                Files.GroupBy(f => f.Value).Where(v => v.Count() > 1).Select(h => h.Key);

            duplicateLists = new SortedList<string, List<string>>();

            foreach (string hash in duplicate_hashes)
            {
                IEnumerable<string> duplicates = Files.Where(d => d.Value == hash).Select(s => s.Key);
                duplicateLists.Add(hash, duplicates.ToList());
            }
        }

        static List<string> SelectFileToKeep(string hash, SortedList<DateTime, string> fileList)
        {
            bool selection_success = false;
            uint choice = 0;

            while (!selection_success)
            {
                Console.WriteLine("=================================");
                Console.WriteLine("The following files are duplicates of each other: ");

                if (Verbose == true)
                {
                    Console.WriteLine("(Hash: " + hash + ")");
                }

                foreach (KeyValuePair<DateTime, string> duplicate in fileList)
                {
                    Console.Write((fileList.IndexOfValue(duplicate.Value) + 1) + ". ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(duplicate.Value);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(duplicate.Value));
                    }

                    Console.Write("Last modified: " +
                        duplicate.Key.ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat));

                    if (fileList.IndexOfValue(duplicate.Value) == 0)
                    {
                        Console.Write(" (oldest)");
                    }

                    if (fileList.IndexOfValue(duplicate.Value) == fileList.Count - 1)
                    {
                        Console.Write(" (newest)");
                    }

                    Console.WriteLine();
                }

                if (DryRun == false)
                {
                    Console.WriteLine((fileList.Count + 1) + ". Take no action");

                    try
                    {
                        Console.Write("Select the file you want TO KEEP. (The rest will be deleted.): ");
                        choice = Convert.ToUInt32(Console.ReadLine());

                        selection_success = true;

                        if (choice < 1 || choice > fileList.Count + 1)
                        {
                            throw new Exception("You entered an invalid option. Please choose an option printed on the menu.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unable to parse choice. An exception happened: " + ex.Message);
                        selection_success = false;
                    }
                }
                else if (DryRun == true)
                {
                    selection_success = true;
                    choice = 0;
                }
            }

            if (DryRun == false)
            {
                if (choice != fileList.Count + 1 && choice != 0)
                {
                    Console.Write("Scheduled to keep file: ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(fileList.Values[(int)choice - 1]);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(fileList.Values[(int)choice - 1]));
                    }

                    fileList.RemoveAt((int)choice - 1);
                }
                else if (choice == fileList.Count + 1 || choice == 0)
                {
                    Console.WriteLine("All files will be kept.");
                    fileList.Clear();
                }
            }

            return fileList.Values.ToList();
        }

        static string CalculateHash(ref System.Security.Cryptography.MD5CryptoServiceProvider mcsp, string file)
        {
            string md5b64;

            if (Verbose == true)
            {
                Console.Write("Reading file " + Path.GetFileName(file) + "...");
            }

            byte[] md5bytes;
            using (FileStream stream = File.OpenRead(file))
            {
                md5bytes = mcsp.ComputeHash(stream);
                md5b64 = Convert.ToBase64String(md5bytes);
            }

            if (Verbose == true)
            {
                Console.WriteLine(" Hash: " + md5b64 + ".");
            }

            return md5b64;
        }

        static void RemoveFile(string file)
        {
            Console.Write("File " + Path.GetFileName(file) + " ...");

            try
            {
                //File.Delete(file);
                //Console.WriteLine(" Deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(" ERROR: Unable to delete. An exception happened: " + ex.Message);
                FileRemoveException = true;
            }
        }

        static void ReadFileSizes(string directory, ref List<string> subfolderList)
        {
            if (Verbose)
                Console.WriteLine("Reading contents of " + directory);
            
            try
            {
                int insertIndex = 0;
                foreach (string path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string fullPath = Path.GetFullPath(path).Replace(Directory.GetCurrentDirectory(), String.Empty).TrimStart('\\');
                    //string fullPath = Path.GetFullPath(path);
                    //string fullPath = directory + Path.DirectorySeparatorChar + path;

                    // Skip some files which should not be access by the program
                    if (Path.GetFullPath(path) == SizesFile.Stream.Name)
                        continue;

                    try
                    {
                        if (Directory.Exists(fullPath))
                        {
                            // If it is a directory, add it to the list of subfolders to check later on
                            if (Verbose)
                                Console.WriteLine(fullPath + " is a subfolder.");

                            // Add the found subfolders to the beginning of the list, but keep their natural order
                            subfolderList.Insert(++insertIndex, fullPath);
                        }
                        else if (File.Exists(fullPath))
                        {
                            if (Verbose)
                                Console.Write("Measuring " + fullPath + "...");

                            // If it is a file, register its path and size.
                            FileInfo fi = new FileInfo(fullPath);
                            Sizes.Add(path, fi.Length);

                            SizeEntry entry = new SizeEntry();
                            long position = 0;
                            bool known = SizesFile.GetRecord((ulong)fi.Length, out entry, out position);
                            entry.Size = (ulong)fi.Length;
                            if (!known) // Need to reset the entry's count because GetRecord gives
                                        // undefined value if the entry is not found.
                                entry.Count = 0;
                            entry.Count++;
                            SizesFile.WriteRecord(entry);

                            if (Verbose)
                                Console.WriteLine(" Size: " + fi.Length.ToString() + " bytes.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("The path " + fullPath + " could not be accessed, because:");
                        Console.ResetColor();
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The directory " + directory + " could not be accessed, because:");
                Console.ResetColor();
                Console.WriteLine(ex.Message);
            }

            subfolderList.Remove(directory);
        }

        static void SearchSizes(string directory)
        {
            try
            {
                foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (Verbose == true)
                    {
                        Console.Write("Measuring " + Path.GetFileName(f) + "...");
                    }

                    FileInfo fi = new FileInfo(f);

                    Sizes.Add(f, fi.Length);

                    if (Verbose == true)
                    {
                        Console.WriteLine(" Size: " + Convert.ToString(fi.Length) + " bytes.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to check folder. An exception happened: " + ex.Message);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        static int CountFiles(string directory)
        {
            int c = 0;
            try
            {
                foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    c++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to count files. An exception happened: " + ex.Message);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
            return c;
        }
    }
}