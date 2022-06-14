﻿using System;
using System.IO;
using System.Text;
using Nino.Shared;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable UnusedMember.Local

namespace Nino.Serialization
{
	// ReSharper disable UnusedParameter.Local
	public static class Deserializer
	{
		/// <summary>
		/// Default Encoding
		/// </summary>
		private static readonly Encoding DefaultEncoding = Encoding.UTF8;

		/// <summary>
		/// 缓存反射创建dict的参数数组
		/// </summary>
		private static volatile Queue<Type[]> _reflectionGenericTypePool = new Queue<Type[]>();

		/// <summary>
		/// Custom exporter
		/// </summary>
		private static readonly Dictionary<Type, ExporterDelegate> CustomExporter =
			new Dictionary<Type, ExporterDelegate>();

		/// <summary>
		/// Custom Exporter delegate that reads bytes to object
		/// </summary>
		private delegate object ExporterDelegate(Reader reader);

		/// <summary>
		/// Add custom Exporter of all type T objects
		/// </summary>
		/// <param name="func"></param>
		/// <typeparam name="T"></typeparam>
		public static void AddCustomExporter<T>(Func<Reader, T> func)
		{
			var type = typeof(T);
			if (CustomExporter.ContainsKey(type))
			{
				Logger.W($"already added custom exporter for: {type}");
				return;
			}
			CustomExporter.Add(typeof(T), (reader) => func.Invoke(reader));
		}

		/// <summary>
		/// Deserialize a NinoSerialize object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		public static T Deserialize<T>(byte[] data, Encoding encoding = null) where T : new()
		{
			T val = new T();
			return (T)Deserialize(typeof(T), val, data, encoding ?? DefaultEncoding);
		}

		/// <summary>
		/// Deserialize a NinoSerialize object
		/// </summary>
		/// <param name="type"></param>
		/// <param name="val"></param>
		/// <param name="data"></param>
		/// <param name="encoding"></param>
		/// <param name="reader"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="NullReferenceException"></exception>
		private static object Deserialize(Type type, object val, byte[] data, Encoding encoding, Reader reader = null)
		{
			//Get Attribute that indicates a class/struct to be serialized
			TypeModel.TryGetModel(type, out var model);

			//invalid model
			if (model != null)
			{
				if (!model.valid)
				{
					return ConstMgr.Null;
				}
			}

			//generate model
			if (model == null)
			{
				model = TypeModel.CreateModel(type);
			}

			//create type
			if (val == null || val == ConstMgr.Null)
			{
				val = Activator.CreateInstance(type);
			}

			//min, max index
			ushort min = model.min, max = model.max;

			void Read()
			{
				int index = 0;
				bool hasSet = model.ninoSetMembers != null;
				object[] objs = ConstMgr.EmptyParam;
				if (hasSet)
				{
					objs = ExtensibleObjectPool.RequestObjArr(model.members.Count);
				}

				//only include all model need this
				if (model.includeAll)
				{
					//read len
					var len = GetLength(reader);
					Dictionary<string, object> values = new Dictionary<string, object>(len);
					//read elements key by key
					for (int i = 0; i < len; i++)
					{
						var key = (string)ReadCommonVal(reader, ConstMgr.StringType, encoding);
						var typeFullName = (string)ReadCommonVal(reader, ConstMgr.StringType, encoding);
						var value = ReadCommonVal(reader, Type.GetType(typeFullName), encoding);
						values.Add(key,value);
					}
					
					//set elements
					for (; min <= max; min++)
					{
						//if end, skip
						if (reader.EndOfReader) continue;
						//prevent index not exist
						if (!model.types.ContainsKey(min)) continue;
						//get the member
						var member = model.members[min];
						//try get same member and set it
						if (values.TryGetValue(member.Name, out var ret))
						{
							SetMember(model.members[min], val, ret);
						}
					}
				}
				else
				{
					for (; min <= max; min++)
					{
						//if end, skip
						if (reader.EndOfReader) continue;
						//prevent index not exist
						if (!model.types.ContainsKey(min)) continue;
						//get type of that member
						type = model.types[min];
						//try code gen, if no code gen then reflection

						//read basic values
						var ret = ReadCommonVal(reader, type, encoding);
						if (hasSet)
						{
							objs[index] = ret;
						}
						else
						{
							SetMember(model.members[min], val, ret);
						}

						//add the index, so it will fetch the next member (when code gen exists)
						index++;
					}

					//invoke code gen
					if (!hasSet) return;
					object[] p = ExtensibleObjectPool.RequestObjArr(1);
					p[0] = objs;
					model.ninoSetMembers.Invoke(val, p);
					ExtensibleObjectPool.ReturnObjArr(index, objs);
					ExtensibleObjectPool.ReturnObjArr(1, p);
				}
			}

			//share a reader
			if (reader != null)
			{
				Read();
				return val;
			}

			//start Deserialize
			using (reader = new Reader(CompressMgr.Decompress(data), encoding))
			{
				Read();
				return val;
			}
		}

		/// <summary>
		/// Set value from MemberInfo
		/// </summary>
		/// <param name="info"></param>
		/// <param name="instance"></param>
		/// <param name="val"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SetMember(MemberInfo info, object instance, object val)
		{
			switch (info)
			{
				case FieldInfo fo:
					fo.SetValue(instance, val);
					break;
				case PropertyInfo po:
					po.SetValue(instance, val);
					break;
				default:
					return;
			}
		}
		
		/// <summary>
		/// Get Length
		/// </summary>
		/// <param name="reader"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetLength(Reader reader)
		{
			switch (reader.GetCompressType())
			{
				case CompressType.Byte:
					return reader.ReadByte();
				case CompressType.SByte:
					return reader.ReadSByte();
				case CompressType.Int16:
					return reader.ReadInt16();
				case CompressType.UInt16:
					return reader.ReadUInt16();
				case CompressType.Int32:
					return reader.ReadInt32();
			}

			return 0;
		}

