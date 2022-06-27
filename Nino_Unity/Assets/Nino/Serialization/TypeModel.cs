﻿using System;
using System.Reflection;
using System.Collections.Generic;

namespace Nino.Serialization
{
	/// <summary>
	/// A model of a serialized type
	/// </summary>
	internal class TypeModel
	{
		private const string HelperName = "NinoSerializationHelper";
		
		public Dictionary<ushort, MemberInfo> members;
		public Dictionary<ushort, Type> types;
		public ushort min;
		public ushort max;
		public bool valid;
		public bool includeAll;

		/// <summary>
		/// Cached Models
		/// </summary>
		private static readonly Dictionary<Type, TypeModel> TypeModels = new Dictionary<Type, TypeModel>(10);
		
		/// <summary>
		/// Generated helpers
		/// </summary>
		private static readonly Dictionary<Type, object> GeneratedSerializationHelper = new Dictionary<Type, object>(10);
		
		/// <summary>
		/// Generated helpers
		/// </summary>
		private static readonly Dictionary<Type, Action<object, Writer>> SerializeActions = new Dictionary<Type, Action<object, Writer>>(10);

		/// <summary>
		/// Add a code gen serialize action
		/// </summary>
		/// <param name="type"></param>
		/// <param name="action"></param>
		internal static void AddSerializeAction(Type type, Action<object, Writer> action)
		{
			if (SerializeActions.ContainsKey(type)) return;
			SerializeActions[type] = action;
		}

		/// <summary>
		/// try get a code gen serialize action
		/// </summary>
		/// <param name="type"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		internal static bool TryGetSerializeAction(Type type, out Action<object, Writer> action)
		{
			return SerializeActions.TryGetValue(type, out action);
		}
		
		/// <summary>
		/// Generated helpers
		/// </summary>
		private static readonly Dictionary<Type, Func<Reader, object>> DeserializeActions = new Dictionary<Type, Func<Reader, object>>(10);

		/// <summary>
		/// Add a code gen deserialize action
		/// </summary>
		/// <param name="type"></param>
		/// <param name="func"></param>
		internal static void AddDeserializeAction(Type type, Func<Reader, object> func)
		{
			if (DeserializeActions.ContainsKey(type)) return;
			DeserializeActions[type] = func;
		}

		/// <summary>
		/// try get a code gen serialize action
		/// </summary>
		/// <param name="type"></param>
		/// <param name="func"></param>
		/// <returns></returns>
		internal static bool TryGetDeserializeAction(Type type, out Func<Reader, object> func)
		{
			return DeserializeActions.TryGetValue(type, out func);
		}
		
		/// <summary>
		/// Get whether or not a type is a code gen type
		/// </summary>
		/// <param name="type"></param>
		/// <param name="helper"></param>
		/// <returns></returns>
		internal static bool TryGetHelper(Type type, out object helper)
		{
			if (GeneratedSerializationHelper.TryGetValue(type, out helper)) return helper != null;
			
			var field = type.GetField(HelperName, BindingFlags.Public | BindingFlags.Static);
			helper = field?.GetValue(null);
			GeneratedSerializationHelper[type] = helper;
			return GeneratedSerializationHelper[type] != null;
		}

		/// <summary>
		/// Try get cached model
		/// </summary>
		/// <param name="type"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		internal static void TryGetModel(Type type, out TypeModel model)
		{
			if (TypeModels.TryGetValue(type, out model)) return;
			NinoSerializeAttribute[] ns =
				(NinoSerializeAttribute[])type.GetCustomAttributes(typeof(NinoSerializeAttribute), false);
			if (ns.Length != 0) return;
			model = new TypeModel()
			{
				valid = false
			};
			TypeModels.Add(type, model);
			throw new InvalidOperationException(
				$"The type {type.FullName} does not have NinoSerialize attribute or custom importer/exporter");
		}

		/// <summary>
		/// Create a typeModel using given type
		/// </summary>
		/// <param name="type"></param>
		/// <exception cref="InvalidOperationException"></exception>
		// ReSharper disable CognitiveComplexity
		internal static TypeModel CreateModel(Type type)
			// ReSharper restore CognitiveComplexity
		{
			var model = new TypeModel
			{
				min = ushort.MaxValue,
				max = ushort.MinValue,
				valid = true,
				//fetch members
				members = new Dictionary<ushort, MemberInfo>(10),
				//fetch types
				types = new Dictionary<ushort, Type>(10)
			};
			
			//include all or not
			NinoSerializeAttribute[] ns =
				(NinoSerializeAttribute[])type.GetCustomAttributes(typeof(NinoSerializeAttribute), false);
			model.includeAll = ns[0].IncludeAll;

			//store temp attr
			NinoMemberAttribute sp;
			//flag
			const BindingFlags flags = BindingFlags.Default | BindingFlags.DeclaredOnly | BindingFlags.Public |
			                           BindingFlags.NonPublic | BindingFlags.Instance;
			ushort index;

			//fetch fields (only public and private fields that declared in the type)
			FieldInfo[] fs = type.GetFields(flags);
			//iterate fields
			foreach (var f in fs)
			{
				if (model.includeAll)
				{
					//skip nino ignore
					if (f.GetCustomAttribute(typeof(NinoIgnoreAttribute), false) != null) continue;
					index = (ushort)model.members.Count;
				}
				else
				{
					sp = f.GetCustomAttribute(typeof(NinoMemberAttribute), false) as NinoMemberAttribute;
					//not fetch all and no attribute => skip this member
					if (sp == null) continue;
					index = sp.Index;
				}
				//record field
				model.members.Add(index, f);
				model.types.Add(index, f.FieldType);
				//record min/max
				if (index < model.min)
				{
					model.min = index;
				}

				if (index > model.max)
				{
					model.max = index;
				}
			}

			//fetch properties (only public and private properties that declared in the type)
			PropertyInfo[] ps = type.GetProperties(flags);
			//iterate properties
			foreach (var p in ps)
			{
				//has to have getter and setter
				if (!(p.CanRead && p.CanWrite))
				{
					throw new InvalidOperationException(
						$"Cannot read or write property {p.Name} in {type.FullName}, cannot Serialize or Deserialize this property");
				}
				
				if (model.includeAll)
				{
					//skip nino ignore
					if (p.GetCustomAttribute(typeof(NinoIgnoreAttribute), false) != null) continue;
					index = (ushort)model.members.Count;
				}
				else
				{
					sp = p.GetCustomAttribute(typeof(NinoMemberAttribute), false) as NinoMemberAttribute;
					//not fetch all and no attribute => skip this member
					if (sp == null) continue;
					index = sp.Index;
				}
				//record property
				model.members.Add(index, p);
				model.types.Add(index, p.PropertyType);
				//record min/max
				if (index < model.min)
				{
					model.min = index;
				}

				if (index > model.max)
				{
					model.max = index;
				}
			}
			
			if (model.members.Count == 0)
			{
				model.valid = false;
			}

			TypeModels.Add(type, model);
			return model;
		}
	}
}
