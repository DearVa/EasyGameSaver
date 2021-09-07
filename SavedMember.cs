using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EasyGameSaver {
	[Serializable]
	internal class SavedMember {
		public string componentName;
		public string memberName;

		public SavedMember(Component component, MemberInfo memberInfo) {
			componentName = component.GetType().FullName;
			var index = componentName.LastIndexOf('.');
			if (index != -1) {
				componentName = componentName.Substring(index + 1);
			}
			memberName = memberInfo.Name;
		}

		public void Save(GameObject go, BinaryWriter bw) {
			var component = go.GetComponent(componentName);
			if (component != null) {
				var membersInfos = component.GetType().GetMember(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (membersInfos.Length > 0) {
					bw.Write(true);
					bw.Write(componentName);
					bw.Write(memberName);
					switch (membersInfos[0]) {
					case FieldInfo fi:
						bw.Write(fi.GetValue(component));
						break;
					case PropertyInfo pi:
						bw.Write(pi.GetValue(component));
						break;
					default:
						bw.Write(false);
						Debug.LogWarning($"Cannot save GameObject: {go} Component: {componentName} Member: {memberName}. Not support.");
						break;
					}
				} else {
					bw.Write(false);
					Debug.LogWarning($"Cannot save GameObject: {go} Component: {componentName} Member: {memberName}. Member not found.");
				}
			} else {
				bw.Write(false);
				Debug.LogWarning($"Cannot save GameObject: {go} Component: {componentName}. Component not found.");
			}
		}

		/// <summary>
		/// 内部方法，你应该使用GameSaveMgr.LoadGame保存当前Level
		/// </summary>
		internal bool Load(GameObject go, object value) {
			if (string.IsNullOrWhiteSpace(componentName) || string.IsNullOrWhiteSpace(memberName)) {
				return false;
			}
			var component = go.GetComponent(componentName);
			if (component != null) {
				var membersInfos = component.GetType().GetMember(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (membersInfos.Length > 0) {
					switch (membersInfos[0]) {
					case FieldInfo fi:
						fi.SetValue(component, value);
						return true;
					case PropertyInfo pi:
						pi.SetValue(component, value);
						return true;
					}
				}
			}
			return false;
		}

		public override int GetHashCode() => componentName.GetHashCode() + memberName.GetHashCode();

		public override bool Equals(object obj) => obj is SavedMember sm && sm.componentName == componentName && sm.memberName == memberName;
	}

	[Serializable]
	public class RegisteredPrefab {
		public GameObject prefabObject;
		public string hash;

		public RegisteredPrefab(GameObject prefabObject, string hash) {
			this.prefabObject = prefabObject;
			this.hash = hash;
		}
	}
}