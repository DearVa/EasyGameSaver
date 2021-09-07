using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_EDITOR

namespace EasyGameSaver {
	internal class ComponentTreeView : TreeView {
		private readonly GameSaver gameSaver;
		private readonly Dictionary<int, SavedMember> memberDict = new Dictionary<int, SavedMember>();
		private readonly Dictionary<int, List<int>> childrenDict = new Dictionary<int, List<int>>();

		public ComponentTreeView(GameSaver gameSaver) : base(gameSaver.treeViewState) {
			this.gameSaver = gameSaver;
			Reload();
		}

		protected override TreeViewItem BuildRoot() => new TreeViewItem { id = 0, depth = -1 };
		
		protected override IList<TreeViewItem> BuildRows(TreeViewItem root) {
			var rows = GetRows() ?? new List<TreeViewItem>(59);
			rows.Clear();
			memberDict.Clear();
			childrenDict.Clear();

			var components = gameSaver.gameObject.GetComponents(typeof(Component));
			foreach (var component in components) {
				var type = component.GetType();
				if (type == typeof(Transform) || type == typeof(GameSaver)) {
					continue;
				}

				var item = CreateTreeViewItemForComponent(component);
				var id = component.GetInstanceID();
				if (!childrenDict.ContainsKey(id)) {
					childrenDict.Add(id, new List<int>());
				}
				root.AddChild(item);
				rows.Add(item);

				var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (props.Length + fields.Length > 0) {
					if (IsExpanded(item.id)) {
						if (props.Length > 0) {
							AddComponentMembers(component, props, item, rows);
						}
						if (fields.Length > 0) {
							AddComponentMembers(component, fields, item, rows);
						}
					} else {
						item.children = CreateChildListForCollapsedParent();
					}
				}
			}

			SetupDepthsFromParentsAndChildren(root);
			return rows;
		}

		private void AddComponentMembers(Component component, IReadOnlyCollection<MemberInfo> members, TreeViewItem item, ICollection<TreeViewItem> rows) {
			item.children ??= new List<TreeViewItem>(members.Count);
			var id = component.GetInstanceID();

			foreach (var member in members) {
				if (member is FieldInfo fi && !fi.FieldType.IsSerializable || member is PropertyInfo pi && !pi.PropertyType.IsSerializable) {
					continue;
				}

				var childItem = new TreeViewItem(member.MetadataToken + id, -1, member.Name);
				childrenDict[id].Add(member.MetadataToken + id);
				memberDict.Add(member.MetadataToken + id, new SavedMember(component, member));
				item.AddChild(childItem);
				rows.Add(childItem);
			}
		}

		private static TreeViewItem CreateTreeViewItemForComponent(Component component) {
			var name = component.GetType().Name;
			var i = name.LastIndexOf('.');
			if (i != -1) {
				name = name.Substring(i);
			}
			return new TreeViewItem(component.GetInstanceID(), -1, name);
		}

		protected override IList<int> GetDescendantsThatHaveChildren(int id) => childrenDict[id];

		protected override void RowGUI(RowGUIArgs args) {
			extraSpaceBeforeIconAndLabel = 18f;

			if (args.item.depth != 0) {
				if (!memberDict.ContainsKey(args.item.id)) {
					return;
				}

				var savedMember = memberDict[args.item.id];
				var toggleRect = args.rowRect;
				toggleRect.x += GetContentIndent(args.item);
				toggleRect.width = 16f;

				EditorGUI.BeginChangeCheck();
				EditorGUI.Toggle(toggleRect, gameSaver.savedMembers.Contains(savedMember));
				if (EditorGUI.EndChangeCheck()) {
					var index = gameSaver.savedMembers.IndexOf(savedMember);
					if (index != -1) {
						gameSaver.savedMembers.RemoveAt(index);
					} else {
						gameSaver.savedMembers.Add(savedMember);
					}
					EditorUtility.SetDirty(gameSaver);
				}
			}

			base.RowGUI(args);
		}
	}
}

#endif