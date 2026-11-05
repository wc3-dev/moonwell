using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Moonwell
{
    /// <summary>
    /// Reads a save's fields back one at a time: <see cref="Next"/> moves to the next field, and the read of its type gives
    /// its value. A field stored as another type than the one asked gives the fallback (a list null), and is passed over.
    /// The code the build writes for each save class calls it.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SaveReader
    {
        private readonly ByteReader reader;
        private int type;

        internal SaveReader(ByteReader reader)
        {
            this.reader = reader;
        }

        /// <summary>The number of the field Next moved to.</summary>
        public int Number { get; private set; }

        /// <summary>Moves to the next field; false when there is none.</summary>
        public bool Next()
        {
            if (reader.Remaining == 0)
            {
                return false;
            }

            var tag = 0;
            var shift = 0;
            int read;
            do
            {
                if (shift > 21)
                {
                    throw new FormatException("SaveReader: a tag longer than four bytes");
                }

                read = reader.ReadByte();
                tag |= (read & 127) << shift;
                shift += 7;
            }
            while ((read & 128) != 0);

            Number = tag >> 4;
            type = tag & 15;
            return true;
        }

        /// <summary>Passes over the field's value.</summary>
        public void Skip()
        {
            switch (type)
            {
                case SaveWriter.IntType:
                    reader.ReadInt();
                    break;
                case SaveWriter.BoolType:
                    reader.ReadByte();
                    break;
                case SaveWriter.StringType:
                    reader.ReadString();
                    break;
                case SaveWriter.FloatType:
                case SaveWriter.TimeType:
                case SaveWriter.FourType:
                    reader.Take(4);
                    break;
                default:
                    if (type == 0 || type == SaveWriter.ListType)
                    {
                        throw new FormatException("SaveReader: a field of an unknown type " + type);
                    }

                    reader.Take(reader.ReadInt());
                    break;
            }
        }

        /// <summary>The field's whole number, or the fallback.</summary>
        public int Int(int fallback)
        {
            if (type == SaveWriter.IntType)
            {
                return reader.ReadInt();
            }

            return type == SaveWriter.FourType ? Four(reader) : Fallback(fallback);
        }

        /// <summary>The field's float, or the fallback.</summary>
        public float Float(float fallback)
        {
            return type == SaveWriter.FloatType ? reader.ReadFloat() : Fallback(fallback);
        }

        /// <summary>The field's bool, or the fallback.</summary>
        public bool Bool(bool fallback)
        {
            return type == SaveWriter.BoolType ? reader.ReadBool() : Fallback(fallback);
        }

        /// <summary>The field's text, or the fallback.</summary>
        public string? String(string? fallback)
        {
            return type == SaveWriter.StringType ? reader.ReadString() : Fallback(fallback);
        }

        /// <summary>The field's real time, or the fallback.</summary>
        public RealTime Time(RealTime fallback)
        {
            return type == SaveWriter.TimeType ? new RealTime(Four(reader)) : Fallback(fallback);
        }

        /// <summary>The field's list of whole numbers, or null.</summary>
        public List<int>? IntList()
        {
            if (type != SaveWriter.ListType + SaveWriter.IntType && type != SaveWriter.ListType + SaveWriter.FourType)
            {
                return Fallback<List<int>?>(null);
            }

            var four = type == SaveWriter.ListType + SaveWriter.FourType;
            var inner = Inner();
            var list = new List<int>();
            while (inner.Remaining > 0)
            {
                list.Add(four ? Four(inner) : inner.ReadInt());
            }

            return list;
        }

        /// <summary>The field's list of floats, or null.</summary>
        public List<float>? FloatList()
        {
            if (type != SaveWriter.ListType + SaveWriter.FloatType)
            {
                return Fallback<List<float>?>(null);
            }

            var inner = Inner();
            var list = new List<float>();
            while (inner.Remaining > 0)
            {
                list.Add(inner.ReadFloat());
            }

            return list;
        }

        /// <summary>The field's list of bools, or null.</summary>
        public List<bool>? BoolList()
        {
            if (type != SaveWriter.ListType + SaveWriter.BoolType)
            {
                return Fallback<List<bool>?>(null);
            }

            var inner = Inner();
            var list = new List<bool>();
            while (inner.Remaining > 0)
            {
                list.Add(inner.ReadBool());
            }

            return list;
        }

        /// <summary>The field's list of texts, or null.</summary>
        public List<string>? StringList()
        {
            if (type != SaveWriter.ListType + SaveWriter.StringType)
            {
                return Fallback<List<string>?>(null);
            }

            var inner = Inner();
            var list = new List<string>();
            while (inner.Remaining > 0)
            {
                list.Add(inner.ReadString());
            }

            return list;
        }

        /// <summary>The field's list of real times, or null.</summary>
        public List<RealTime>? TimeList()
        {
            if (type != SaveWriter.ListType + SaveWriter.TimeType)
            {
                return Fallback<List<RealTime>?>(null);
            }

            var inner = Inner();
            var list = new List<RealTime>();
            while (inner.Remaining > 0)
            {
                list.Add(new RealTime(Four(inner)));
            }

            return list;
        }

        /// <summary>The field's part, made and then read, or the fallback.</summary>
        public P? Part<P>(P? fallback, Func<P> make, Action<P, SaveReader> read)
            where P : class
        {
            if (type != SaveWriter.PartType)
            {
                return Fallback(fallback);
            }

            var part = make();
            read(part, new SaveReader(Inner()));
            return part;
        }

        /// <summary>The field's list of parts, each made and then read, or null.</summary>
        public List<P>? PartList<P>(Func<P> make, Action<P, SaveReader> read)
            where P : class
        {
            if (type != SaveWriter.ListType + SaveWriter.PartType)
            {
                return Fallback<List<P>?>(null);
            }

            var inner = Inner();
            var list = new List<P>();
            while (inner.Remaining > 0)
            {
                var part = make();
                read(part, new SaveReader(inner.Take(inner.ReadInt())));
                list.Add(part);
            }

            return list;
        }

        // A whole number's four bytes, low first; no step passes 2^31, as the Lua cases' integers do not wrap where the game's do.
        private static int Four(ByteReader from)
        {
            var low = from.ReadByte() | (from.ReadByte() << 8) | (from.ReadByte() << 16);
            var high = from.ReadByte();
            return high < 128 ? high * 16777216 + low : (high - 256) * 16777216 + low;
        }

        private ByteReader Inner()
        {
            return reader.Take(reader.ReadInt());
        }

        private T Fallback<T>(T fallback)
        {
            Skip();
            return fallback;
        }
    }
}
