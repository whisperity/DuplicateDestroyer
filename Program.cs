using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

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

        //static Dictionary<string, long> Sizes;
        //static int FileCount;
        static string TargetDirectory;

        static SizeFile SizesFile;
        static ulong SizeCount, FileCount;

        static PathFile PathsFile;

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
            SizeCount = 0;
            FileCount = 0;

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

            FileStream PathsFileStream = new FileStream(".dd_files", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            PathsFileStream.SetLength(0);
            PathsFile = new PathFile(PathsFileStream);


            FileRemoveException = false;
            TargetDirectory = Directory.GetCurrentDirectory();

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

            Console.Write("Analysing sizes... ");
            AnalyseSizes();
            Console.Write(SizeCount + " unique file size");
            Console.WriteLine(" found for " + FileCount + " files.");
            Console.WriteLine();

            Console.WriteLine("\n\nPrevious operation continues...");
            //Console.ReadLine();
            //Environment.Exit(0);


            Console.WriteLine("Reading file contents...");
            MD5CryptoServiceProvider mcsp = new MD5CryptoServiceProvider();

            foreach (SizeEntry duplicated_size in SizesFile.GetRecords())
            {
                // For each size entry, iterate the path list
                PathEntry entry;
                long position = duplicated_size.FirstPath;

                while (position != -1)
                {
                    if (PathsFile.GetRecordAt(position, out entry))
                    {
                        string hash = CalculateHash(ref mcsp, entry.Path);

                        entry.Hash = hash;
                        PathsFile.WriteRecordAt(entry, position);

                        position = entry.NextRecord; // Jump to the next record in the chain
                    }
                }
            }
            Console.WriteLine();

            Console.WriteLine("Searching for true duplication... ");
            SortedList<string, List<string>> DuplicateHashesList;
            AnalyseFilelist(out DuplicateHashesList);

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

            SizesFileStream.Dispose();
            PathsFileStream.Dispose();

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

        static void AnalyseFilelist(out SortedList<string, List<string>> duplicateLists)
        {
            duplicateLists = new SortedList<string, List<string>>();

            foreach (SizeEntry se in SizesFile.GetRecords())
            {
                PathEntry firstEntry;
                PathsFile.GetRecordAt(se.FirstPath, out firstEntry);

                // TODO: obviously, this will also go into a file backend!
                // I really don't like var, but
                // System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Linq.IGrouping<string,DuplicateDestroyer.PathEntry>,<>f__AnonymousType0<string,System.Collections.Generic.IEnumerable<string>>>
                var fileGroupsWithThisSize = PathsFile.GetRecords(firstEntry.Path, se.FirstPath, false)
                    .GroupBy(pf => pf.Hash)
                    .Where(group => group.Count() > 1)
                    .Select(hash => new
                    {
                        Hash = hash.Key,
                        Files = hash.Select(file => file.Path)
                    });

                foreach (var hashGroup in fileGroupsWithThisSize)
                    duplicateLists.Add(hashGroup.Hash, hashGroup.Files.ToList());
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

        // Indicate the number of files counted with visual glyphs.
        // Each marks a ten-fold increasement. So # is 10s, & is 100s, @ is 1000s, etc.
        //static char[] CountVisualGlyphs = new char[] { '#', '&', '@', '$', '*' };

        static void ReadFileSizes(string directory, ref List<string> subfolderList)
        {
            if (Verbose)
                Console.WriteLine("Reading contents of " + directory);
            
            try
            {
                int insertIndex = 0;
                foreach (string path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string relativePath = Path.GetFullPath(path).Replace(Directory.GetCurrentDirectory(), String.Empty).TrimStart('\\');

                    // Skip some files which should not be access by the program
                    if (Path.GetFullPath(path) == SizesFile.Stream.Name || Path.GetFullPath(path) == PathsFile.Stream.Name)
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
                                Console.WriteLine(" Size: " + fi.Length.ToString() + " bytes.");

                            ++FileCount;

                            // If verbose mode is turned off, give some visual hint for the user.
                            /*if (!Verbose && CountVisualGlyphs.Length > 0)
                            {
                                if (FileCount % 10 == 0)
                                    Console.Write(CountVisualGlyphs[0]);

                                for (byte i = 2; i < CountVisualGlyphs.Length + 1; ++i)
                                    if (FileCount % Math.Pow(10, i) == 0)
                                    {
                                        Console.Write(new String('\b', 10));
                                        Console.Write(new String(' ', 10));
                                        Console.Write(new String('\b', 10));
                                        Console.Write(CountVisualGlyphs[i - 1]);
                                    }
                            }*/
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

            for (long i = SizesFile.RecordCount - 1; i >= 0; --i)
            {
                SizeEntry rec = new SizeEntry();
                try
                {
                    rec = SizesFile.GetRecordByIndex(i);
                }
                catch (Exception ex)
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
                        Console.Write("Size " + rec.Size + " has " + rec.Count + " files assigned.");

                    SizesFile.DeleteRecord(rec.Size);
                    --SizeCount;
                    --FileCount;

                    // Delete every record (there should be 1) that is associated with this size... they'll no longer be needed.
                    if (rec.FirstPath != -1 && rec.LastPath != -1)
                        if (rec.FirstPath != rec.LastPath)
                            throw new InvalidDataException("Size's count is 1, but there appears to be multiple associated files to exist.");
                        else
                        {
                            PathEntry entry;
                            PathsFile.GetRecordAt(rec.FirstPath, out entry);
                            PathsFile.DeleteRecord(entry, rec.FirstPath);

                            if (Verbose)
                                Console.Write(" Ignored " + entry.Path);
                        }

                    if (Verbose)
                        Console.WriteLine();
                }
            }

            SizesFile.Stream.Flush(true);
            PathsFile.Stream.Flush(true);
        }

        static string CalculateHash(ref MD5CryptoServiceProvider mcsp, string path)
        {
            if (Verbose == true)
                Console.Write("Reading file " + path + "...");

            byte[] md5bytes;
            using (FileStream stream = File.OpenRead(path))
                md5bytes = mcsp.ComputeHash(stream);

            StringBuilder sb = new StringBuilder(32);
            for (int i = 0; i < md5bytes.Length; ++i)
                sb.Append(md5bytes[i].ToString("x2"));

            if (Verbose == true)
                Console.WriteLine(" Hash: " + sb.ToString() + ".");

            return sb.ToString();
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
                bufferSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(fullByteCount, 2)));
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
    }
}