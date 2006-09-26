using System;
using System.Text;
using System.IO;
using libsecondlife;

namespace mapgenerator
{
    class mapgenerator
    {
        static void WriteFieldMember(MapField field)
        {
            string type = "";

            switch (field.Type)
            {
                case FieldType.BOOL:
                    type = "bool";
                    break;
                case FieldType.F32:
                    type = "float";
                    break;
                case FieldType.F64:
                    type = "double";
                    break;
                case FieldType.IPPORT:
                case FieldType.U16:
                    type = "ushort";
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    type = "uint";
                    break;
                case FieldType.LLQuaternion:
                    type = "LLQuaternion";
                    break;
                case FieldType.LLUUID:
                    type = "LLUUID";
                    break;
                case FieldType.LLVector3:
                    type = "LLVector3";
                    break;
                case FieldType.LLVector3d:
                    type = "LLVector3d";
                    break;
                case FieldType.LLVector4:
                    type = "LLVector4";
                    break;
                case FieldType.S16:
                    type = "short";
                    break;
                case FieldType.S32:
                    type = "int";
                    break;
                case FieldType.S8:
                    type = "sbyte";
                    break;
                case FieldType.U64:
                    type = "ulong";
                    break;
                case FieldType.U8:
                    type = "byte";
                    break;
                case FieldType.Fixed:
                    type = "byte[]";
                    break;
            }
            if (field.Type != FieldType.Variable)
            {
                Console.WriteLine("            public " + type + " " + field.Name + ";");
            }
            else
            {
                Console.WriteLine("            private byte[] _" + field.Name.ToLower() + ";");
                Console.WriteLine("            public byte[] " + field.Name + "\n            {");
                Console.WriteLine("                get { return _" + field.Name.ToLower() + "; }");
                Console.WriteLine("                set\n                {");
                Console.WriteLine("                    if (value == null) { _" + 
                    field.Name.ToLower() + " = null; return; }");
                Console.WriteLine("                    if (value.Length > " + 
                    ((field.Count == 1) ? "255" : "1024") + ") { throw new OverflowException(" + 
                    "\"Value exceeds " + ((field.Count == 1) ? "255" : "1024") + " characters\"); }");
                Console.WriteLine("                    else { _" + field.Name.ToLower() + 
                    " = new byte[value.Length]; Array.Copy(value, _" + 
                    field.Name.ToLower() + ", value.Length); }");
                Console.WriteLine("                }\n            }");
            }
        }

