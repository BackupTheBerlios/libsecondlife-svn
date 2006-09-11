/*
 * Copyright (c) 2006, Second Life Reverse Engineering Team
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the Second Life Reverse Engineering Team nor the names 
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Collections;

namespace libsecondlife
{
	public struct Field
	{
		public object Data;
		public MapField Layout;

		public override string ToString()
		{
			string output = "";

			if (Layout.Type == FieldType.Variable || Layout.Type == FieldType.Fixed)
			{
				bool printable = true;
				byte[] byteArray = (byte[])Data;

				for (int i = 0; i < byteArray.Length; ++i)
				{
					// Check if there are any unprintable characters in the array
					if ((byteArray[i] < 0x20 || byteArray[i] > 0x7E) && byteArray[i] != 0x09
						&& byteArray[i] != 0x0D)
					{
						printable = false;
					}
				}

				if (printable)
				{
					output = Helpers.FieldToString(byteArray);
				}
				else
				{
					for (int i = 0; i < byteArray.Length; i += 16)
					{
						output += Layout.Name + ": ";

						for (int j = 0; j < 16; j++)
						{
							if ((i + j) < byteArray.Length)
							{
								output += String.Format("{0:X2} ", byteArray[i + j]);
							}
							else
							{
								output += "   ";
							}
						}

						for (int j = 0; j < 16 && (i + j) < byteArray.Length; j++)
						{
							if (byteArray[i + j] >= 0x20 && byteArray[i + j] < 0x7E)
							{
								output += (char)byteArray[i + j];
							}
							else
							{
								output += ".";
							}
						}

						output += "\n";
					}
				}
			}
			else
			{
				output += Layout.Name + ": " + Data.ToString();
			}

			return output;
		}
	}

	public struct Block
	{
		public ArrayList Fields;
		public MapBlock Layout;
	}

	public class Packet
	{
		public byte[] Data;
		public MapPacket Layout;

		public ushort Sequence
		{
			get
			{
				// The sequence number is the third and fourth bytes of the packet, stored 
				// in network order
                                return (ushort)(Data[2] * 256 + Data[3]);
			}

			set
			{
				Data[2] = (byte)(value / 256);
				Data[3] = (byte)(value % 256);
			}
		}

		private ProtocolManager Protocol;

		public Packet(string command, ProtocolManager protocol, int length)
		{
			Protocol = protocol;
			Data = new byte[length];
			Layout = protocol.Command(command);

			if (Layout == null)
			{
				//Client.Log("Attempting to build a packet with invalid command \"" + command + "\"", 
				//	Helpers.LogLevel.Error);

                // Create an empty Layout
                Layout = new MapPacket();
                Layout.Blocks = new ArrayList();
                return;
			}

			switch (Layout.Frequency)
			{
				case PacketFrequency.Low:
					// Set the low frequency identifier bits
					byte[] lowHeader = {0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF};
					Array.Copy(lowHeader, 0, Data, 0, 6);

					// Store the packet ID in network order
					Data[6] = (byte)(Layout.ID / 256);
					Data[7] = (byte)(Layout.ID % 256);

					break;
				case PacketFrequency.Medium:
					// Set the medium frequency identifier bit
					byte[] mediumHeader = {0x00, 0x00, 0x00, 0x00, 0xFF};
					Array.Copy(mediumHeader, 0, Data, 0, 5);
					Data[5] = (byte)Layout.ID;

					break;
				case PacketFrequency.High:
					byte[] highHeader = {0x00, 0x00, 0x00, 0x00};
					Array.Copy(highHeader, 0, Data, 0, 4);
					Data[4] = (byte)Layout.ID;

					break;
			}
		}

		public Packet(byte[] data, int length, ProtocolManager protocol) : this(data, length, protocol, protocol.Command(data), true)
		{
		}

		public Packet(byte[] data, int length, ProtocolManager protocol, MapPacket layout, bool copy)
		{
			Protocol = protocol;
			Layout = layout;

			if (Layout == null)
			{
				// Create an empty MapPacket
				Layout = new MapPacket();
				Layout.Blocks = new ArrayList();
			}

			if (copy) {
				// Copy the network byte array to this packet's byte array
				Data = new byte[length];
				Array.Copy(data, 0, Data, 0, length);
			}
			else
			{
				// Use the buffer we got for Data
				Data = data;
			}
		}

		public ArrayList Blocks()
		{
			Field field;
			Block block;
			byte blockCount;
			int fieldSize;

			// Get the starting position of the SL payload (different than the UDP payload)
			int pos = HeaderLength();

			// Initialize the block list we are returning
			ArrayList blocks = new ArrayList();

			// Get the packet body's length so we can do bounds checking
			int length = Data.Length;
			if ((Data[0] & Helpers.MSG_APPENDED_ACKS) != 0)
				length -= Data[Data.Length - 1] * 4 + 1;

			foreach (MapBlock blockMap in Layout.Blocks)
			{
				if (blockMap.Count == -1)
				{
					// Variable count block
					if (pos < length)
					{
						blockCount = Data[pos];
						pos++;
					}
					else
					{
                        // FIXME: Figure out a sane way to log from this function
						Console.WriteLine("WARNING: Blocks(): end of packet in 1-byte variable block count for " + 
                            Layout.Name + "." + blockMap.Name + " (" + pos + "/" + length + ")");
						goto Done;
					}
				}
				else
				{
					blockCount = (byte)blockMap.Count;
				}

				for (int i = 0; i < blockCount; ++i)
				{
					// Create a new block to push back on the list
					block = new Block();
					block.Layout = blockMap;
					block.Fields = new ArrayList();

					foreach (MapField fieldMap in blockMap.Fields)
					{
						if (fieldMap.Type == FieldType.Variable)
						{
							if (fieldMap.Count == 1)
							{
								// Field length described with one byte
								if (pos < length)
								{
									fieldSize = (ushort)Data[pos];
									pos++;
								}
								else
								{
                                    Console.WriteLine("WARNING: Blocks(): end of packet in 1-byte variable field count for " + 
                                        Layout.Name + "." + blockMap.Name + "." + fieldMap.Name + " (" + pos + "/" + length + ")");
									goto BlockDone;
								}
							}
							else // (fieldMap.Count == 2)
							{
								// Field length described with two bytes
								if (pos + 1 < length)
								{
									fieldSize = (ushort)(Data[pos] + Data[pos + 1] * 256);
									pos += 2;
								}
								else
								{
                                    Console.WriteLine("WARNING: Blocks(): end of packet in 2-byte variable field count for " + 
                                        Layout.Name + "." + blockMap.Name + "." + fieldMap.Name + " (" + pos + "/" + length + ")");
									goto BlockDone;
								}
							}

							if (fieldSize != 0)
							{
								if (pos + fieldSize <= length)
								{
									// Create a new field to add to the fields for this block
									field = new Field();
									field.Data = GetField(Data, pos, fieldMap.Type, fieldSize);
									field.Layout = fieldMap;

									block.Fields.Add(field);

									pos += fieldSize;
								}
								else
								{
                                    Console.WriteLine("WARNING: Blocks(): end of packet in " + fieldSize + 
                                        "-byte variable field " + Layout.Name + "." + blockMap.Name + "." + fieldMap.Name + 
                                        " (" + pos + "/" + length + ")");
									goto BlockDone;
								}
							}
						}
						else if (fieldMap.Type == FieldType.Fixed)
						{
							fieldSize = fieldMap.Count;

							if (pos + fieldSize <= length)
							{
								// Create a new field to add to the fields for this block
								field = new Field();
								field.Data = GetField(Data, pos, fieldMap.Type, fieldSize);
								field.Layout = fieldMap;

								block.Fields.Add(field);

								pos += fieldSize;
							}
							else
							{
                                Console.WriteLine("WARNING: Blocks(): end of packet in " + fieldSize + "-byte fixed field " + 
                                    Layout.Name + "." + blockMap.Name + "." + fieldMap.Name + " (" + pos + "/" + length + ")");
								goto BlockDone;
							}
						}
						else
						{
							for (int j = 0; j < fieldMap.Count; ++j)
							{
								fieldSize = (int)Protocol.TypeSizes[fieldMap.Type];

								if (pos + fieldSize <= length)
								{
									// Create a new field to add to the fields for this block
									field = new Field();
									field.Data = GetField(Data, pos, fieldMap.Type, fieldSize);
									field.Layout = fieldMap;

									block.Fields.Add(field);

									pos += fieldSize;
								}
								else
								{
                                    Console.WriteLine("WARNING: Blocks(): end of packet in " + fieldSize + "-byte " + 
                                        fieldMap.Type + " field " + Layout.Name + "." + blockMap.Name + "." + fieldMap.Name + 
                                        " (" + pos + "/" + length + ")");
									goto BlockDone;
								}
							}
						}
					}

				BlockDone:
					blocks.Add(block);
				}
			}

			Done:
				return blocks;
		}

		public object Field(string name)
		{
			foreach (Block block in Blocks())
			{
				foreach (Field field in block.Fields)
				{
					if (field.Layout.Name == name)
					{
						return field.Data;
					}
				}
			}

			return null;
		}

		public override string ToString()
		{
			string output = "";
			ArrayList blocks = Blocks();
			
			output += "---- " + Layout.Name + " ----\n";

			foreach (Block block in blocks)
			{
				output += "-- " + block.Layout.Name + " --\n";

				foreach (Field field in block.Fields)
				{
					output += field.ToString() + "\n";
				}
			}

			return output;
		}

		private object GetField(byte[] byteArray, int pos, FieldType type, int fieldSize)
		{
			switch (type)
			{
				case FieldType.U8: return byteArray[pos];
				case FieldType.U16:
				case FieldType.IPPORT:
                    			return DataConvert.toU16(byteArray,pos);
				case FieldType.U32:
					return DataConvert.toU32(byteArray,pos);
				case FieldType.U64:
					return DataConvert.toU64(byteArray,pos);
				case FieldType.S8:
					return (sbyte)byteArray[pos];
				case FieldType.S16:
					return (short)DataConvert.toU16(byteArray,pos);
				case FieldType.S32:
					return (int)DataConvert.toU32(byteArray,pos);
				case FieldType.S64:
					return (long)DataConvert.toU64(byteArray,pos);
				case FieldType.F32:
					return DataConvert.toFloat(byteArray, pos);
				case FieldType.F64:
					return DataConvert.toDouble(byteArray, pos);
				case FieldType.LLUUID:
					return new LLUUID(byteArray, pos);
				case FieldType.BOOL:
					return (byteArray[pos] != 0) ? (bool)true : (bool)false;
				case FieldType.LLVector3:
					return new LLVector3(byteArray, pos);
				case FieldType.LLVector3d:
					return new LLVector3d(byteArray, pos);
				case FieldType.LLVector4:
					return new LLVector4(byteArray, pos);
				case FieldType.LLQuaternion:
					return new LLQuaternion(byteArray, pos);
				case FieldType.IPADDR:
					return new IPAddress(DataConvert.toU32(byteArray,pos));
				case FieldType.Variable:
				case FieldType.Fixed:
					byte[] bytes = new byte[fieldSize];
					Array.Copy(byteArray, pos, bytes, 0, fieldSize);
					return bytes;
			}

			return null;
		}

		public int HeaderLength()
		{
			switch (Layout.Frequency)
			{
				case PacketFrequency.High:
					return 5;
				case PacketFrequency.Medium:
					return 6;
				case PacketFrequency.Low:
					return 8;
			}

			return 0;
		}
	}

	public class PacketBuilder
	{
		public static Packet BuildPacket(string name, ProtocolManager protocol, Hashtable blocks, byte flags)
		{
			Hashtable fields;
			ArrayList payload = new ArrayList();
			byte[] byteArray = new byte[4096];
			int length = 0;
			int blockCount = 0;
			int fieldLength = 0;
			IDictionaryEnumerator blocksEnum;

			MapPacket packetMap = protocol.Command(name);

			// Build the header
			#region Header
			switch (packetMap.Frequency)
			{
				case PacketFrequency.High:
					byteArray[4] = (byte)packetMap.ID;
					length = 5;
					break;
				case PacketFrequency.Medium:
					byteArray[4] = 0xFF;
					byteArray[5] = (byte)packetMap.ID;
					length = 6;
					break;
				case PacketFrequency.Low:
					byteArray[4] = 0xFF;
					byteArray[5] = 0xFF;
					byteArray[6] = (byte)(packetMap.ID / 256);
					byteArray[7] = (byte)(packetMap.ID % 256);
					length = 8;
					break;
			}
			#endregion Header

			foreach (MapBlock blockMap in packetMap.Blocks)
			{
				// If this is a variable count block, count the number of appearances of this block in the 
				// passed in Hashtable and prepend a counter byte
				#region VariableSize
				if (blockMap.Count == -1)
				{
					blockCount = 0;

					// Count the number of this type of block in the blocks Hashtable
					blocksEnum = blocks.GetEnumerator();

					while (blocksEnum.MoveNext())
					{
						if ((string)blocksEnum.Value == blockMap.Name)
						{
							blockCount++;
						}
					}

					if (blockCount > 255)
					{
                        throw new Exception("Trying to put more than 255 blocks in a variable block");
					}

					// Prepend the blocks with a count
					byteArray[length] = (byte)blockCount;
					length++;
				}
				#endregion VariableSize

				// Reset blockCount
				blockCount = 0;

				// Check for blocks of this type in the Hashtable
				#region BuildBlock
				blocksEnum = blocks.GetEnumerator();

				while (blocksEnum.MoveNext())
				{
					if ((string)blocksEnum.Value == blockMap.Name)
					{
						// Found a match of this block
						if ((blockMap.Count == -1 && blockCount < 255) || blockCount < blockMap.Count)
						{
							blockCount++;

							#region TryBlockTypecast
							try
							{
								fields = (Hashtable)blocksEnum.Key;
							}
							catch (Exception e)
							{
                                throw new Exception("A block Hashtable did not contain a fields Hashtable", e);
							}
							#endregion TryBlockTypecast

							foreach (MapField fieldMap in blockMap.Fields)
							{
								if (fields.ContainsKey(fieldMap.Name))
								{
									object field = fields[fieldMap.Name];

									#region AddField
									switch (fieldMap.Type)
									{
										case FieldType.U8:
											byteArray[length++] = (byte)field;
											break;
										case FieldType.U16: 
											Array.Copy(DataConvert.from((ushort)field), 0, byteArray, length+=2, 2);
											break;
										case FieldType.U32:
											Array.Copy(DataConvert.from((uint)field), 0, byteArray, length+=4, 4);
											break;
										case FieldType.U64:
											Array.Copy(DataConvert.from((ulong)field), 0, byteArray, length+=8, 8);
											break;
										case FieldType.S8:
											byteArray[length++] = (byte)((sbyte)field);
											break;
										case FieldType.S16:
											// FIXME: Apply endianness patch
											Array.Copy(BitConverter.GetBytes((short)field), 0, byteArray, length, 2);
											length += 2;
											break;
										case FieldType.S32:
											// FIXME: Apply endianness patch
											Array.Copy(BitConverter.GetBytes((int)field), 0, byteArray, length, 4);
											length += 4;
											break;
										case FieldType.S64:
											// FIXME: Apply endianness patch
											Array.Copy(BitConverter.GetBytes((long)field), 0, byteArray, length, 8);
											length += 8;
											break;
										case FieldType.F32:
											Array.Copy(BitConverter.GetBytes((float)field), 0, byteArray, length, 4);
											length += 4;
											break;
										case FieldType.F64:
											Array.Copy(BitConverter.GetBytes((double)field), 0, byteArray, length, 8);
											length += 8;
											break;
										case FieldType.LLUUID:
											Array.Copy(((LLUUID)field).Data, 0, byteArray, length, 16);
											length += 16;
											break;
										case FieldType.BOOL:
											byteArray[length] = (byte)((bool)field == true ? 1 : 0);
											length++;
											break;
										case FieldType.LLVector3:
											Array.Copy(((LLVector3)field).GetBytes(), 0, byteArray, length, 12);
											length += 12;
											break;
										case FieldType.LLVector3d:
											Array.Copy(((LLVector3d)field).GetBytes(), 0, byteArray, length, 24);
											length += 24;
											break;
										case FieldType.LLVector4:
											Array.Copy(((LLVector4)field).GetBytes(), 0, byteArray, length, 16);
											length += 16;
											break;
										case FieldType.LLQuaternion:
											Array.Copy(((LLQuaternion)field).GetBytes(), 0, byteArray, length, 16);
											length += 16;
											break;
										case FieldType.IPADDR:
											Array.Copy(((IPAddress)field).GetAddressBytes(), 0, byteArray, length, 4);
											length += 4;
											break;
										case FieldType.IPPORT:
											ushort fieldIPPort = (ushort)field;
											byteArray[length + 1] = (byte)(fieldIPPort % 256);
											fieldIPPort >>= 8;
											byteArray[length] = (byte)(fieldIPPort % 256);
											length += 2;
											break;
										case FieldType.Variable:
                                            if (field.GetType().IsArray)
											{
												// Assume this is a byte array
												fieldLength = ((byte[])field).Length;
											}
											else
											{
												// Assume this is a string, add 1 for the null terminator
												fieldLength = ((string)field).Length + 1;
											}

											if (fieldMap.Count == 1)
											{
												if (fieldLength > 255)
												{
                                                    throw new Exception("Variable byte field longer than 255 characters");
												}

                                                byteArray[length] = (byte)(fieldLength);
												length++;
											}
											else if (fieldMap.Count == 2)
											{
												if (fieldLength > 1024)
												{
                                                    throw new Exception("Variable byte field longer than 1024 characters");
												}

                                                byteArray[length++] = (byte)(fieldLength % 256);
												byteArray[length++] = (byte)(fieldLength / 256);
											}
											else
											{
                                                throw new Exception("Variable field with an unknown count, protocol map error");
											}
											
											if (field.GetType().IsArray)
											{
												// Assume this is a byte array
												Array.Copy((byte[])field, 0, byteArray, length, fieldLength);
											}
											else
											{
												// Assume this is a string, add 1 for the null terminator
												byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes((string)field);
												Array.Copy(stringBytes, 0, byteArray, length, stringBytes.Length);
												fieldLength = stringBytes.Length + 1;
											}

											length += fieldLength;

											break;
										case FieldType.Fixed:
											Array.Copy((byte[])field, 0, byteArray, length, fieldMap.Count);
											length += fieldMap.Count;
											break;
										default:
                                            throw new Exception("Unhandled FieldType");
									}
									#endregion AddField
								}
								else
								{
									// This field wasn't passed in, create an empty version
									#region EmptyField
									if (fieldMap.Type == FieldType.Variable)
									{
										// Just set the counter to zero and move on
										if (fieldMap.Count == 2)
										{
											length += 2;
										}
										else
										{
											if (fieldMap.Count != 1)
											{
                                                throw new Exception("Variable length field has an invalid Count");
											}

											length++;
										}
									}
									else if (fieldMap.Type == FieldType.Fixed)
									{
										length += fieldMap.Count;
									}
									else
									{
										length += (int)protocol.TypeSizes[fieldMap.Type];
									}
									#endregion EmptyField
								}
							}
						}
						else
						{
                            throw new Exception("Too many blocks");
						}
					}
				}
				#endregion BuildBlock

				// If this is a fixed count block and it doesn't appear in the Hashtable passed in, create 
				// empty filler blocks
				#region EmptyBlock
				if (blockCount == 0 && blockMap.Count != -1)
				{
					for (int i = 0; i < blockMap.Count; ++i)
					{
						foreach (MapField field in blockMap.Fields)
						{
							if (field.Type == FieldType.Variable)
							{
								length++;
							}
							else
							{
								length += (int)protocol.TypeSizes[field.Type];
							}
						}
					}
				}
				#endregion EmptyBlock
			}

			Packet packet = new Packet(byteArray, length, protocol);
			packet.Data[0] = flags;
			return packet;
		}
	}
}