using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EasyGameSaver {
	public static class Utils {
		private static readonly BinaryFormatter Bf = new BinaryFormatter();
		private static readonly MD5 Md5 = new MD5CryptoServiceProvider();

		public static void Write(this BinaryWriter bw, Vector2 v2) {
			bw.Write(v2.x);
			bw.Write(v2.y);
		}

		public static void Write(this BinaryWriter bw, Vector3 v3) {
			bw.Write(v3.x);
			bw.Write(v3.y);
			bw.Write(v3.z);
		}

		public static void Write(this BinaryWriter bw, Quaternion q) {
			bw.Write(q.x);
			bw.Write(q.y);
			bw.Write(q.z);
			bw.Write(q.w);
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

		public static Vector2 ReadVector2(this BinaryReader br) => new Vector2(br.ReadSingle(), br.ReadSingle());

		public static Vector3 ReadVector3(this BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

		public static Quaternion ReadQuaternion(this BinaryReader br) => new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

		public static string GenerateHash(string name, IEnumerable<string> hashes) {
			string hash;
			var i = 0;
			do {
				hash = BitConverter.ToString(Utils.Md5.ComputeHash(Encoding.UTF8.GetBytes(name + i++))).ToLower().Substring(0, 16).Replace("-", "");
			} while (hashes.Any(s => s == hash));
			return hash;
		}
	}
}