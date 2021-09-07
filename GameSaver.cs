using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Debug = UnityEngine.Debug;
using UnityEditor.IMGUI.Controls;
using UnityEngine.SceneManagement;

namespace EasyGameSaver {
	public class GameSaver : MonoBehaviour {
		[SerializeField]
		[LabelLocalization("启用", SystemLanguage.ChineseSimplified)]
		private new bool enabled = true;

		[SerializeField]
		[LabelLocalization("保存Transform", SystemLanguage.ChineseSimplified)]
		private bool saveTransform = true;
		 
		[SerializeField]
		[ConditionalField("saveTransform")]
		[LabelLocalization("保存Position", SystemLanguage.ChineseSimplified)]
		private bool savePosition = true;

		[SerializeField]
		[ConditionalField("saveTransform")]
		[LabelLocalization("保存Rotation", SystemLanguage.ChineseSimplified)]
		private bool saveRotation = true;

		[SerializeField]
		[ConditionalField("saveTransform")]
		[LabelLocalization("保存Scale", SystemLanguage.ChineseSimplified)]
		private bool saveScale = true;

		[SerializeField]
		[ConditionalField("saveTransform")]
		[LabelLocalization("使用本地坐标系", SystemLanguage.ChineseSimplified)]
		private bool useLocalTransform;

		[SerializeField]
		[ConditionalField(typeof(Rigidbody))]
		[LabelLocalization("保存刚体组件", SystemLanguage.ChineseSimplified)]
		private bool saveRigidbody;

		[SerializeField]
		[ConditionalField("saveRigidbody")]
		[LabelLocalization("保存速度", SystemLanguage.ChineseSimplified)]
		private bool saveVelocity = true;

		[SerializeField]
		[ConditionalField("saveRigidbody")]
		[LabelLocalization("保存角速度", SystemLanguage.ChineseSimplified)]
		private bool saveAngularVelocity = true;

		[SerializeField]
		[HideInInspector]
		private bool isPrefab;

		internal string instanceHash;

		[SerializeField] 
		[HideInInspector] 
		internal RegisteredPrefab registeredPrefab;
		
		[SerializeField]
		[HideInInspector]
		internal TreeViewState treeViewState;

		public TreeView treeView;

		[SerializeField]
		[Tooltip("选择特定的组件和数据进行保存")]
		[LabelLocalization("自定义保存数据", SystemLanguage.ChineseSimplified)]
		internal bool customSaveData;
		
		[SerializeField]
		[HideInInspector]
		internal List<SavedMember> savedMembers;

		[NonSerialized]
		[HideInInspector]
		public bool saved;

		internal static readonly Dictionary<string, GameSaver> PrefabInstances = new Dictionary<string, GameSaver>();
		
		private void OnValidate() {
			if (gameObject.scene.rootCount == 0) {
				if (!isPrefab) {
					isPrefab = true;
					List<RegisteredPrefab> rps;
					if (GameSaveMgr.Instance == null) {
						var gsm = new GameObject("GameSaveMgr").AddComponent<GameSaveMgr>();
						rps = gsm.registeredPrefabs = new List<RegisteredPrefab>();
					} else if (GameSaveMgr.Instance.registeredPrefabs == null) {
						rps = GameSaveMgr.Instance.registeredPrefabs = new List<RegisteredPrefab>();
					} else {
						rps = GameSaveMgr.Instance.registeredPrefabs;
					}
					registeredPrefab = rps.FirstOrDefault(rp => rp.prefabObject == gameObject);;
					if (registeredPrefab == null) {
						registeredPrefab = new RegisteredPrefab(gameObject, Utils.GenerateHash(name, rps.Select(rp => rp.hash)));
						rps.Add(registeredPrefab);
					}
				}
			} else if (!Application.isPlaying) {
				isPrefab = false;
			}
		}

		private void Awake() {
			if (!GameSaveMgr.GameSavers.Contains(this)) {
				GameSaveMgr.GameSavers.Add(this);
			}
			if (isPrefab && !PrefabInstances.ContainsValue(this)) {
				instanceHash = Utils.GenerateHash(Path.GetRandomFileName(), PrefabInstances.Keys);
				PrefabInstances.Add(instanceHash, this);
			}
		}

		/// <summary>
		/// 内部方法，你应该使用GameSaveMgr.SaveGame保存当前Level
		/// </summary>
		internal void Save(BinaryWriter bw) {
			bw.Write(enabled);
			if (!enabled) {
				return;
			}

			var parent = transform.parent;
			while (parent != null) {
				var gs = parent.GetComponent<GameSaver>();
				if (gs != null && !gs.saved) {
					gs.Save(bw);
				}
				parent = parent.parent;
			}
			
			parent = transform.parent;
			if (parent == null) {
				bw.Write(-1);
			} else {
				var gs = parent.GetComponent<GameSaver>();
				if (gs == null || !gs.isPrefab) {
					bw.Write(parent.GetInstanceID());
				} else {
					bw.Write(0);
					bw.Write(gs.instanceHash);
				}
			}

			bw.Write(isPrefab);
			if (isPrefab) {
				bw.Write(registeredPrefab.hash);
				bw.Write(instanceHash);
			} else {
				bw.Write(gameObject.GetInstanceID());
			}

			bw.Write(saveTransform);
			if (saveTransform) {
				bw.Write(useLocalTransform);
				bw.Write(savePosition);
				if (savePosition) {
					bw.Write(useLocalTransform ? transform.localPosition : transform.position);
				}
				bw.Write(saveRotation);
				if (saveRotation) {
					bw.Write(useLocalTransform ? transform.localRotation : transform.rotation);
				}
				bw.Write(saveScale);
				if (saveScale) {
					bw.Write(transform.localScale);
				}
			}
			var rb = GetComponent<Rigidbody>();
			if (rb == null) {
				saveRigidbody = false;
			}
			bw.Write(saveRigidbody);
			if (saveRigidbody) {
				bw.Write(saveVelocity);
				if (saveVelocity) {
					bw.Write(rb.velocity);
				}
				bw.Write(saveAngularVelocity);
				if (saveAngularVelocity) {
					bw.Write(rb.angularVelocity);
				}
			}

			bw.Write(savedMembers.Count);
			foreach (var savedMember in savedMembers) {
				savedMember.Save(gameObject, bw);
			}

			saved = true;
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
				if (GUILayout.Button("刷新")) {
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