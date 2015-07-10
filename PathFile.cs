﻿using System;
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
        public ushort PathLength; // 2
        public string Path; // n
        public byte[] MD5; // 32
        public long NextRecord; // 8

        public int RecordSize { get { return 1 + 8 + 2 + PathLength + 32 + 8; } }

        public PathEntry(string path)
        {
            this.Deleted = false;
            this.PrevRecord = -1;
            this.PathLength = Convert.ToUInt16(Encoding.UTF8.GetByteCount(path));
            this.Path = path;
            this.MD5 = Encoding.UTF8.GetBytes(new String('0', 32));
            this.NextRecord = -1;
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
                    record.PathLength = br.ReadUInt16(); // Path length (2)
                    byte[] path_bytes = br.ReadBytes(record.PathLength); // n
                    record.Path = Encoding.UTF8.GetString(path_bytes);
                    record.MD5 = br.ReadBytes(32); // 32
                    record.NextRecord = br.ReadInt64(); // 8

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
                    record.PathLength = br.ReadUInt16(); // Path length.
                    byte[] path_bytes = br.ReadBytes(record.PathLength); // n
                    record.Path = Encoding.UTF8.GetString(path_bytes);
                    record.MD5 = br.ReadBytes(32); // 32
                    record.NextRecord = br.ReadInt64(); // 8

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
                    record.PathLength = br.ReadUInt16(); // Path length (2)
                    byte[] path_bytes = br.ReadBytes(record.PathLength); // n
                    record.Path = Encoding.UTF8.GetString(path_bytes);
                    record.MD5 = br.ReadBytes(32); // 32
                    record.NextRecord = br.ReadInt64(); // 8
                }
                catch (Exception e)
                {
                    record.Deleted = true;
                    record.PrevRecord = -1;
                    record.PathLength = 0;
                    record.Path = e.Message;
                    record.MD5 = new byte[32];
                    record.NextRecord = -1;

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
                bw.Write(rec.PathLength); // 2
                bw.Write(Encoding.UTF8.GetBytes(rec.Path)); // n
                bw.Write(rec.MD5); // 32
                bw.Write(rec.NextRecord); // 8
            }
            this.Stream.Flush(true);

            return pos;
        }

        public void WriteRecordAt(PathEntry rec, long pos)
        {
            this.Stream.Seek(pos, SeekOrigin.Begin);
            using (BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true))
            {
                bw.Write(rec.Deleted); // 1
                bw.Write(rec.PrevRecord); // 8
                bw.Write(rec.PathLength); // 2
                bw.Write(Encoding.UTF8.GetBytes(rec.Path)); // n
                bw.Write(rec.MD5); // 32
                bw.Write(rec.NextRecord); // 8
            }
            this.Stream.Flush(true);
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

        public IEnumerable<PathEntry> GetRecords(string path, bool traverseBackwards = false)
        {
            PathEntry entry = new PathEntry();
            long pos = -1;

            if (GetRecord(path, out entry, out pos))
            {
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

            Console.WriteLine("Would delete record " + already.Path);

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
    }
}
