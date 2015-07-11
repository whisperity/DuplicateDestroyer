using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DuplicateDestroyer
{
    struct PathEntry
    {
        // Offsets where the previous and next record is in the datafile (-1 indicates NIL)
        public bool Deleted; // 1 (true if the record is deleted and should be skipped)
        public long PrevRecord; // 8
        public long NextRecord; // 8
        public ushort PathLength; // 2
        public string Path; // n
        public string Hash; // 32 (MD5 is used)

        public int RecordSize { get { return 1 + 8 + 2 + PathLength + 32 + 8; } }

        public PathEntry(string path)
        {
            this.Deleted = false;
            this.PrevRecord = -1;
            this.NextRecord = -1;
            this.PathLength = Convert.ToUInt16(Encoding.UTF8.GetByteCount(path));
            this.Path = path;
            this.Hash = new String('0', 32);
        }
    }


    class PathFile
    {
        internal FileStream Stream;
        
        public PathFile(FileStream stream)
        {
            this.Stream = stream;
        }

        internal IEnumerable<PathEntry> GetRecords(bool skipDeleted = false)
        {
            this.Stream.Seek(0, SeekOrigin.Begin);
            long position = 0;

            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
                while (position < this.Stream.Length)
                {
                    this.Stream.Seek(position, SeekOrigin.Begin);
                    PathEntry record = new PathEntry();

                    record.Deleted = br.ReadBoolean(); // 1
                    record.PrevRecord = br.ReadInt64(); // 8
                    record.NextRecord = br.ReadInt64(); // 8
                    record.PathLength = br.ReadUInt16(); // Path length (2)
                    byte[] path_bytes = br.ReadBytes(record.PathLength); // n
                    record.Path = Encoding.UTF8.GetString(path_bytes);
                    record.Hash = Encoding.UTF8.GetString(br.ReadBytes(32)); // 32

                    position = this.Stream.Position;

                    if (!skipDeleted || !record.Deleted)
                        yield return record;
                }

            yield break;
        }

        public bool GetRecord(string path, out PathEntry record, out long position)
        {
            record = new PathEntry();
            position = 0;

            if (this.Stream.Length == 0)
                return false;

            // Search for the record linearly
            this.Stream.Seek(0, SeekOrigin.Begin);
            bool found = false;
            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
            {
                while (this.Stream.Position < this.Stream.Length && !found)
                {
                    // Read every record and try to make out if it is the searched one.
                    position = this.Stream.Position;
                    record.Deleted = br.ReadBoolean(); // 1
                    record.PrevRecord = br.ReadInt64(); // 8
                    record.NextRecord = br.ReadInt64(); // 8
                    record.PathLength = br.ReadUInt16(); // Path length.
                    byte[] path_bytes = br.ReadBytes(record.PathLength); // n
                    record.Path = Encoding.UTF8.GetString(path_bytes);
                    record.Hash = Encoding.UTF8.GetString(br.ReadBytes(32)); // 32

                    if (record.Path == path && !record.Deleted)
                        found = true;
                }
            }

            return found;
        }

        public bool GetRecordAt(long position, out PathEntry record)
        {
            record = new PathEntry();

            if (position > this.Stream.Length)
                throw new ArgumentOutOfRangeException("Position out of stream bounds.");

            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
            {
                try
                {
                    this.Stream.Seek(position, SeekOrigin.Begin);

                    record.Deleted = br.ReadBoolean(); // 1
                    record.PrevRecord = br.ReadInt64(); // 8
                    record.NextRecord = br.ReadInt64(); // 8
                    record.PathLength = br.ReadUInt16(); // Path length (2)
                    byte[] path_bytes = br.ReadBytes(record.PathLength); // n
                    record.Path = Encoding.UTF8.GetString(path_bytes);
                    record.Hash = Encoding.UTF8.GetString(br.ReadBytes(32)); // 32
                }
                catch (Exception e)
                {
                    record.Deleted = true;
                    //record.PrevRecord = -1;
                    //record.NextRecord = -1;
                    //record.PathLength = 0;
                    record.Path = e.Message;
                    //record.Hash = String.Empty;

                    return false;
                }
            }

            return true;
        }

        public long WriteRecord(PathEntry rec)
        {
            long pos = -1;
            this.Stream.Seek(0, SeekOrigin.End);
            using (BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true))
            {
                pos = bw.BaseStream.Position;
                bw.Write(rec.Deleted); // 1
                bw.Write(rec.PrevRecord); // 8
                bw.Write(rec.NextRecord); // 8
                bw.Write(rec.PathLength); // 2
                bw.Write(Encoding.UTF8.GetBytes(rec.Path)); // n
                bw.Write(Encoding.UTF8.GetBytes(rec.Hash)); // 32
            }
            this.Stream.Flush();

            return pos;
        }

        public void WriteRecordAt(PathEntry rec, long pos)
        {
            this.Stream.Seek(pos, SeekOrigin.Begin);
            using (BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true))
            {
                bw.Write(rec.Deleted); // 1
                bw.Write(rec.PrevRecord); // 8
                bw.Write(rec.NextRecord); // 8
                bw.Write(rec.PathLength); // 2
                bw.Write(Encoding.UTF8.GetBytes(rec.Path)); // n
                bw.Write(Encoding.UTF8.GetBytes(rec.Hash)); // 32
            }
            this.Stream.Flush();
        }

        public long AddAfter(PathEntry node, PathEntry entry, long nodePosition = -1)
        {
            // If we don't know where the record is, search for it
            if (nodePosition == -1)
                GetRecord(node.Path, out node, out nodePosition);

            entry.PrevRecord = nodePosition;
            long insertedPosition = WriteRecord(entry);

            if (node.NextRecord != -1)
            {
                // If there is a record after 'node', we need to insert 'entry' before it
                // So A -> B -> C, insert D after B results in: A -> B -> D -> C (and of course the backwards links.)
                PathEntry nextRecord = new PathEntry();
                GetRecordAt(node.NextRecord, out nextRecord);

                nextRecord.PrevRecord = insertedPosition;
                WriteRecordAt(nextRecord, node.NextRecord);

                entry.NextRecord = node.NextRecord;
                WriteRecordAt(entry, insertedPosition);
            }

            node.NextRecord = insertedPosition;
            WriteRecordAt(node, nodePosition);

            return insertedPosition;
        }

        public IEnumerable<PathEntry> GetRecords(string path, long pos = -1, bool traverseBackwards = false)
        {
            PathEntry entry = new PathEntry();

            // Try to find the record given for traversal
            bool recordFound;
            if (pos == -1)
                recordFound = GetRecord(path, out entry, out pos);
            else
            {
                recordFound = GetRecordAt(pos, out entry);

                // GetRecordAt doesn't check just gives the record as result.
                // Bail out if the given record is not the right one we searched for...
                if (entry.Path != path)
                    recordFound = false;
            }

            // Get the rest of the records and given them in the enumerable
            if (recordFound)
            {
                yield return entry;

                long nextPositionToRead;
                if (traverseBackwards)
                    nextPositionToRead = entry.PrevRecord;
                else
                    nextPositionToRead = entry.NextRecord;

                while (nextPositionToRead != -1)
                {
                    if (GetRecordAt(nextPositionToRead, out entry))
                    {
                        yield return entry;

                        if (traverseBackwards)
                            nextPositionToRead = entry.PrevRecord;
                        else
                            nextPositionToRead = entry.NextRecord;
                    }
                    else
                        break;
                }
            }

            yield break;
        }

        public void DeleteRecord(string path)
        {
            PathEntry rec = new PathEntry();
            long pos = 0;

            if (GetRecord(path, out rec, out pos))
                DeleteRecord(rec, pos);
        }

        public void DeleteRecord(PathEntry rec, long position)
        {
            // Check if the given record is the one we want to delete.
            PathEntry already = new PathEntry();
            if (!GetRecordAt(position, out already))
                throw new ArgumentOutOfRangeException("There is no record at the given position.");

            if (rec.Path != already.Path)
                throw new ArgumentException("The record at the given position does not match the given record.");

            // When a record is deleted, the broken chain that was going through them has to be reconnected
            // A -> B -> C with B's deletion becomes A -> C
            if (already.PrevRecord != -1)
            {
                PathEntry prevRec = new PathEntry();
                GetRecordAt(already.PrevRecord, out prevRec);

                // If there is something after the deleted one, it comes after the previous one
                if (already.NextRecord != -1)
                    prevRec.NextRecord = already.NextRecord;
                else
                    prevRec.NextRecord = -1;

                WriteRecordAt(prevRec, already.PrevRecord);
            }
            
            if (already.NextRecord != -1)
            {
                PathEntry nextRec = new PathEntry();
                GetRecordAt(already.NextRecord, out nextRec);

                // If there is something before the deleted one, it comes before the next one
                if (already.PrevRecord != -1)
                    nextRec.PrevRecord = already.PrevRecord;
                else
                    nextRec.PrevRecord = -1;

                WriteRecordAt(nextRec, already.NextRecord);
            }

            // Mark the current record deleted and break its chains
            already.PrevRecord = -1;
            already.NextRecord = -1;
            already.Deleted = true;

            WriteRecordAt(already, position);
        }

        private const int MaxConsolidateCount = 4096;
        public long Consolidate()
        {
            // TODO: Add support for moving the pointers in a SizeFile backwards because even though in-situ
            // this file is consolidated, the first and last path pointers are heavily invalidated.
            throw new NotImplementedException("Prevention of SizeFile invalidation is not implemented.");

            // Calling this method physically eliminates the deleted records from the datafile

            long fullCount = GetRecords().LongCount();
            long nonDeletedCount = GetRecords(true).LongCount();
            long removedCount = 0; // Will count how many records we have acutally, physically removed

            // (Don't use Dictionary<> here. List<> will be faster in this operation as we don't
            // care about the association and will always read this collection sequentially.)
            int consolidateLimit;
            if (((int)fullCount - nonDeletedCount) < MaxConsolidateCount)
                consolidateLimit = (int)(fullCount - nonDeletedCount);
            else
                consolidateLimit = MaxConsolidateCount;

            List<KeyValuePair<long, long>> moveOffsets = new List<KeyValuePair<long, long>>(consolidateLimit);

            // First, we will read the storage sequentially and find every 'Deleted' record.
            BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true);
            BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true);

            long position = 0;
            while (removedCount < (fullCount - nonDeletedCount))
            {
                // Read the stream and search for logically (marked) deleted records.
                // Can't use GetRecords() here because the enumeration is modified while processing it.
                this.Stream.Seek(position, SeekOrigin.Begin);

                bool stopSeeking = false;
                long currentMoveOffset = 0;
                long recordStartPosition = 0;
                while (position < this.Stream.Length && !stopSeeking)
                {
                    // Read a record
                    recordStartPosition = this.Stream.Position; // Mark where the record has begun

                    bool deleted = br.ReadBoolean(); // 1
                    br.ReadInt64(); // 8
                    br.ReadInt64(); // 8
                    ushort pathLength = br.ReadUInt16(); // 2
                    string path = Encoding.UTF8.GetString(br.ReadBytes(pathLength)); // n
                    br.ReadBytes(32); // 32

                    position = this.Stream.Position; // Save out where the read head currently is (on the beginning of the next record)

                    // If we found a deleted record, mark its position and length
                    if (deleted)
                    {
                        currentMoveOffset = 1 + 8 + 8 + 2 + pathLength + 32; // The size of the found deleted record

                        // Mark it for physical deletion
                        moveOffsets.Add(new KeyValuePair<long, long>(recordStartPosition, currentMoveOffset));

                        // Don't continue searching for deleted records if a limit has been hit
                        if (moveOffsets.Count >= consolidateLimit)
                            stopSeeking = true;
                    }
                }

                // After a certain number of deleted records are found (or every deleted record is found without reaching said limit)
                // Delete the records physically by pulling the records coming after it back by 'currentMoveOffset' bytes.
                for (int i = moveOffsets.Count - 1; i >= 0; --i)
                {
                    Program.MoveEndPart(this.Stream, moveOffsets[i].Key + moveOffsets[i].Value, -moveOffsets[i].Value);

                    // As the records move backwards because of the deletions, the record at 'position'
                    // (the next after the last marked-for-deletion-one also moves backwards.
                    if (position >= moveOffsets[i].Key)
                        position -= moveOffsets[i].Value;
                }

                // Now the chains of the doubly linked list is broken, because records have been moved and this invalidated the pointers
                long pullPosition = 0;
                this.Stream.Seek(0, SeekOrigin.Begin);

                while (pullPosition < this.Stream.Length)
                {
                    // Iterate every record and align their pointers
                    long prevRecordPtrPosition, nextRecordPtrPosition;

                    // Read a record
                    br.ReadBoolean(); // 1
                    prevRecordPtrPosition = this.Stream.Position;
                    long prevRecord = br.ReadInt64(); // 8
                    nextRecordPtrPosition = this.Stream.Position;
                    long nextRecord = br.ReadInt64(); // 8
                    ushort pathLength = br.ReadUInt16(); // 2
                    string path = Encoding.UTF8.GetString(br.ReadBytes(pathLength)); // n
                    br.ReadBytes(32); // 32

                    pullPosition = this.Stream.Position; // Save out where the read head ended

                    if (prevRecord >= moveOffsets[0].Key || nextRecord >= moveOffsets[0].Key)
                    {
                        // Accumulate how much the pointers have to be moved to get valid again
                        long prevPtrMoveBy = 0;
                        long nextPtrMoveBy = 0;

                        foreach (KeyValuePair<long, long> kv in moveOffsets)
                        {
                            if (prevRecord >= kv.Key)
                                prevPtrMoveBy += kv.Value;

                            if (nextRecord >= kv.Key)
                                nextPtrMoveBy += kv.Value;
                        }

                        if (prevPtrMoveBy != 0)
                        {
                            this.Stream.Seek(prevRecordPtrPosition, SeekOrigin.Begin);
                            prevRecord -= prevPtrMoveBy;
                            bw.Write(prevRecord); // 8
                        }

                        if (nextPtrMoveBy != 0)
                        {
                            this.Stream.Seek(nextRecordPtrPosition, SeekOrigin.Begin);
                            nextRecord -= nextPtrMoveBy;
                            bw.Write(nextRecord); // 8
                        }

                        this.Stream.Seek(pullPosition, SeekOrigin.Begin); // Go back where we last ended reading
                    }
                }

                // Mark that some entries have been removed, empty the offset list and continue working
                removedCount += moveOffsets.Count;
                moveOffsets.Clear();
            }

            br.Dispose();
            bw.Dispose();

            return removedCount;
        }
    }
}
