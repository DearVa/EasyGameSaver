using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.IMGUI.Controls;
using UnityEditor;
using UnityEngine;

namespace EasyGameSaver {
	public static class Utils {
		private static readonly BinaryFormatter Bf = new BinaryFormatter();
		private static readonly MD5 Md5 = new MD5CryptoServiceProvider();

		public static void Write(this BinaryWriter bw, Vector3 v3) {
			bw.Write(v3.x);
			bw.Write(v3.y);
			bw.Write(v3.z);
		}

		public static void Write(this BinaryWriter bw, Vector4 v4) {
			bw.Write(v4.x);
			bw.Write(v4.y);
			bw.Write(v4.z);
			bw.Write(v4.w);
		}

		public static void Write(this BinaryWriter bw, Quaternion q) {
			bw.Write(q.x);
			bw.Write(q.y);
			bw.Write(q.z);
			bw.Write(q.w);
		}

		public static void Write(this BinaryWriter bw, Matrix4x4 m) {
			bw.Write(m.m00); bw.Write(m.m01); bw.Write(m.m02); bw.Write(m.m03);
			bw.Write(m.m10); bw.Write(m.m11); bw.Write(m.m12); bw.Write(m.m13);
			bw.Write(m.m20); bw.Write(m.m21); bw.Write(m.m22); bw.Write(m.m23);
			bw.Write(m.m30); bw.Write(m.m31); bw.Write(m.m32); bw.Write(m.m33);
		}

		public static void Write(this BinaryWriter bw, Color c) {
			bw.Write(c.r);
			bw.Write(c.g);
			bw.Write(c.b);
			bw.Write(c.a);
		}

		public static void WriteArray(this BinaryWriter bw, float[] array) {
			bw.Write(array.Length);
			foreach (var v in array) {
				bw.Write(v);
			}
		}

		public static void WriteArray(this BinaryWriter bw, Color[] array) {
			bw.Write(array.Length);
			foreach (var v in array) {
				bw.Write(v);
			}
		}
		
		public static void WriteArray(this BinaryWriter bw, Vector4[] array) {
			bw.Write(array.Length);
			foreach (var v in array) {
				bw.Write(v);
			}
		}

		public static void WriteArray(this BinaryWriter bw, Matrix4x4[] array) {
			bw.Write(array.Length);
			foreach (var v in array) {
				bw.Write(v);
			}
		}

		public static void Write(this BinaryWriter bw, object obj) {
			var type = obj.GetType();
			if (type.IsSerializable) {
				Bf.Serialize(bw.BaseStream, obj);
			} else {
				throw new InvalidOperationException();
			}
		}

		public static object ReadObject(this BinaryReader br) => Bf.Deserialize(br.BaseStream);

		public static Vector3 ReadVector3(this BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

		public static Vector4 ReadVector4(this BinaryReader br) => new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

		public static Quaternion ReadQuaternion(this BinaryReader br) => new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

		public static Matrix4x4 ReadMatrix4x4(this BinaryReader br) =>
			new Matrix4x4() {
				m00 = br.ReadSingle(), m01 = br.ReadSingle(), m02 = br.ReadSingle(), m03 = br.ReadSingle(),
				m10 = br.ReadSingle(), m11 = br.ReadSingle(), m12 = br.ReadSingle(), m13 = br.ReadSingle(),
				m20 = br.ReadSingle(), m21 = br.ReadSingle(), m22 = br.ReadSingle(), m23 = br.ReadSingle(),
				m30 = br.ReadSingle(), m31 = br.ReadSingle(), m32 = br.ReadSingle(), m33 = br.ReadSingle()
			};

		public static Color ReadColor(this BinaryReader br) => new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

		public static float[] ReadFloatArray(this BinaryReader br) {
			var r = new float[br.ReadInt32()];
			for (var i = 0; i < r.Length; i++) {
				r[i] = br.ReadSingle();
			}
			return r;
		}

		public static Color[] ReadColorArray(this BinaryReader br) {
			var r = new Color[br.ReadInt32()];
			for (var i = 0; i < r.Length; i++) {
				r[i] = br.ReadColor();
			}
			return r;
		}

		public static Vector4[] ReadVector4Array(this BinaryReader br) {
			var r = new Vector4[br.ReadInt32()];
			for (var i = 0; i < r.Length; i++) {
				r[i] = br.ReadVector4();
			}
			return r;
		}

		public static Matrix4x4[] ReadMatrix4x4Array(this BinaryReader br) {
			var r = new Matrix4x4[br.ReadInt32()];
			for (var i = 0; i < r.Length; i++) {
				r[i] = br.ReadMatrix4x4();
			}
			return r;
		}

		public static string GenerateHash(string name, IEnumerable<string> hashes) {
			string hash;
			var i = 0;
			do {
				hash = BitConverter.ToString(Utils.Md5.ComputeHash(Encoding.UTF8.GetBytes(name + i++))).ToLower().Substring(0, 16).Replace("-", "");
			} while (hashes.Any(s => s == hash));
			return hash;
		}
	}

