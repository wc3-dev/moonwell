using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Moonwell
{
    /// <summary>
    /// Writes a save's fields, each as a tag (its number and its type) and its value; a list and a part go behind their
    /// length, so a reader that no longer knows the field can skip it. The code the build writes for each save class calls it.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SaveWriter
    {
        internal const int IntType = 1;
        internal const int FloatType = 2;
        internal const int BoolType = 3;
        internal const int StringType = 4;
        internal const int TimeType = 5;
        internal const int PartType = 6;
        internal const int FourType = 7;
        internal const int ListType = 8;

        // A whole number at least this far from 0 takes five bytes as a ByteWriter writes it, so it is written in four.
        private const int FiveBytes = 134217728;

        private readonly ByteWriter writer = new ByteWriter();

        /// <summary>The bytes written, each 0 to 255.</summary>
        internal List<int> Bytes => writer.Bytes;

        /// <summary>Writes a whole number, in four bytes when it would take five (an object id).</summary>
        public void Int(int number, int value)
        {
            if (IsLong(value))
            {
                Tag(number, FourType);
                Four(writer.Bytes, value);
                return;
            }

            Tag(number, IntType);
            writer.WriteInt(value);
        }

        /// <summary>Writes a float.</summary>
        public void Float(int number, float value)
        {
            Tag(number, FloatType);
            writer.WriteFloat(value);
        }

        /// <summary>Writes a bool.</summary>
        public void Bool(int number, bool value)
        {
            Tag(number, BoolType);
            writer.WriteBool(value);
        }

        /// <summary>Writes a text; a null text is not written, so it reads back as the field's default.</summary>
        public void String(int number, string? value)
        {
            if (value == null)
            {
                return;
            }

            Tag(number, StringType);
            writer.WriteString(value);
        }

        /// <summary>Writes a real time, in four bytes, as a date takes five otherwise.</summary>
        public void Time(int number, RealTime value)
        {
            Tag(number, TimeType);
            Four(writer.Bytes, value.Seconds);
        }

        /// <summary>Writes a list of whole numbers, each in four bytes when that is shorter; a null list is not written.</summary>
        public void IntList(int number, IEnumerable<int>? values)
        {
            if (values == null)
            {
                return;
            }

            var small = new ByteWriter();
            var four = new List<int>();
            foreach (var value in values)
            {
                small.WriteInt(value);
                Four(four, value);
            }

            if (four.Count < small.Count)
            {
                Behind(number, ListType + FourType, four);
                return;
            }

            Behind(number, ListType + IntType, small.Bytes);
        }

        /// <summary>Writes a list of floats; a null list is not written.</summary>
        public void FloatList(int number, IEnumerable<float>? values)
        {
            if (values == null)
            {
                return;
            }

            var inner = new ByteWriter();
            foreach (var value in values)
            {
                inner.WriteFloat(value);
            }

            Behind(number, ListType + FloatType, inner.Bytes);
        }

        /// <summary>Writes a list of bools; a null list is not written.</summary>
        public void BoolList(int number, IEnumerable<bool>? values)
        {
            if (values == null)
            {
                return;
            }

            var inner = new ByteWriter();
            foreach (var value in values)
            {
                inner.WriteBool(value);
            }

            Behind(number, ListType + BoolType, inner.Bytes);
        }

        /// <summary>Writes a list of texts, a null one as the empty text; a null list is not written.</summary>
        public void StringList(int number, IEnumerable<string?>? values)
        {
            if (values == null)
            {
                return;
            }

            var inner = new ByteWriter();
            foreach (var value in values)
            {
                inner.WriteString(value);
            }

            Behind(number, ListType + StringType, inner.Bytes);
        }

        /// <summary>Writes a list of real times; a null list is not written.</summary>
        public void TimeList(int number, IEnumerable<RealTime>? values)
        {
            if (values == null)
            {
                return;
            }

            var inner = new List<int>();
            foreach (var value in values)
            {
                Four(inner, value.Seconds);
            }

            Behind(number, ListType + TimeType, inner);
        }

        /// <summary>Writes a part, a class of saved fields of its own; a null part is not written.</summary>
        public void Part<P>(int number, P? part, Action<P, SaveWriter> write)
            where P : class
        {
            if (part == null)
            {
                return;
            }

            var inner = new SaveWriter();
            write(part, inner);
            Behind(number, PartType, inner.Bytes);
        }

        /// <summary>Writes a list of parts, a null one as a part with nothing saved; a null list is not written.</summary>
        public void PartList<P>(int number, IEnumerable<P?>? parts, Action<P, SaveWriter> write)
            where P : class
        {
            if (parts == null)
            {
                return;
            }

            var inner = new ByteWriter();
            foreach (var part in parts)
            {
                var one = new SaveWriter();
                if (part != null)
                {
                    write(part, one);
                }

                inner.WriteInt(one.Bytes.Count);
                inner.Bytes.AddRange(one.Bytes);
            }

            Behind(number, ListType + PartType, inner.Bytes);
        }

        /// <summary>Whether a whole number takes five bytes as a ByteWriter writes it.</summary>
        internal static bool IsLong(int value)
        {
            return value >= FiveBytes || value < -FiveBytes;
        }

        /// <summary>Adds a whole number's four bytes, low first.</summary>
        internal static void Four(List<int> bytes, int value)
        {
            // Each byte is masked after the shift, so a shift that fills with zeros (the game's) gives what one that fills with the sign does.
            bytes.Add(value & 255);
            bytes.Add((value >> 8) & 255);
            bytes.Add((value >> 16) & 255);
            bytes.Add((value >> 24) & 255);
        }

        private void Behind(int number, int type, List<int> bytes)
        {
            Tag(number, type);
            writer.WriteInt(bytes.Count);
            writer.Bytes.AddRange(bytes);
        }

        // The tag is the number and the type, 7 bits a byte, so fields 1 to 7 take one byte.
        private void Tag(int number, int type)
        {
            var tag = number * 16 + type;
            while (tag > 127)
            {
                writer.Bytes.Add((tag & 127) | 128);
                tag >>= 7;
            }

            writer.Bytes.Add(tag);
        }
    }
}
