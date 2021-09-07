using System;
using System.IO;
using UnityEngine;

namespace EasyGameSaver {
	[Serializable]
	internal class SavedMaterial {
		public int materialIndex;
		public MatPropertyType propertyType;
		public string propertyName;

		internal void Save(MeshRenderer mr, BinaryWriter bw) {
			bw.Write(materialIndex);
			bw.Write((int)propertyType);
			bw.Write(propertyName);
			var mat = mr.materials[materialIndex];
			switch (propertyType) {
			case MatPropertyType.Int:
				bw.Write(mat.GetInt(propertyName));
				break;
			case MatPropertyType.Float:
				bw.Write(mat.GetFloat(propertyName));
				break;
			case MatPropertyType.FloatArray:
				bw.WriteArray(mat.GetFloatArray(propertyName));
				break;
			case MatPropertyType.Color:
				bw.Write(mat.GetColor(propertyName));
				break;
			case MatPropertyType.ColorArray:
				bw.WriteArray(mat.GetColorArray(propertyName));
				break;
			case MatPropertyType.Matrix:
				bw.Write(mat.GetMatrix(propertyName));
				break;
			case MatPropertyType.MatrixArray:
				bw.WriteArray(mat.GetMatrixArray(propertyName));
				break;
			case MatPropertyType.Vector:
				bw.Write(mat.GetVector(propertyName));
				break;
			case MatPropertyType.VectorArray:
				bw.WriteArray(mat.GetVectorArray(propertyName));
				break;
			}
		}

		internal static void Load(MeshRenderer mr, BinaryReader br) {
			var materialIndex = br.ReadInt32();
			var propertyType = (MatPropertyType)br.ReadInt32();
			var propertyName = br.ReadString();
			var mat = mr.materials[materialIndex];
			switch (propertyType) {
			case MatPropertyType.Int:
				mat.SetInt(propertyName, br.ReadInt32());
				break;
			case MatPropertyType.Float:
				mat.SetFloat(propertyName, br.ReadSingle());
				break;
			case MatPropertyType.FloatArray:
				mat.SetFloatArray(propertyName, br.ReadFloatArray());
				break;
			case MatPropertyType.Color:
				mat.SetColor(propertyName, br.ReadColor());
				break;
			case MatPropertyType.ColorArray:
				mat.SetColorArray(propertyName, br.ReadColorArray());
				break;
			case MatPropertyType.Matrix:
				mat.SetMatrix(propertyName, br.ReadMatrix4x4());
				break;
			case MatPropertyType.MatrixArray:
				mat.SetMatrixArray(propertyName, br.ReadMatrix4x4Array());
				break;
			case MatPropertyType.Vector:
				mat.SetVector(propertyName, br.ReadVector4());
				break;
			case MatPropertyType.VectorArray:
				mat.SetVectorArray(propertyName, br.ReadVector4Array());
				break;
			}
		}
	}

	internal enum MatPropertyType {
		Int,
		Float,
		FloatArray,
		Color,
		ColorArray,
		Matrix,
		MatrixArray,
		Vector,
		VectorArray,
	}
}