	/// <summary>
	/// Conditionally Show/Hide field in inspector, based on some other field value
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class ConditionalFieldAttribute : PropertyAttribute {
		public readonly string fieldToCheck;
		public readonly Type componentToCheck;
		public readonly string[] compareValues;
		public readonly bool inverse;

		/// <param name="fieldToCheck">String name of field to check value</param>
		/// <param name="inverse">Inverse check result</param>
		/// <param name="compareValues">On which values field will be shown in inspector</param>
		public ConditionalFieldAttribute(string fieldToCheck, bool inverse = false, params object[] compareValues) {
			this.fieldToCheck = fieldToCheck;
			this.inverse = inverse;
			this.compareValues = compareValues.Select(c => c.ToString().ToUpper()).ToArray();
		}

		public ConditionalFieldAttribute(Type component, bool inverse = false) {
			componentToCheck = component;
			this.inverse = inverse;
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(GameSaver))]
	public class GameSaverCustomEditor : Editor {
		public override void OnInspectorGUI() {
			DrawDefaultInspector();
			var gs = (GameSaver)target;
			if (gs.customSaveData) {
				gs.treeViewState ??= new TreeViewState();
				gs.savedMembers ??= new List<SavedMember>();
				gs.treeView ??= new ComponentTreeView(gs);
				var rect = GUILayoutUtility.GetRect(0, Screen.width, 0, gs.treeView.totalHeight);
				gs.treeView.OnGUI(rect);
				if (GUILayout.Button("Ë¢ÐÂ")) {
					gs.treeView.Reload();
				}
			}
		}
	}

	[CustomPropertyDrawer(typeof(ConditionalFieldAttribute))]
	public class ConditionalFieldAttributeDrawer : PropertyDrawer {
		private bool toShow = true;

		/// <summary>
		/// Key is Associated with drawer type (the T in [CustomPropertyDrawer(typeof(T))])
		/// Value is PropertyDrawer Type
		/// </summary>
		private static Dictionary<Type, Type> allPropertyDrawersInDomain;

		private bool initialized;
		private PropertyDrawer customAttributeDrawer;
		private PropertyDrawer customTypeDrawer;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			if (!(attribute is ConditionalFieldAttribute conditional)) {
				return 0;
			}

			Initialize(property);

			if (conditional.fieldToCheck == null) {
				var go = ((GameSaver)property.serializedObject.targetObject).gameObject;
				if (go == null) {
					return 0;
				}
				toShow = go.GetComponent(conditional.componentToCheck) != null;
				if (conditional.inverse) {
					toShow = !toShow;
				}
			} else {
				var propertyToCheck = ConditionalFieldUtility.FindRelativeProperty(property, conditional.fieldToCheck);
				toShow = ConditionalFieldUtility.PropertyIsVisible(propertyToCheck, conditional.inverse, conditional.compareValues);
			}

			if (!toShow) {
				return 0;
			}
			if (customAttributeDrawer != null) {
				return customAttributeDrawer.GetPropertyHeight(property, label);
			}
			if (customTypeDrawer != null) {
				return customTypeDrawer.GetPropertyHeight(property, label);
			}