        static void WriteFieldFromBytes(MapField field)
        {
            switch (field.Type)
            {
                case FieldType.BOOL:
                    Console.WriteLine("                    " +
                        field.Name + " = (bytes[i++] != 0) ? (bool)true : (bool)false;");
                    break;
                case FieldType.F32:
                    Console.WriteLine("                    " + 
                        "if (!BitConverter.IsLittleEndian) Array.Reverse(bytes, i, 4);");
                    Console.WriteLine("                    " +
                        field.Name + " = BitConverter.ToSingle(bytes, i); i += 4;");
                    break;
                case FieldType.F64:
                    Console.WriteLine("                    " +
                        "if (!BitConverter.IsLittleEndian) Array.Reverse(bytes, i, 8);");
                    Console.WriteLine("                    " +
                        field.Name + " = BitConverter.ToDouble(bytes, i); i += 8;");
                    break;
                case FieldType.Fixed:
                    Console.WriteLine("                    " + field.Name + " = new byte[" + field.Count + "];");
                    Console.WriteLine("                    Array.Copy(bytes, i, " + field.Name +
                        ", 0, " + field.Count + "); i += " + field.Count + ";");
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    Console.WriteLine("                    " + field.Name + 
                        " = (uint)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));");
                    break;
                case FieldType.IPPORT:
                case FieldType.U16:
                    Console.WriteLine("                    " + field.Name + 
                        " = (ushort)(bytes[i++] + (bytes[i++] << 8));");
                    break;
                case FieldType.LLQuaternion:
                    Console.WriteLine("                    " + field.Name + 
                        " = new LLQuaternion(bytes, i); i += 16;");
                    break;
                case FieldType.LLUUID:
                    Console.WriteLine("                    " + field.Name +
                        " = new LLUUID(bytes, i); i += 16;");
                    break;
                case FieldType.LLVector3:
                    Console.WriteLine("                    " + field.Name +
                        " = new LLVector3(bytes, i); i += 12;");
                    break;
                case FieldType.LLVector3d:
                    Console.WriteLine("                    " + field.Name +
                        " = new LLVector3d(bytes, i); i += 24;");
                    break;
                case FieldType.LLVector4:
                    Console.WriteLine("                    " + field.Name +
                        " = new LLVector4(bytes, i); i += 16;");
                    break;
                case FieldType.S16:
                    Console.WriteLine("                    " + field.Name +
                        " = (short)(bytes[i++] + (bytes[i++] << 8));");
                    break;
                case FieldType.S32:
                    Console.WriteLine("                    " + field.Name +
                        " = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));");
                    break;
                case FieldType.S8:
                    Console.WriteLine("                    " + field.Name +
                        " = (sbyte)bytes[i++];");
                    break;
                case FieldType.U64:
                    Console.WriteLine("                    " + field.Name +
                        " = (ulong)(bytes[i++] + (bytes[i++] << 8) + " +
                        "(bytes[i++] << 16) + (bytes[i++] << 24) + " +
                        "(bytes[i++] << 32) + (bytes[i++] << 40) + " +
                        "(bytes[i++] << 48) + (bytes[i++] << 56));");
                    break;
                case FieldType.U8:
                    Console.WriteLine("                    " + field.Name +
                        " = (byte)bytes[i++];");
                    break;
                case FieldType.Variable:
                    if (field.Count == 1)
                    {
                        Console.WriteLine("                    length = (ushort)bytes[i++];");
                    }
                    else
                    {
                        Console.WriteLine("                    length = (ushort)(bytes[i++] + (bytes[i++] << 8));");
                    }
                    Console.WriteLine("                    _" + field.Name.ToLower() + " = new byte[length];");
                    Console.WriteLine("                    Array.Copy(bytes, i, _" + field.Name.ToLower() +
                        ", 0, length); i += length;");
                    break;
                default:
                    Console.WriteLine("!!! ERROR: Unhandled FieldType: " + field.Type.ToString() + " !!!");
                    break;
            }
        }

