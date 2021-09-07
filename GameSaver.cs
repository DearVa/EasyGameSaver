using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Reflection;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
#endif

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
		[ConditionalField(typeof(MeshRenderer))]
		[LabelLocalization("保存材质状态", SystemLanguage.ChineseSimplified)]
		private bool saveMaterial;

		[SerializeField]
		[ConditionalField("saveMaterial")]
		private bool saveMainColor = true;

		[SerializeField]
		[ConditionalField("saveMaterial")]
		private bool saveMainTexture = true;

		[SerializeField]
		[ConditionalField("saveMaterial")]
		private CollectionWrapper<SavedMaterial> savedMaterials;

		[SerializeField]
		[Tooltip("选择特定的组件和数据进行保存")]
		[LabelLocalization("自定义保存数据", SystemLanguage.ChineseSimplified)]
		internal bool customSaveData;

		[SerializeField]
		[HideInInspector]
		internal List<SavedMember> savedMembers;

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

		internal TreeView treeView;

		[NonSerialized]
		[HideInInspector]
		internal bool saved;

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
					registeredPrefab = rps.FirstOrDefault(rp => rp.prefabObject == gameObject);
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

			var mr = GetComponent<MeshRenderer>();
			if (mr == null) {
				saveMaterial = false;
			}
			bw.Write(saveMaterial);
			if (saveMaterial) {
				bw.Write(saveMainColor);
				if (saveMainColor) {
					bw.Write(mr.material.color);
				}
				bw.Write(saveMainTexture);
				if (saveMainTexture) {
					bw.Write(mr.material.mainTexture.GetInstanceID());
				}
			}

			bw.Write(customSaveData);
			if (customSaveData) {
				bw.Write(savedMembers.Count);
				foreach (var savedMember in savedMembers) {
					savedMember.Save(gameObject, bw);
				}
			}

			saved = true;
		}
	}
}