			return EditorGUI.GetPropertyHeight(property);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			if (!toShow) {
				return;
			}

			if (customAttributeDrawer != null) {
				TryUseAttributeDrawer();
			} else if (customTypeDrawer != null) {
				TryUseTypeDrawer();
			} else {
				EditorGUI.PropertyField(position, property, label, true);
			}


			void TryUseAttributeDrawer() {
				try {
					customAttributeDrawer.OnGUI(position, property, label);
				} catch (Exception e) {
					EditorGUI.PropertyField(position, property, label);
					LogWarning("Unable to use Custom Attribute Drawer " + customAttributeDrawer.GetType() + " : " + e, property);
				}
			}

			void TryUseTypeDrawer() {
				try {
					customTypeDrawer.OnGUI(position, property, label);
				} catch (Exception e) {
					EditorGUI.PropertyField(position, property, label);
					LogWarning("Unable to instantiate " + fieldInfo.FieldType + " : " + e, property);
				}
			}
		}


		private void Initialize(SerializedProperty property) {
			if (initialized) {
				return;
			}

			CacheAllDrawersInDomain();

			TryGetCustomAttributeDrawer();
			TryGetCustomTypeDrawer();

			initialized = true;


			static void CacheAllDrawersInDomain() {
				if (allPropertyDrawersInDomain != null) {
					return;
				}

				allPropertyDrawersInDomain = new Dictionary<Type, Type>();
				var propertyDrawerType = typeof(PropertyDrawer);

				var allDrawerTypesInDomain = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(x => x.GetTypes())
					.Where(t => propertyDrawerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

				foreach (var type in allDrawerTypesInDomain) {
					var drawerAttribute = CustomAttributeData.GetCustomAttributes(type).FirstOrDefault();
					if (drawerAttribute == null) {
						continue;
					}
					var associatedType = drawerAttribute.ConstructorArguments.FirstOrDefault().Value as Type;
					if (associatedType == null) {
						continue;
					}

					if (allPropertyDrawersInDomain.ContainsKey(associatedType)) {
						continue;
					}
					allPropertyDrawersInDomain.Add(associatedType, type);
				}
			}

			void TryGetCustomAttributeDrawer() {
				if (fieldInfo == null) {
					return;
				}
				//Get the second attribute flag
				var secondAttribute = (PropertyAttribute)fieldInfo.GetCustomAttributes(typeof(PropertyAttribute), false)
					.FirstOrDefault(a => !(a is ConditionalFieldAttribute));
				if (secondAttribute == null) {
					return;
				}
				var genericAttributeType = secondAttribute.GetType();

				//Get the associated attribute drawer
				if (!allPropertyDrawersInDomain.ContainsKey(genericAttributeType)) {
					return;
				}

				var customAttributeDrawerType = allPropertyDrawersInDomain[genericAttributeType];
				var customAttributeData = fieldInfo.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType == secondAttribute.GetType());
				if (customAttributeData == null) {
					return;
				}


				//Create drawer for custom attribute
				try {
					customAttributeDrawer = (PropertyDrawer)Activator.CreateInstance(customAttributeDrawerType);
					var attributeField = customAttributeDrawerType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
					if (attributeField != null) {
						attributeField.SetValue(customAttributeDrawer, secondAttribute);
					}
				} catch (Exception e) {
					LogWarning("Unable to construct drawer for " + secondAttribute.GetType() + " : " + e, property);
				}
			}

			void TryGetCustomTypeDrawer() {
				if (fieldInfo == null) {
					return;
				}
				// Skip checks for mscorlib.dll
				if (fieldInfo.FieldType.Module.ScopeName.Equals(typeof(int).Module.ScopeName)) {
					return;
				}

				// Of all property drawers in the assembly we need to find one that affects target type
				// or one of the base types of target type
				Type fieldDrawerType = null;
				var fieldType = fieldInfo.FieldType;
				while (fieldType != null) {
					if (allPropertyDrawersInDomain.ContainsKey(fieldType)) {
						fieldDrawerType = allPropertyDrawersInDomain[fieldType];
						break;
					}

					fieldType = fieldType.BaseType;
				}

				if (fieldDrawerType == null) {
					return;
				}

				//Create instances of each (including the arguments)
				try {
					customTypeDrawer = (PropertyDrawer)Activator.CreateInstance(fieldDrawerType);
				} catch (Exception e) {
					LogWarning("No constructor available in " + fieldType + " : " + e, property);
					return;
				}

				//Reassign the attribute field in the drawer so it can access the argument values
				var attributeField = fieldDrawerType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
				if (attributeField != null) {
					attributeField.SetValue(customTypeDrawer, attribute);
				}
				var fieldInfoField = fieldDrawerType.GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);
				if (fieldInfoField != null) {
					fieldInfoField.SetValue(customTypeDrawer, fieldInfo);
				}
			}
		}