        static void WriteFieldToBytes(MapField field)
        {
            Console.Write("                ");

            switch (field.Type)
            {
                case FieldType.BOOL:
                    Console.WriteLine("bytes[i++] = (byte)((" + field.Name + ") ? 1 : 0);");
                    break;
                case FieldType.F32:
                    Console.WriteLine("ba = BitConverter.GetBytes(" + field.Name + ");\n" +
                        "                if(!BitConverter.IsLittleEndian) { Array.Reverse(ba, 0, 4); }\n" +
                        "                Array.Copy(ba, 0, bytes, i, 4); i += 4;");
                    break;
                case FieldType.F64:
                    Console.WriteLine("ba = BitConverter.GetBytes(" + field.Name + ");\n" +
                        "                if(!BitConverter.IsLittleEndian) { Array.Reverse(ba, 0, 8); }\n" +
                        "                Array.Copy(ba, 0, bytes, i, 8); i += 8;");
                    break;
                case FieldType.Fixed:
                    Console.WriteLine("Array.Copy(" + field.Name + ", 0, bytes, i, " + field.Count + ");" + 
                        "i += " + field.Count + ";");
                    break;
                case FieldType.IPPORT:
                case FieldType.U16:
                case FieldType.S16:
                    Console.WriteLine("bytes[i++] = (byte)(" + field.Name + " % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 8) % 256);");
                    break;
                case FieldType.LLQuaternion:
                case FieldType.LLUUID:
                case FieldType.LLVector4:
                    Console.WriteLine("Array.Copy(" + field.Name + ".GetBytes(), 0, bytes, i, 16); i += 16;");
                    break;
                case FieldType.LLVector3:
                    Console.WriteLine("Array.Copy(" + field.Name + ".GetBytes(), 0, bytes, i, 12); i += 12;");
                    break;
                case FieldType.LLVector3d:
                    Console.WriteLine("Array.Copy(" + field.Name + ".GetBytes(), 0, bytes, i, 24); i += 24;");
                    break;
                case FieldType.U8:
                    Console.WriteLine("bytes[i++] = " + field.Name + ";");
                    break;
                case FieldType.S8:
                    Console.WriteLine("bytes[i++] = (byte)" + field.Name + ";");
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                case FieldType.S32:
                    Console.WriteLine("bytes[i++] = (byte)(" + field.Name + " % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 8) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 16) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 24) % 256);");
                    break;
                case FieldType.U64:
                    Console.WriteLine("bytes[i++] = (byte)(" + field.Name + " % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 8) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 16) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 24) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 32) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 40) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 48) % 256);");
                    Console.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 56) % 256);");
                    break;
                case FieldType.Variable:
                    if (field.Count == 1)
                    {
                        Console.WriteLine("bytes[i++] = (byte)" + field.Name + ".Length;");
                    }
                    else
                    {
                        Console.WriteLine("bytes[i++] = (byte)(" + field.Name + ".Length % 256);");
                        Console.WriteLine("                bytes[i++] = (byte)((" + 
                            field.Name + ".Length >> 8) % 256);");
                    }
                    Console.WriteLine("                Array.Copy(" + field.Name + ", 0, bytes, i, " + 
                        field.Name + ".Length); " + "i += " + field.Name + ".Length;");
                    break;
                default:
                    Console.WriteLine("!!! ERROR: Unhandled FieldType: " + field.Type.ToString() + " !!!");
                    break;
            }
        }

        static void WriteBlockClass(MapBlock block)
        {
            bool variableFields = false;
            bool floatFields = false;

            Console.WriteLine("        public class " + block.Name + "Block\n        {");

            foreach (MapField field in block.Fields)
            {
                WriteFieldMember(field);

                if (field.Type == FieldType.Variable) { variableFields = true; }
                if (field.Type == FieldType.F32 || field.Type == FieldType.F64) { floatFields = true; }
            }

            Console.WriteLine("");

            // Default constructor
            Console.WriteLine("            public " + block.Name + "Block() { }");

            // Constructor for building the class from bytes
            Console.WriteLine("            public " + block.Name + "Block(byte[] bytes, ref int i)" +
                "\n            {");

            // Declare a length variable if we need it for variable fields in this constructor
            if (variableFields) { Console.WriteLine("                int length;"); }

            // Start of the try catch block
            Console.WriteLine("                try\n                {");

            foreach (MapField field in block.Fields)
            {
                WriteFieldFromBytes(field);
            }

            Console.WriteLine("                }\n                catch (Exception)\n" +
                "                {\n                    throw new MalformedDataException();\n" +
                "                }\n            }\n");

            // ToBytes() function
            Console.WriteLine("            public void ToBytes(byte[] bytes, ref int i)\n            {");

            // Declare a byte[] variable if we need it for floating point field conversions
            if (floatFields) { Console.WriteLine("                byte[] ba;"); }

            foreach (MapField field in block.Fields)
            {
                WriteFieldToBytes(field);
            }

            Console.WriteLine("            }\n        }\n");
        }

        static void WritePacketClass(MapPacket packet)
        {
            Console.WriteLine("    public class " + packet.Name + "Packet : Packet\n    {");

            // Write out each block class
            foreach (MapBlock block in packet.Blocks)
            {
                WriteBlockClass(block);
            }

            // Header member
            Console.WriteLine("        private Header header;");
            Console.WriteLine("        public override Header Header { get { return header; } set { header = value; } }");

            // PacketType member
            Console.WriteLine("        public override PacketType Type { get { return PacketType." + 
                packet.Name + ";  } }");

            // Block members
            foreach (MapBlock block in packet.Blocks)
            {
                Console.WriteLine("        public " + block.Name + "Block" +
                    ((block.Count != 1) ? "[]" : "") + " " + block.Name + ";");
            }

            Console.WriteLine("");

            // Default constructor
            Console.WriteLine("        public " + packet.Name + "Packet()\n        {");
            Console.WriteLine("            Header = new " + packet.Frequency.ToString() + "Header();");
            foreach (MapBlock block in packet.Blocks)
            {
                if (block.Count == 1)
                {
                    // Single count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + "Block();");
                }
                else if (block.Count == -1)
                {
                    // Variable count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + "Block[0];");
                }
                else
                {
                    // Multiple count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + 
                        "Block[" + block.Count + "];");
                }
            }
            Console.WriteLine("        }\n");

            // Constructor that takes a byte array and beginning position only (no prebuilt header)
            bool seenVariable = false;
            Console.WriteLine("        public " + packet.Name + "Packet(byte[] bytes, ref int i)\n        {");
            Console.WriteLine("            int packetEnd = bytes.Length - 1;");
            Console.WriteLine("            Header = new " + packet.Frequency.ToString() + 
                "Header(bytes, ref i, ref packetEnd);");
            foreach (MapBlock block in packet.Blocks)
            {
                if (block.Count == 1)
                {
                    // Single count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + "Block(bytes, ref i);");
                }
                else if (block.Count == -1)
                {
                    // Variable count block
                    if (!seenVariable)
                    {
                        Console.WriteLine("            int count = (int)bytes[i++];");
                        seenVariable = true;
                    }
                    else
                    {
                        Console.WriteLine("            count = (int)bytes[i++];");
                    }
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + "Block[count];");
                    Console.WriteLine("            for (int j = 0; j < count; j++)");
                    Console.WriteLine("            { " + block.Name + "[j] = new " +
                        block.Name + "Block(bytes, ref i); }");
                }
                else
                {
                    // Multiple count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + 
                        "Block[" + block.Count + "];");
                    Console.WriteLine("            for (int j = 0; j < " + block.Count + "; j++)");
                    Console.WriteLine("            { " + block.Name + "[j] = new " + 
                        block.Name + "Block(bytes, ref i); }");
                }
            }
            Console.WriteLine("        }\n");

            seenVariable = false;

            // Constructor that takes a byte array and a prebuilt header
            Console.WriteLine("        public " + packet.Name + 
                "Packet(Header head, byte[] bytes, ref int i)\n        {");
            Console.WriteLine("            Header = head;");
            Console.WriteLine("            int packetEnd = bytes.Length - 1;");
            foreach (MapBlock block in packet.Blocks)
            {
                if (block.Count == 1)
                {
                    // Single count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + "Block(bytes, ref i);");
                }
                else if (block.Count == -1)
                {
                    // Variable count block
                    if (!seenVariable)
                    {
                        Console.WriteLine("            int count = (int)bytes[i++];");
                        seenVariable = true;
                    }
                    else
                    {
                        Console.WriteLine("            count = (int)bytes[i++];");
                    }
                    Console.WriteLine("            " + block.Name + " = new " + block.Name + "Block[count];");
                    Console.WriteLine("            for (int j = 0; j < count; j++)");
                    Console.WriteLine("            { " + block.Name + "[j] = new " +
                        block.Name + "Block(bytes, ref i); }");
                }
                else
                {
                    // Multiple count block
                    Console.WriteLine("            " + block.Name + " = new " + block.Name +
                        "Block[" + block.Count + "];");
                    Console.WriteLine("            for (int j = 0; j < " + block.Count + "; j++)");
                    Console.WriteLine("            { " + block.Name + "[j] = new " +
                        block.Name + "Block(bytes, ref i); }");
                }
            }
            Console.WriteLine("        }\n");

            // ToBytes() function
            Console.WriteLine("        public override byte[] ToBytes()\n        {");

            // FIXME:
            Console.WriteLine("            return null;");

            // Closing bracket
            Console.WriteLine("        }\n    }\n");
        }

        static void Main(string[] args)
        {
            SecondLife libsl = new SecondLife("keywords.txt", "message_template.msg");

            TextReader reader = new StreamReader("template.cs");
            Console.WriteLine(reader.ReadToEnd());
            reader.Close();

            // Write the PacketType enum
            Console.WriteLine("    public enum PacketType\n    {\n        Default,");
            foreach (MapPacket packet in libsl.Protocol.LowMaps)
            {
                if (packet != null)
                {
                    Console.WriteLine("        " + packet.Name + ",");
                }
            }
            foreach (MapPacket packet in libsl.Protocol.MediumMaps)
            {
                if (packet != null)
                {
                    Console.WriteLine("        " + packet.Name + ",");
                }
            }
            foreach (MapPacket packet in libsl.Protocol.HighMaps)
            {
                if (packet != null)
                {
                    Console.WriteLine("        " + packet.Name + ",");
                }
            }
            Console.WriteLine("    }\n");

            // Write the base Packet class
            Console.WriteLine("    public abstract class Packet\n    {\n" + 
                "        public abstract Header Header { get; set; }\n" +
                "        public abstract PacketType Type { get; }\n" +
                "        public int TickCount;\n\n" +
                "        public abstract byte[] ToBytes();\n\n" +
                "        public static Packet BuildPacket(byte[] bytes, ref int packetEnd)\n" +
                "        {\n            ushort id;\n            int i = 0;\n" +
                "            Header header = Header.BuildHeader(bytes, ref i, ref packetEnd);\n" +
                "            if ((bytes[0] & Helpers.MSG_ZEROCODED) != 0)\n            {\n" +
                "                byte[] zeroBuffer = new byte[4096];\n" +
                "                packetEnd = Helpers.ZeroDecode(bytes, packetEnd + 1, zeroBuffer) - 1;\n" +
                "                bytes = zeroBuffer;\n            }\n\n" + 
                "            if (bytes[4] == 0xFF)\n            {\n" +
                "                if (bytes[5] == 0xFF)\n                {\n" +
                "                    id = (ushort)((bytes[6] << 8) + bytes[7]);\n" +
                "                    switch (id)\n                    {");
            foreach (MapPacket packet in libsl.Protocol.LowMaps)
            {
                if (packet != null)
                {
                    Console.WriteLine("                        case " + packet.ID + ":");
                    Console.WriteLine("                            return new " + packet.Name +
                        "Packet(header, bytes, ref i);");
                }
            }
            Console.WriteLine("                    }\n                }\n                else\n" +
                "                {\n                    id = (ushort)bytes[5];\n" +
                "                    switch (id)\n                    {");
            foreach (MapPacket packet in libsl.Protocol.MediumMaps)
            {
                if (packet != null)
                {
                    Console.WriteLine("                        case " + packet.ID + ":");
                    Console.WriteLine("                            return new " + packet.Name +
                        "Packet(header, bytes, ref i);");
                }
            }
            Console.WriteLine("                    }\n                }\n            }\n" + 
                "            else\n            {\n" + 
                "                id = (ushort)bytes[4];\n" + 
                "                switch (id)\n                    {");
            foreach (MapPacket packet in libsl.Protocol.HighMaps)
            {
                if (packet != null)
                {
                    Console.WriteLine("                        case " + packet.ID + ":");
                    Console.WriteLine("                            return new " + packet.Name +
                        "Packet(header, bytes, ref i);");
                }
            }
            Console.WriteLine("                }\n            }\n\n" +
                "            throw new MalformedDataException(\"Unknown packet ID\");\n" + 
                "        }\n    }\n");

            // Write the packet classes
            foreach (MapPacket packet in libsl.Protocol.LowMaps)
            {
                if (packet != null) { WritePacketClass(packet); }
            }

            foreach (MapPacket packet in libsl.Protocol.MediumMaps)
            {
                if (packet != null) { WritePacketClass(packet); }
            }

            foreach (MapPacket packet in libsl.Protocol.HighMaps)
            {
                if (packet != null) { WritePacketClass(packet); }
            }

            Console.WriteLine("}");
        }
    }
}
