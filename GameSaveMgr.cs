using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace EasyGameSaver {
	public class GameSaveMgr : MonoBehaviour {
		[Tooltip("如果保存的GameObject中有Prefab，那需要在此添加，相同prefab的哈希值必须相同")]
		public List<RegisteredPrefab> registeredPrefabs;

		public UnityEvent OnGameSaved, OnGameLoaded;
		public static readonly HashSet<GameSaver> GameSavers = new HashSet<GameSaver>();

		/// <summary>
		/// 游戏保存的路径，可修改
		/// </summary>
		public static string SavePath {
			get => savePath ??= Path.Combine(Application.persistentDataPath, "saved");

			set {
				if (File.Exists(value)) {
					throw new IOException("Path is a file");
				}
				savePath = value;
			}
		}

		private static string savePath;
		private string saveNameAfterLoad;

		public static GameSaveMgr Instance { get; private set; }

		private void Awake() {
			if (Instance == null) {
				Instance = this;
				SceneManager.activeSceneChanged += (s1, s2) => {
					GameSaver.PrefabInstances.Clear();
				};
			}
			if (saveNameAfterLoad != null) {
				LoadGame(saveNameAfterLoad);
				Destroy(gameObject);
			}
		}

		private void OnValidate() {
			if (Instance == null) {
				Instance = this;
			}
			if (registeredPrefabs == null) {
				return;
			}
			foreach (var rp in registeredPrefabs.Where(rp => rp.prefabObject != null)) {
				if (string.IsNullOrWhiteSpace(rp.hash)) {
					rp.hash = Utils.GenerateHash(rp.prefabObject.name, registeredPrefabs.Select(rp => rp.hash));
				}
				rp.prefabObject.GetComponent<GameSaver>().registeredPrefab = rp;
			}
		}

		/// <summary>
		/// 保存游戏
		/// </summary>
		/// <param name="saveName">保存的存档名</param>
		public static void SaveGame(string saveName) {
			if (!Directory.Exists(SavePath)) {
				Directory.CreateDirectory(SavePath);
			}
			using (var bw = new BinaryWriter(new FileStream(Path.Combine(SavePath, saveName), FileMode.Create))) {
				bw.Write(SceneManager.GetActiveScene().name);

				bw.Write(GameSavers.Count);
				foreach (var gameSaver in GameSavers.Where(gameSaver => !gameSaver.saved)) {
					gameSaver.Save(bw);
				}
				foreach (var gameSaver in GameSavers) {
					gameSaver.saved = false;
				}
			}
			Instance.OnGameSaved?.Invoke();
		}

		/// <summary>
		/// 加载游戏
		/// </summary>
		/// <param name="saveName">保存的存档名</param>
		public static void LoadGame(string saveName) {
			using (var br = new BinaryReader(new FileStream(Path.Combine(SavePath, saveName), FileMode.Open))) {
				var levelName = br.ReadString();
				if (levelName != SceneManager.GetActiveScene().name) {
					var lall = new GameObject("LoadAfterLevelLoaded");
					DontDestroyOnLoad(lall);
					lall.AddComponent<GameSaveMgr>().saveNameAfterLoad = saveName;
					SceneManager.LoadScene(levelName);
					return;
				}

				foreach (var gs in GameSaver.PrefabInstances.Values.Where(gs => gs != null && gs.gameObject != null)) {
					DestroyImmediate(gs.gameObject);
				}
				GameSaver.PrefabInstances.Clear();

				var prefabInstances = new Dictionary<string, Transform>();
				var saveCount = br.ReadInt32();
				for (var i = 0; i < saveCount; i++) {
					if (!br.ReadBoolean()) {
						continue;
					}

					var parentId = br.ReadInt32();
					var parent = parentId switch {
						-1 => null,
						0 => prefabInstances[br.ReadString()],
						_ => (Transform)FindObjectFromInstanceID(parentId)
					};

					GameObject go;
					if (br.ReadBoolean()) {  // isPrefab
						var prefabHash = br.ReadString();
						var instanceHash = br.ReadString();
						if (prefabInstances.TryGetValue(instanceHash, out var tf)) {
							go = tf.gameObject;
						} else {
							var prefab = Instance.registeredPrefabs.FirstOrDefault(rp => rp.hash == prefabHash);
							if (prefab == null) {
								throw new Exception("Prefab not registered");
							}
							go = Instantiate(prefab.prefabObject, parent);
							prefabInstances.Add(instanceHash, go.transform);
							var gs = go.GetComponent<GameSaver>();
							GameSaver.PrefabInstances.Add(instanceHash, gs);
							gs.instanceHash = instanceHash;
						}
					} else {
						go = (GameObject)FindObjectFromInstanceID(br.ReadInt32());
					}

					if (br.ReadBoolean()) {
						var useLocal = br.ReadBoolean();
						if (br.ReadBoolean()) {
							var pos = br.ReadVector3();
							if (useLocal) {
								go.transform.localPosition = pos;
							} else {
								go.transform.position = pos;
							}
						}
						if (br.ReadBoolean()) {
							var rot = br.ReadQuaternion();
							if (useLocal) {
								go.transform.localRotation = rot;
							} else {
								go.transform.rotation = rot;
							}
						}
						if (br.ReadBoolean()) {
							go.transform.localScale = br.ReadVector3();
						}
					}

					if (br.ReadBoolean()) {
						var rb = go.GetComponent<Rigidbody>();
						if (rb == null) {
							rb = go.AddComponent<Rigidbody>();
						}
						if (br.ReadBoolean()) {
							rb.velocity = br.ReadVector3();
						}
						if (br.ReadBoolean()) {
							rb.angularVelocity = br.ReadVector3();
						}
					}

					if (br.ReadBoolean()) {
						var mr = go.GetComponent<MeshRenderer>();
						if (mr == null) {
							mr = go.AddComponent<MeshRenderer>();
						}
						if (br.ReadBoolean()) {
							mr.material.color = br.ReadColor();
						}
						if (br.ReadBoolean()) {
							mr.material.mainTexture = (Texture)FindObjectFromInstanceID(br.ReadInt32());
						}
						var propertyCount = br.ReadInt32();
						for (var j = 0; j < propertyCount; j++) {
							try {
								SavedMaterial.Load(mr, br);
							} catch (Exception e) {
								Debug.LogException(e);
							}
						}
					}

					if (br.ReadBoolean()) {
						var memberCount = br.ReadInt32();
						for (var j = 0; j < memberCount; j++) {
							try {
								SavedMember.Load(go, br);
							} catch (Exception e) {
								Debug.LogException(e);
							}
						}
					}
				}
			}
			Instance.OnGameLoaded?.Invoke();
		}

		private static MethodInfo method;

		public static UnityEngine.Object FindObjectFromInstanceID(int id) {
			if (method == null) {
				method = typeof(UnityEngine.Object).GetMethod("FindObjectFromInstanceID", BindingFlags.NonPublic | BindingFlags.Static);
			}
			return method == null ? null : (UnityEngine.Object)method.Invoke(null, new object[] { id });
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(GameSaveMgr))]
	public class GameSaveMgrEditor : Editor {
		private string saveName = "test";

		public override void OnInspectorGUI() {
			DrawDefaultInspector();
			GUILayout.Label("Save Name");
			saveName = GUILayout.TextField(saveName);
			if (GUILayout.Button("测试保存")) {
				GameSaveMgr.SaveGame(saveName);
			}
			if (GUILayout.Button("测试加载")) {
				GameSaveMgr.LoadGame(saveName);
			}
		}
	}
#endif
}