		private void LogWarning(string log, SerializedProperty property) {
			var warning = "Property <color=brown>" + fieldInfo.Name + "</color>";
			if (fieldInfo != null && fieldInfo.DeclaringType != null) {
				warning += " on behaviour <color=brown>" + fieldInfo.DeclaringType.Name + "</color>";
			}
			warning += " caused: " + log;

			Debug.LogWarning(warning, property.serializedObject.targetObject);
		}
	}

	public static class ConditionalFieldUtility {
		#region Property Is Visible

		public static bool PropertyIsVisible(SerializedProperty property, bool inverse, string[] compareAgainst) {
			if (property == null) {
				return true;
			}

			var asString = property.AsStringValue().ToUpper();

			if (compareAgainst != null && compareAgainst.Length > 0) {
				var matchAny = CompareAgainstValues(asString, compareAgainst, IsFlagsEnum());
				if (inverse) {
					matchAny = !matchAny;
				}
				return matchAny;
			}

			var someValueAssigned = asString != "FALSE" && asString != "0" && asString != "NULL";
			if (someValueAssigned) {
				return !inverse;
			}

			return inverse;

			bool IsFlagsEnum() {
				if (property.propertyType != SerializedPropertyType.Enum) {
					return false;
				}
				var value = property.objectReferenceValue;
				if (value == null) {
					return false;
				}
				return value.GetType().GetCustomAttribute<FlagsAttribute>() != null;
			}
		}

		/// <summary>
		/// True if the property value matches any of the values in '_compareValues'
		/// </summary>
		private static bool CompareAgainstValues(string propertyValueAsString, string[] compareAgainst, bool handleFlags) {
			if (!handleFlags) {
				return ValueMatches(propertyValueAsString);
			}

			var separateFlags = propertyValueAsString.Split(',');
			return separateFlags.Any(flag => ValueMatches(flag.Trim()));


			bool ValueMatches(string value) {
				return compareAgainst.Any(compare => value == compare);
			}
		}

		#endregion

		#region Find Relative Property

		public static SerializedProperty FindRelativeProperty(SerializedProperty property, string propertyName) {
			if (property.depth == 0) {
				return property.serializedObject.FindProperty(propertyName);
			}

			var path = property.propertyPath.Replace(".Array.data[", "[");
			var elements = path.Split('.');

			var nestedProperty = NestedPropertyOrigin(property, elements);

			// if nested property is null = we hit an array property
			if (nestedProperty == null) {
				var cleanPath = path.Substring(0, path.IndexOf('['));
				var arrayProp = property.serializedObject.FindProperty(cleanPath);
				var target = arrayProp.serializedObject.targetObject;

				var who = "Property <color=brown>" + arrayProp.name + "</color> in object <color=brown>" + target.name + "</color> caused: ";
				var warning = who + "Array fields is not supported by [ConditionalFieldAttribute]. Consider to use <color=blue>CollectionWrapper</color>";

				Debug.LogWarning(warning, target);

				return null;
			}

			return nestedProperty.FindPropertyRelative(propertyName);
		}

		// For [Serialized] types with [Conditional] fields
		private static SerializedProperty NestedPropertyOrigin(SerializedProperty property, string[] elements) {
			SerializedProperty parent = null;

			for (var i = 0; i < elements.Length - 1; i++) {
				var element = elements[i];
				var index = -1;
				if (element.Contains("[")) {
					index = Convert.ToInt32(element.Substring(element.IndexOf("[", StringComparison.Ordinal))
						.Replace("[", "").Replace("]", ""));
					element = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
				}

				parent = i == 0
					? property.serializedObject.FindProperty(element)
					: parent?.FindPropertyRelative(element);

				if (index >= 0 && parent != null) {
					parent = parent.GetArrayElementAtIndex(index);
				}
			}

			return parent;
		}

		#endregion

		#region Behaviour Property Is Visible

		public static bool BehaviourPropertyIsVisible(UnityEngine.Object obj, string propertyName, ConditionalFieldAttribute appliedAttribute) {
			if (string.IsNullOrEmpty(appliedAttribute.fieldToCheck)) {
				return true;
			}

			var so = new SerializedObject(obj);
			var property = so.FindProperty(propertyName);
			var targetProperty = FindRelativeProperty(property, appliedAttribute.fieldToCheck);

			return PropertyIsVisible(targetProperty, appliedAttribute.inverse, appliedAttribute.compareValues);
		}

		public static string AsStringValue(this SerializedProperty property) {
			switch (property.propertyType) {
			case SerializedPropertyType.String:
				return property.stringValue;

			case SerializedPropertyType.Character:
			case SerializedPropertyType.Integer:
				return property.type == "char" ? Convert.ToChar(property.intValue).ToString() : property.intValue.ToString();

			case SerializedPropertyType.ObjectReference:
				return property.objectReferenceValue != null ? property.objectReferenceValue.ToString() : "null";

			case SerializedPropertyType.Boolean:
				return property.boolValue.ToString();

			case SerializedPropertyType.Enum:
				return property.enumNames[property.enumValueIndex];

			default:
				return string.Empty;
			}
		}

		#endregion
	}