		/// <summary>
		/// Read primitive value from binary writer
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="type"></param>
		/// <param name="encoding"></param>
		/// <exception cref="InvalidDataException"></exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static object ReadCommonVal(Reader reader, Type type, Encoding encoding)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Byte:
					return reader.ReadByte();

				case TypeCode.SByte:
					return reader.ReadSByte();

				case TypeCode.Int16:
					return reader.ReadInt16();

				case TypeCode.UInt16:
					return reader.ReadUInt16();
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
					var i = reader.GetCompressType();
					switch (i)
					{
						case CompressType.Byte:
							return reader.ReadByte();
						case CompressType.SByte:
							return reader.ReadSByte();
						case CompressType.Int16:
							return reader.ReadInt16();
						case CompressType.UInt16:
							return reader.ReadUInt16();
						case CompressType.Int32:
							return reader.ReadInt32();
						case CompressType.UInt32:
							return reader.ReadUInt32();
						case CompressType.Int64:
							return reader.ReadInt64();
						case CompressType.UInt64:
							return reader.ReadUInt64();
						default:
							throw new InvalidOperationException("invalid compress type");
					}
				case TypeCode.String:
					return reader.ReadString();
				case TypeCode.Boolean:
					return reader.ReadBool();
				case TypeCode.Double:
					return reader.ReadDouble();
				case TypeCode.Single:
					return reader.ReadSingle();
				case TypeCode.Decimal:
					return reader.ReadDecimal();
				case TypeCode.Char:
					return reader.ReadChar();
			}

			//enum
			if (type.IsEnum)
			{
				//try decompress and read
				return DecompressAndReadEnum(reader, type, encoding);
			}

			//array/ list -> recursive
			if (type.IsArray)
			{
				//read len
				int len = GetLength(reader);

				//byte[] -> write directly
				if (type == ConstMgr.ByteArrType)
				{
					//read item
					return reader.ReadBytes(len);
				}

				//other type
				var elemType = type.GetElementType();
				if (elemType == null)
				{
					throw new NullReferenceException("element type is null, can not make array");
				}

				var arr = Array.CreateInstance(elemType, len);
				//read item
				for (int i = 0; i < len; i++)
				{
					var obj = ReadCommonVal(reader, elemType, encoding);
					if (obj.GetType() != elemType)
					{
						obj = Convert.ChangeType(obj, elemType);
					}

					arr.SetValue(obj, i);
				}

				return arr;
			}

			if (type.IsGenericType)
			{
				var genericDefType = type.GetGenericTypeDefinition();

				//list
				if (genericDefType == ConstMgr.ListDefType)
				{
					//read len
					int len = GetLength(reader);

					//List<byte> -> write directly
					if (type == ConstMgr.ByteListType)
					{
						return reader.ReadBytes(len).ToList();
					}

					//other
					var elemType = type.GenericTypeArguments[0];
					Type newType = ConstMgr.ListDefType.MakeGenericType(elemType);
					var arr = Activator.CreateInstance(newType, ConstMgr.EmptyParam) as IList;
					//read item
					for (int i = 0; i < len; i++)
					{
						var obj = ReadCommonVal(reader, elemType, encoding);
						if (obj.GetType() != elemType)
						{
							obj = Convert.ChangeType(obj, elemType);
						}

						arr?.Add(obj);
					}

					return arr;
				}

				//dict
				if (genericDefType == ConstMgr.DictDefType)
				{
					//parse dict type
					var args = type.GetGenericArguments();
					Type keyType = args[0];
					Type valueType = args[1];
					Type[] temp;
					if (_reflectionGenericTypePool.Count > 0)
					{
						temp = _reflectionGenericTypePool.Dequeue();
						temp[0] = keyType;
						temp[1] = valueType;
					}
					else
					{
						// ReSharper disable RedundantExplicitArrayCreation
						temp = new Type[] { keyType, valueType };
						// ReSharper restore RedundantExplicitArrayCreation
					}

					Type dictType = ConstMgr.DictDefType.MakeGenericType(temp);
					_reflectionGenericTypePool.Enqueue(temp);
					var dict = Activator.CreateInstance(dictType) as IDictionary;

					//read len
					int len = GetLength(reader);

					//read item
					for (int i = 0; i < len; i++)
					{
						//read key
						var key = ReadCommonVal(reader, keyType, encoding);
						if (key.GetType() != keyType)
						{
							key = Convert.ChangeType(key, keyType);
						}

						//read value
						var val = ReadCommonVal(reader, valueType, encoding);
						if (val.GetType() != valueType)
						{
							val = Convert.ChangeType(val, valueType);
						}

						//add
						dict?.Add(key, val);
					}

					return dict;
				}
			}

			//custom exporter
			if (CustomExporter.TryGetValue(type, out var exporterDelegate))
			{
				return exporterDelegate.Invoke(reader);
			}
			else
			{
				//no chance to Deserialize -> see if this type can be serialized in other ways
				//try recursive
				return Deserialize(type, ConstMgr.Null, ConstMgr.Null, encoding, reader);
			}
		}

		/// <summary>
		/// Compress and write enum
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="type"></param>
		/// <param name="encoding"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static object DecompressAndReadEnum(Reader reader, Type type,
			Encoding encoding)
		{
			type = Enum.GetUnderlyingType(type);
			//typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
			//typeof(int), typeof(uint), typeof(long), typeof(ulong)
			return ReadCommonVal(reader, type, encoding);
		}
	}
	// ReSharper restore UnusedParameter.Local
}