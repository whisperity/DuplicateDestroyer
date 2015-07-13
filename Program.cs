using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DuplicateDestroyer
{
    static class Program
    {
        static bool Verbose;
        static bool DryRun;
        static bool AutoOldest;
        static bool AutoNewest;
        static bool FileRemoveException;

        static string TargetDirectory;
        static ulong SizeCount, FileCount;

        static SizeFile SizesFile;
        static PathFile PathsFile;
        static HashFile HashesFile;
        static FileStream FilesToRemove;
        static StreamWriter DuplicateFileLog;

        static void Main(string[] args)
        {
            Console.WriteLine("Duplicate Destroyer");
            Console.WriteLine("'Devastating Desert'");
            Console.WriteLine("Licenced under Tiny Driplet Licence (can be found at cloudchiller.net)");
            Console.WriteLine("Copyright, Copydrunk, Copypone (c) 2012-2014, Cloud Chiller");
            Console.WriteLine();

            if (args.Contains("-h"))
            {
                Console.WriteLine("HELP:");
                Console.WriteLine("-h       Show this help text");
                Console.WriteLine("-v       Verbose mode");
                Console.WriteLine("-d       Dry run/discovery - Only check for duplicates, but don't actually remove them");
                Console.WriteLine("-o       Automatically keep the OLDEST of the files");
                Console.WriteLine("-n       Automatically keep the NEWEST of the files");
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
            SizeCount = 0;
            FileCount = 0;

            if (AutoOldest == true && AutoNewest == true)
            {
                Console.WriteLine("ERROR: Conflicting arguments.");
                Console.WriteLine("Please use either -o or -n, not both.");
                Console.WriteLine();

                Environment.Exit(3);
            }

            FileStream SizesFileStream = null;
            FileStream PathsFileStream = null;
            FileStream HashesFileStream = null;
            FileStream DuplicateLogFileStream = null;
            try
            {
                SizesFileStream = new FileStream(".dd_sizes", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                SizesFileStream.SetLength(0);

                PathsFileStream = new FileStream(".dd_files", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                PathsFileStream.SetLength(0);

                HashesFileStream = new FileStream(".dd_hashes", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                HashesFileStream.SetLength(0);

                FilesToRemove = new FileStream(".dd_remove", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                FilesToRemove.SetLength(0);

                DuplicateLogFileStream = new FileStream("duplicates_" + DateTime.Now.ToString().Replace(":", "_") + ".log", FileMode.OpenOrCreate,
                    FileAccess.Write, FileShare.None);
                DuplicateLogFileStream.SetLength(0);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Was unable to create the program's datafiles.");
                Console.ResetColor();
                Console.WriteLine("Please make sure the folder " + Directory.GetCurrentDirectory() + " is writable.");
                Console.WriteLine("The following error happened: " + ex.Message);

                Environment.Exit(1);
            }

            SizesFile = new SizeFile(SizesFileStream);
            PathsFile = new PathFile(PathsFileStream);
            HashesFile = new HashFile(HashesFileStream);
            DuplicateFileLog = new StreamWriter(DuplicateLogFileStream);

            FileRemoveException = false;
            TargetDirectory = Directory.GetCurrentDirectory();

            {
                Console.Write("Counting files and measuring sizes... " + (Verbose ? "\n" : String.Empty));
                List<string> Subfolders = new List<string>();
                Subfolders.Add(TargetDirectory);
                while (Subfolders.Count != 0)
                {
                    // Read the files in the subfolders.
                    ReadFileSizes(Subfolders[0], ref Subfolders);
                    // The on-the-fly detected subfolders are added to the list while reading.
                }
                SizesFile.Stream.Flush(true);
                PathsFile.Stream.Flush(true);
                Console.WriteLine((!Verbose ? "\n" : String.Empty) + FileCount + " files found.");
                Console.WriteLine();
            }

            {
                Console.Write("Analysing sizes... " + (Verbose ? "\n" : String.Empty));
                AnalyseSizes();
                SizesFile.Stream.Flush(true);
                PathsFile.Stream.Flush(true);
                Console.WriteLine((!Verbose ? "\n" : String.Empty) + SizeCount + " unique file size found for " + FileCount + " files.");
                Console.WriteLine();
            }

            //{
            //    // Remove entries from the PathsFile physically which were logically removed (marked deleted) in the previous step
            //    if (Verbose)
            //    {
            //        Console.WriteLine("Removing knowledge about files I don't need to check.");
            //        Console.WriteLine("(This is an internal maintenance run to speed up further operations.)");
            //    }
            //    PathsFile.Consolidate(new SizeFileAligner(Program.AlignSizeFilePointers));
            //    PathsFile.Stream.Flush(true);
            //    if (Verbose)
            //        Console.WriteLine();
            //}

            {
                Console.Write("Reading file contents... " + (Verbose ? "\n" : String.Empty));
                MD5CryptoServiceProvider mcsp = new MD5CryptoServiceProvider();
                ulong _hashesReadCount = 0;
                foreach (SizeEntry duplicated_size in SizesFile.GetRecords())
                {
                    if (Verbose)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Reading files of " + duplicated_size.Size + " size");
                        Console.ResetColor();
                    }
                    // For each size entry, iterate the path list
                    PathEntry entry;
                    long position = duplicated_size.FirstPath;

                    while (position != -1)
                    {
                        if (PathsFile.GetRecordAt(position, out entry))
                        {
                            string hash = String.Empty;
                            try
                            {
                                hash = CalculateHash(ref mcsp, entry.Path);
                                ++_hashesReadCount;
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("The file " + entry.Path + " could not be checked, because:");
                                Console.ResetColor();
                                Console.WriteLine(ex.Message);
                            }

                            if (!String.IsNullOrEmpty(hash))
                                entry.Hash = hash;
                            else
                                // Mark this record "deleted" so it won't be checked for hash duplication
                                entry.Deleted = true;

                            PathsFile.WriteRecordAt(entry, position);
                            VisualGlyph(_hashesReadCount);
                            position = entry.NextRecord; // Jump to the next record in the chain
                        }
                    }
                }
                PathsFile.Stream.Flush(true);
                Console.WriteLine((!Verbose ? "\n" : String.Empty) + _hashesReadCount + " files read.");
            }

            {
                Console.Write("Searching for true duplication... " + (Verbose ? "\n" : String.Empty));
                long UniqueHashCount, DuplicatedFileCount;
                AnalyseFilelist(out UniqueHashCount, out DuplicatedFileCount);
                HashesFile.Stream.Flush(true);
                Console.WriteLine((!Verbose ? "\n" : String.Empty) + UniqueHashCount + " unique content duplicated across " + DuplicatedFileCount + " files.");
                Console.WriteLine();

                Console.WriteLine();
                Console.WriteLine("Please select which files you wish to remove.");
                long dealtWithCount = 0;
                while (dealtWithCount < UniqueHashCount)
                {
                    // We go through every hash entry and prompt the user to decide which file to remove
                    HashesFile.Stream.Seek(0, SeekOrigin.Begin);
                    SizeHashEntry she = new SizeHashEntry();
                    PathEntry etr = new PathEntry();
                    long pos = 0;

                    while (pos != -1)
                    {
                        // Get the next duplicated hash
                        pos = HashesFile.GetNextRecord(out she);
                        if (pos != -1)
                        {
                            // Iterate the hash pointers...
                            foreach (HashPointers ptr in she.Pointers)
                            {
                                if (ptr.FileEntries.Count == 0)
                                    continue;

                                // Select which file the user wants to keep
                                List<int> fileIDsToKeep;
                                bool userDecided = SelectFilesToKeep(ptr, out fileIDsToKeep);

                                if (!DryRun)
                                {
                                    if (!userDecided)
                                        Console.WriteLine("Didn't make a decision. You will be asked later on.");
                                    else
                                    {
                                        ++dealtWithCount;

                                        if (fileIDsToKeep.Count == ptr.FileEntries.Count)
                                            Console.WriteLine("Selected to keep all files.");
                                        else if (fileIDsToKeep.Count > 0)
                                        {
                                            if (!AutoOldest && !AutoNewest)
                                            {
                                                foreach (int id in fileIDsToKeep)
                                                {
                                                    Console.Write("Selected to  ");
                                                    Console.ForegroundColor = ConsoleColor.White;
                                                    Console.Write("KEEP");
                                                    Console.ResetColor();
                                                    Console.Write("  ");

                                                    PathsFile.GetRecordAt(ptr.FileEntries[id - 1], out etr);
                                                    Console.WriteLine(etr.Path);
                                                }

                                                foreach (int id in Enumerable.Range(1, ptr.FileEntries.Count).Except(fileIDsToKeep))
                                                {
                                                    Console.Write("Selected to ");
                                                    Console.ForegroundColor = ConsoleColor.Red;
                                                    Console.Write("DELETE");
                                                    Console.ResetColor();
                                                    Console.Write(" ");

                                                    PathsFile.GetRecordAt(ptr.FileEntries[id - 1], out etr);
                                                    Console.WriteLine(etr.Path);

                                                    byte[] pathLine = Encoding.UTF8.GetBytes(etr.Path + StreamWriter.Null.NewLine);
                                                    FilesToRemove.Write(pathLine, 0, pathLine.Length);
                                                }
                                            }
                                        }
                                        else if (fileIDsToKeep.Count == 0)
                                        {
                                            Console.WriteLine("All files will be deleted:");

                                            foreach (long offset in ptr.FileEntries)
                                            {
                                                PathsFile.GetRecordAt(offset, out etr);
                                                Console.WriteLine(etr.Path);

                                                byte[] pathLine = Encoding.UTF8.GetBytes(etr.Path + StreamWriter.Null.NewLine);
                                                FilesToRemove.Write(pathLine, 0, pathLine.Length);
                                            }
                                        }

                                        FilesToRemove.Flush();
                                    }
                                }
                                else
                                    ++dealtWithCount;
                            }
                        }
                    }
                }
                Console.WriteLine();
            }

            {
                Console.Write("Removing all scheduled files... " + (Verbose ? "\n" : String.Empty));
                uint _filesRemoved = 0;
                if (DryRun)
                    Console.WriteLine("Won't remove files in dry-run/discovery mode.");
                else
                {
                    FilesToRemove.Seek(0, SeekOrigin.Begin);
                    string path;

                    if (FilesToRemove.Length > 0) // Only if there are files to be removed
                    {
                        using (StreamReader sr = new StreamReader(FilesToRemove))
                        {
                            path = sr.ReadLine();
                            if (RemoveFile(path))
                                ++_filesRemoved;
                        }
                    }
                }
                Console.WriteLine((!Verbose ? "\n" : String.Empty) + _filesRemoved + " files deleted successfully.");
            }

            SizesFileStream.Dispose();
            PathsFileStream.Dispose();
            HashesFileStream.Dispose();
            //FilesToRemove.Dispose();
            DuplicateFileLog.Dispose();

            // Cleanup
            //File.Delete(".dd_sizes");
            //File.Delete(".dd_files");
            //File.Delete(".dd_hashes");
            //File.Delete(".dd_remove");

            if (FileRemoveException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("One or more files could not be deleted.");
                Console.ResetColor();
            }

            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();

            if (FileRemoveException)
                Environment.Exit(2);
            else
                Environment.Exit(0);
        }

        static void ReadFileSizes(string directory, ref List<string> subfolderList)
        {
            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Reading contents of " + directory);
                Console.ResetColor();
            }
            
            try
            {
                int insertIndex = 0;
                foreach (string path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string relativePath = Path.GetFullPath(path).Replace(Directory.GetCurrentDirectory(), String.Empty).TrimStart('\\');

                    // Skip some files which should not be access by the program
                    if (Path.GetFullPath(path) == SizesFile.Stream.Name || Path.GetFullPath(path) == PathsFile.Stream.Name
                        || Path.GetFullPath(path) == HashesFile.Stream.Name || Path.GetFullPath(path) == FilesToRemove.Name
                        || Path.GetFullPath(path) == ((FileStream)DuplicateFileLog.BaseStream).Name)
                        continue;

                    // Skip files if they are in a Subversion structure
                    // SVN saves a "pristine" copy of every file, and this makes every SVNd file to be marked as duplicate.
                    if (relativePath.Contains(".svn\\pristine") || relativePath.Contains(".svn\\entries")
                        || relativePath.Contains(".svn\\format"))
                        continue;

                    try
                    {
                        if (Directory.Exists(relativePath))
                        {
                            // If it is a directory, add it to the list of subfolders to check later on
                            if (Verbose)
                                Console.WriteLine(relativePath + " is a subfolder.");

                            // Add the found subfolders to the beginning of the list, but keep their natural order
                            subfolderList.Insert(++insertIndex, relativePath);
                        }
                        else if (File.Exists(relativePath))
                        {
                            if (Verbose)
                                Console.Write("Measuring " + relativePath + "...");

                            // If it is a file, register its size and the count for its size
                            FileInfo fi = new FileInfo(relativePath);

                            try
                            {
                                SizeEntry entry = new SizeEntry();
                                long position = 0;
                                bool known = SizesFile.GetRecord((ulong)fi.Length, out entry, out position);
                                entry.Size = (ulong)fi.Length;
                                if (!known)
                                {
                                    // Need to reset the entry's count because GetRecord gives
                                    // undefined value if the entry is not found.
                                    entry.Count = 0;
                                    ++SizeCount;

                                    // The new size record currently has no associated PathEntry records in the path file.
                                    entry.FirstPath = -1;
                                    entry.LastPath = -1;
                                    entry.HashEntry = -1;
                                }
                                entry.Count++;

                                // Also register its path
                                PathEntry pathRec = new PathEntry(relativePath);
                                long pathWrittenPosition;
                                if (entry.LastPath != -1)
                                {
                                    PathEntry previousLastEntry = new PathEntry();
                                    PathsFile.GetRecordAt(entry.LastPath, out previousLastEntry);
                                    pathWrittenPosition = PathsFile.AddAfter(previousLastEntry, pathRec, entry.LastPath);
                                }
                                else
                                {
                                    pathWrittenPosition = PathsFile.WriteRecord(pathRec);

                                    entry.FirstPath = pathWrittenPosition;
                                }

                                entry.LastPath = pathWrittenPosition;
                                SizesFile.WriteRecord(entry);

                                if (Verbose)
                                    Console.WriteLine(" Size: " + fi.Length + " bytes.");

                                ++FileCount;
                                VisualGlyph(FileCount);
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("There was an error registering " + relativePath + " in the databank.");
                                Console.ResetColor();
                                Console.WriteLine(ex.Message);

                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("This indicates an error with the databank. Execution cannot continue.");
                                Console.ResetColor();
                                Console.ReadLine();
                                Environment.Exit(1);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("The path " + relativePath + " could not be accessed, because:");
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

        static void AnalyseSizes()
        {
            // After the file sizes are read, we eliminate every size which refers to one file
            // As there could not be duplicates that way.

            // Go from the back to make the least write overhead when a record is deleted
            ulong _analysedSizes = 0;
            for (long i = SizesFile.RecordCount - 1; i >= 0; --i)
            {
                SizeEntry rec = new SizeEntry();
                try
                {
                    rec = SizesFile.GetRecordByIndex(i);
                }
                catch (Exception)
                {
                    //Console.ForegroundColor = ConsoleColor.Yellow;
                    //Console.WriteLine("Couldn't get, because");
                    //Console.ResetColor();
                    //Console.WriteLine(ex.Message);

                    continue;
                }

                if (rec.Count == 1 || rec.Count == 0)
                {
                    if (Verbose)
                        if (rec.Count == 0)
                            Console.Write("No files with " + rec.Size + " size.");
                        else if (rec.Count == 1)
                            Console.Write("There's only 1 file with " + rec.Size + " size.");

                    SizesFile.DeleteRecord(rec.Size);
                    --SizeCount;
                    --FileCount;

                    // Delete every record (there should be 1) that is associated with this size... they'll no longer be needed.
                    if (rec.FirstPath != -1 && rec.LastPath != -1)
                        if (rec.FirstPath != rec.LastPath)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("An error happened while analysing sizes:");
                            Console.ResetColor();
                            Console.WriteLine("Count for size " + rec.Size + " is 1, but there appears to be multiple associated files to exist.");

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("This indicates an error with the databank. Execution cannot continue.");
                            Console.ResetColor();
                            Console.ReadLine();
                            Environment.Exit(1);
                        }
                        else
                        {
                            PathEntry entry;
                            PathsFile.GetRecordAt(rec.FirstPath, out entry);
                            PathsFile.DeleteRecord(entry, rec.FirstPath);

                            if (Verbose)
                                Console.Write(" Ignoring " + entry.Path);
                        }

                    if (Verbose)
                        Console.WriteLine();
                }

                VisualGlyph(++_analysedSizes);
            }

            SizesFile.Stream.Flush(true);
            PathsFile.Stream.Flush(true);
        }

        static string CalculateHash(ref MD5CryptoServiceProvider mcsp, string path)
        {
            if (Verbose)
                Console.Write("Reading file " + path + "...");

            byte[] md5bytes;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                md5bytes = mcsp.ComputeHash(stream);

            StringBuilder sb = new StringBuilder(32);
            for (int i = 0; i < md5bytes.Length; ++i)
                sb.Append(md5bytes[i].ToString("x2"));

            if (Verbose)
                Console.WriteLine(" Hash: " + sb.ToString() + ".");

            return sb.ToString();
        }

        static void AnalyseFilelist(out long UniqueHashCount, out long DuplicatedFileCount)
        {
            // Go through every size entry and build the hash lists
            UniqueHashCount = 0;
            DuplicatedFileCount = 0;

            for (long i = 0; i < SizesFile.RecordCount; ++i)
            {
                SizeEntry se = SizesFile.GetRecordByIndex(i);

                SizeHashEntry she = new SizeHashEntry()
                {
                    Pointers = new List<HashPointers>()
                };

                // Get the files with the current size
                PathEntry entry;
                long pos = se.FirstPath;
                while (pos != -1)
                {
                    if (!PathsFile.GetRecordAt(pos, out entry))
                        break;

                    if (!entry.Deleted)
                    {
                        // Get the file pointer list for the current hash
                        HashPointers curHash = she.Pointers.Where(p => p.Hash == entry.Hash).FirstOrDefault();
                        if (curHash.FileEntries == null)
                        {
                            // This indicates that this is a new hash, allocate the List for it to prevent a null reference
                            curHash.Hash = entry.Hash;
                            curHash.FileEntries = new List<long>();

                            she.Pointers.Add(curHash);
                            ++UniqueHashCount;
                        }
                        curHash.FileEntries.Add(pos); // A file with this hash is found at this position
                        ++DuplicatedFileCount;
                    }
                    else
                        if (Verbose)
                            Console.WriteLine("Skipping file " + entry.Path + ", I was unable to check it.");

                    VisualGlyph((ulong)DuplicatedFileCount);
                    pos = entry.NextRecord;
                }

                // Remove hashes which is had by only one file
                int hashesRemoved = she.Pointers.RemoveAll(hp => hp.FileEntries.Count == 1);
                UniqueHashCount -= hashesRemoved;
                DuplicatedFileCount -= hashesRemoved;

                // Write the current hash's data to the datafile
                if (she.Pointers.Count > 0)
                {
                    long shePosition = HashesFile.WriteRecord(she);

                    // Update the size table to save where the hash map begins
                    se.HashEntry = shePosition;
                    SizesFile.WriteRecordAt(se, i * SizeEntry.RecordSize);
                }
            }
        }

        static bool SelectFilesToKeep(HashPointers ptr, out List<int> toKeep)
        {
            bool selectionSuccess = false;
            toKeep = new List<int>(Enumerable.Range(1, ptr.FileEntries.Count));
            bool decided = false;
            int choice = 0;

            bool canAutoSelect = false;
            List<int> oldestIDs = new List<int>();
            List<int> newestIDs = new List<int>();
            {
                // Read and register the timestamp when the files were last accessed
                // The oldest file (lowest timestamp) will be on the top of the list
                SortedList<DateTime, List<int>> timeStamps = new SortedList<DateTime, List<int>>(ptr.FileEntries.Count);
                PathEntry entry = new PathEntry();
                int currentID = 1;
                foreach (long offset in ptr.FileEntries)
                {
                    PathsFile.GetRecordAt(offset, out entry);
                    FileInfo fi = new FileInfo(entry.Path);

                    IEnumerable<DateTime> tsRegistered = timeStamps.Select(ts => ts.Key)
                        .Where(ts => ts.Date == fi.LastAccessTime.Date
                            && ts.Hour == fi.LastAccessTime.Hour
                            && ts.Minute == fi.LastAccessTime.Minute
                            && ts.Second == fi.LastAccessTime.Second);
                    if (tsRegistered.Count() == 1)
                        timeStamps[tsRegistered.First()].Add(currentID);
                    else
                    {
                        List<int> idList = new List<int>(1);
                        idList.Add(currentID);
                        timeStamps.Add(fi.LastAccessTime, idList);
                    }
                    ++currentID;
                }

                // If the oldest and newest files are the same, don't select any of them
                if (timeStamps.Count == 1 && (AutoOldest || AutoNewest))
                    Console.WriteLine("The files' age are equal. Unable to select oldest and newest ones.");
                else
                {
                    oldestIDs.AddRange(timeStamps.First().Value);
                    newestIDs.AddRange(timeStamps.Last().Value);
                    canAutoSelect = true;
                }
            }

            while (!selectionSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(new String('-', Console.WindowWidth - 1));
                DuplicateFileLog.WriteLine(new String('-', 24));
                Console.WriteLine("The following " + ptr.FileEntries.Count + " files are duplicate of each other");
                DuplicateFileLog.WriteLine("The following " + ptr.FileEntries.Count + " files are duplicate of each other");
                Console.ResetColor();
                if (Verbose)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Hash: " + ptr.Hash);
                    DuplicateFileLog.WriteLine("Hash: " + ptr.Hash);
                    Console.ResetColor();
                }

                if (!DryRun)
                {
                    Console.Write("Files marked ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("[ KEEP ]");
                    Console.ResetColor();
                    Console.Write(" will be kept. Files marked ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[DELETE]");
                    Console.ResetColor();
                    Console.WriteLine(" will be deleted.");

                    if (!AutoNewest && !AutoOldest)
                        Console.WriteLine("Please select the files you wish to keep or delete.");
                    else
                        if (!canAutoSelect)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Was unable to automatically select " + (AutoOldest ? "oldest" : "newest") + " file to keep");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Automatically selecting the " + (AutoOldest ? "OLDEST" : "NEWEST") + " file to keep");
                            Console.ResetColor();
                            toKeep.Clear();

                            if (AutoOldest)
                                toKeep.AddRange(oldestIDs);
                            else if (AutoNewest)
                                toKeep.AddRange(newestIDs);
                        }
                }

                // Print the file list with a choice
                int menuId = 1;
                PathEntry etr = new PathEntry();
                int totalLog10ofEntries = (int)Math.Floor(Math.Log10((double)ptr.FileEntries.Count)) + 1;
                if (totalLog10ofEntries < 3) // Make sure "-1." can be printed
                    totalLog10ofEntries = 3;
                foreach (long offset in ptr.FileEntries)
                {
                    PathsFile.GetRecordAt(offset, out etr);

                    // Create a little menu for the user to give a choice

                    // The length of the choice option printed, how many characters it takes in base 10
                    int strCurrentLength = (int)Math.Floor(Math.Log10((double)menuId)) + 1; // 0-9: 1 long, 10-99: 2 long, etc.
                    ++strCurrentLength; // The '.' (dot) takes up another character

                    if (!DryRun)
                    {
                        if (toKeep.Contains(menuId))
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write("[ KEEP ] ");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("[DELETE] ");
                            Console.ResetColor();
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(new String(' ', totalLog10ofEntries - strCurrentLength + 1) + menuId + ". ");

                    bool oldestOrNewest = false;
                    if (oldestIDs.Contains(menuId))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("[OLDEST] ");
                        DuplicateFileLog.Write("[OLDEST] ");
                        oldestOrNewest = true;
                    }
                    else if (newestIDs.Contains(menuId))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("[NEWEST] ");
                        DuplicateFileLog.Write("[NEWEST] ");
                        oldestOrNewest = true;
                    }

                    DuplicateFileLog.Write(new String(' ', (!oldestOrNewest ? 9 : 0) +
                        totalLog10ofEntries - strCurrentLength + 1) + menuId + ". ");
                    Console.ResetColor();
                    Console.WriteLine(etr.Path);
                    DuplicateFileLog.WriteLine(etr.Path);

                    ++menuId;
                }

                if (!AutoNewest && !AutoOldest && !DryRun)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("  [DONE] " + new String(' ', totalLog10ofEntries - 2 + 1) + "0. ");
                    Console.ResetColor();
                    Console.WriteLine("Finalise the choices");

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  [SKIP] " + new String(' ', totalLog10ofEntries - 3 + 1) + "-1. ");
                    Console.ResetColor();
                    Console.WriteLine("Keep everything for now, decide later");

                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write("  [NUKE] " + new String(' ', totalLog10ofEntries - 3 + 1) + "-2. ");
                    Console.Write("Delete ALL FILES!");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                // Read the user's choice
                if (!AutoNewest && !AutoOldest && !DryRun)
                {
                    Console.WriteLine("Please select an option from above. If you select a file, its status will be togged between keep and delete.");
                    Console.Write("? ");
                    try
                    {
                        choice = Convert.ToInt32(Console.ReadLine());
                        selectionSuccess = true; // Attempt to say that the user successfully selected

                        if (choice >= menuId || choice < -2)
                            throw new ArgumentOutOfRangeException("The entered choice is invalid.");
                    }
                    catch (Exception)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Invalid input. ");
                        Console.ResetColor();
                        Console.WriteLine("The enterd input is not a number or is out of range. Please select from the presented choices!");

                        selectionSuccess = false;
                    }
                }
                else
                    // If the user decided to automatically keep oldest or newest file, the selection was successful.
                    // Either the oldest or the newest file were selected, or if not, all files were selected to be kept.
                    selectionSuccess = true;

                if (selectionSuccess)
                {
                    // Change the buffer list of which files to keep or don't
                    if (choice >= 1)
                    {
                        if (toKeep.Contains(choice))
                            toKeep.Remove(choice);
                        else
                            toKeep.Add(choice);

                        selectionSuccess = false; // Let the user make further changes
                        decided = false;
                    }
                    else if (choice == -2)
                    {
                        toKeep.Clear();
                        decided = true;
                    }
                    else
                        decided = (choice == 0); // If -1, tell that the user hasn't decided
                }
            }

            toKeep.Sort();
            return decided;
        }

        static bool RemoveFile(string path)
        {
            if (Verbose)
                Console.Write("File " + path + "...");

            bool success = true;
            try
            {
                if (Verbose)
                {
                    if (!DryRun)
                    {
                        File.Delete(path);
                        Console.WriteLine(" Deleted.");
                    }
                    else
                        Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                // Print the path here if it was not printed earlier
                if (!Verbose)
                    Console.Write("File " + path + "...");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" ERROR: Unable to delete. An exception happened: " + ex.Message);
                Console.ResetColor();
                success = false;
                FileRemoveException = true;
            }

            return success;
        }

        #region Stream methods
        private const int MaxBufferSize = 2 << 12; // 2^13 = 8192, 8 KiB

        public static void MoveEndPart(Stream Stream, long position, long difference)
        {
            // Move all the bytes from position to the end of the stream to a new position.
            // (In either direction.)
            if (position < 0)
                throw new ArgumentOutOfRangeException("The initial position from where the content should be moved can't be negative.");
            else if (position > Stream.Length)
                throw new ArgumentOutOfRangeException("The initial position from where the content should be moved must be inside the stream.");

            if (position == Stream.Length && difference < 0)
            {
                // Shrink the stream by the given size
                Stream.SetLength(Stream.Length + difference); // actually a - :)
                return;
            }

            if (difference == 0 || position == Stream.Length)
                return; // Noop, nothing to move.

            if (position + difference < 0)
                throw new ArgumentOutOfRangeException("Requested to move bytes before the beginning of the stream.");

            // First, we calculate how many bytes are there to be moved.
            long fullByteCount = Stream.Length - position;

            // This blob is to be chunked up based on MaxBufferSize.
            // For every move operation, a such buffer will be read and written out.
            Stream.Seek(position, SeekOrigin.Begin);
            long currentPosition = position;
            long newPosition = position + difference; // Where the moved bytes will begin after the move

            if (fullByteCount == 0)
                return; // Noop, nothing to move.

            // Calculate a buffer size to use
            int bufferSize;
            if (fullByteCount > MaxBufferSize)
                bufferSize = MaxBufferSize;
            else
                bufferSize = (int)Math.Pow(2, Math.Floor(Math.Log(fullByteCount, 2)));
            byte[] buffer = new byte[bufferSize];

            long byteCount = 0; // The count of "done" bytes we already moved
            long readPosition = -1, writePosition = -1; // Two pointers where the next read and write operation will work.

            if (difference > 0)
            {
                // If we are moving FORWARD, the first chunk to be read is the LAST in the file.
                // We start from the right.
                readPosition = Stream.Length - bufferSize;
                writePosition = readPosition + difference;

                // Also, if we are moving forward, the stream has to be increased in size.
                Stream.SetLength(Stream.Length + difference);
            }
            else if (difference < 0)
            {
                // If we are moving BACKWARDS, the first chunk to be read is the FIRST
                // We start from the left.
                readPosition = position;
                writePosition = readPosition + difference; // (well, actually a - here :) )
            }

            int bytesToRead = 0;
            while (byteCount < fullByteCount)
            {
                buffer = new byte[bufferSize]; // TODO: this isn't needed, just debug cleanup.
                // If the number of remaining bytes would be smaller than the buffer size, read a partial buffer.
                if (fullByteCount - byteCount < bufferSize)
                    bytesToRead = Convert.ToInt32(fullByteCount - byteCount);
                else
                    bytesToRead = bufferSize;

                // Read the chunk.
                Stream.Seek(readPosition, SeekOrigin.Begin);
                Stream.Read(buffer, 0, bytesToRead);

                // And write it.
                Stream.Seek(writePosition, SeekOrigin.Begin);
                Stream.Write(buffer, 0, bytesToRead);
                Stream.Flush();

                // Align the two intermediate pointers to the new locations for the next operation.
                // (The read and write positions should always be having a distance of 'difference' between each other.)
                if (difference > 0)
                {
                    // If we are moving the bytes FORWARD, the read head moves BACKWARDS, because we started from the right.

                    // Read and write positions could underflow this way.
                    // If the last remaining chunk is smaller than the buffer and would begin before the initial start position...
                    // we correct it.
                    if (readPosition - bytesToRead < position)
                    {
                        readPosition = position;
                        writePosition = position + difference;
                    }
                    else
                    {
                        readPosition -= bytesToRead;
                        writePosition -= bytesToRead;
                    }
                }
                else if (difference < 0)
                {
                    // If we are moving the bytes BACKWARD, the read and write moves FORWARD, because we started form the left.
                    readPosition += bytesToRead;
                    writePosition += bytesToRead;
                }

                byteCount += bytesToRead; // Mark the currently done bytes... 'done'
            }

            Stream.Flush();
            if (difference < 0)
                // If the move operation was to shrink, we eliminate the overhead at the end of the file.
                Stream.SetLength(Stream.Length + difference); // (still a - :) )
        }
        #endregion

        #region Helper functions
        // Helper function to align the pointers in a SizeFile data if a PathFile Consolidate() is happening
        internal delegate void SizeFileAligner(ref List<KeyValuePair<long, long>> moveOffsets);
        private static void AlignSizeFilePointers(ref List<KeyValuePair<long, long>> moveOffsets)
        {
            // When PathFile Consolidate() happens, records in the PathFile usually move towards the beginning of the file
            // This invalidates the pointer in the SizeFile, so they need also to be aligned

            // moveOffset's KeyValuePairs' layout: Key is the offset which was moved and Value is by how much it was moved
            // Every pointer pointing to a position further than a Key must be "pulled back" by Value

            if (moveOffsets.Count == 0)
                return;

            for (long i = 0; i < SizesFile.RecordCount; ++i)
            {
                SizeEntry se = SizesFile.GetRecordByIndex(i);

                // For each size file, check if its pointers point behind a position key
                if (se.FirstPath >= moveOffsets[0].Key || se.LastPath >= moveOffsets[0].Key)
                {
                    long firstPtrMoveBy = 0;
                    long lastPtrMoveBy = 0;

                    foreach (KeyValuePair<long, long> kv in moveOffsets)
                    {
                        if (se.FirstPath >= kv.Key)
                            firstPtrMoveBy += kv.Value;

                        if (se.LastPath >= kv.Key)
                            lastPtrMoveBy += kv.Value;
                    }

                    if (firstPtrMoveBy != 0)
                        se.FirstPath -= firstPtrMoveBy;

                    if (lastPtrMoveBy != 0)
                        se.LastPath -= lastPtrMoveBy;

                    SizesFile.WriteRecordAt(se, i * SizeEntry.RecordSize);
                }
            }
        }
        #endregion

        // Indicate the number of files counted with visual glyphs.
        // Each marks a ten-fold increasement. So # is 10s, & is 100s, @ is 1000s, etc.
        static char[] CountVisualGlyphs = new char[] { '#', '?', '&', '@', '*', '$', '%' };
        private static void VisualGlyph(ulong counter)
        {
            // If verbose mode is turned off, give some visual hint for the user based on the counter
            if (!Verbose && CountVisualGlyphs.Length > 0)
            {
                if (counter % 10 == 0)
                    Console.Write(CountVisualGlyphs[0]);

                for (byte i = 2; i < CountVisualGlyphs.Length + 1; ++i)
                    if (counter % Math.Pow(10, i) == 0)
                    {
                        Console.Write(new String('\b', 10));
                        Console.Write(new String(' ', 10));
                        Console.Write(new String('\b', 10));
                        Console.Write(CountVisualGlyphs[i - 1]);
                    }
            }
        }
    }
}