#endif

	[Serializable]
	public class CollectionWrapper<T> : CollectionWrapperBase {
		public T[] Value;
	}

	[Serializable]
	public class CollectionWrapperBase { }

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(CollectionWrapperBase), true)]
	public class CollectionWrapperDrawer : PropertyDrawer {
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			var collection = property.FindPropertyRelative("Value");
			return EditorGUI.GetPropertyHeight(collection, true);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			var collection = property.FindPropertyRelative("Value");
			EditorGUI.PropertyField(position, collection, label, true);
		}
	}
#endif

	public class LabelLocalization : PropertyAttribute {
		private readonly string label;
		private readonly SystemLanguage lang;

		public LabelLocalization(string label, SystemLanguage lang) {
			this.label = label;
			this.lang = lang;
		}

#if UNITY_EDITOR
		[CustomPropertyDrawer(typeof(LabelLocalization))]
		public class ThisPropertyDrawer : PropertyDrawer {
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
				if (attribute is LabelLocalization propertyAttribute) {
					if (propertyAttribute.lang != Application.systemLanguage) {
						EditorGUI.PropertyField(position, property, label);
						return;
					}
					if (IsItBloodyArrayTho(property) == false) {
						label.text = propertyAttribute.label;
					} else {
						Debug.LogWarningFormat(
							"{0}(\"{1}\") doesn't support arrays ",
							nameof(LabelLocalization),
							propertyAttribute.label
						);
					}
					EditorGUI.PropertyField(position, property, label);
				}
			}

			private static bool IsItBloodyArrayTho(SerializedProperty property) {
				var path = property.propertyPath;
				var idot = path.IndexOf('.');
				if (idot == -1) {
					return false;
				}
				var propName = path.Substring(0, idot);
				var p = property.serializedObject.FindProperty(propName);
				return p.isArray;
				//CREDITS: https://answers.unity.com/questions/603882/serializedproperty-isnt-being-detected-as-an-array.html
			}
		}
#endif
	}

}