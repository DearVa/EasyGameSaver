using System;

namespace EasyGameSaver {
	[Serializable]
	internal class SavedMaterial {
		public int materialIndex;
		public MatPropertyType propertyType;
		public string propertyName;
	}

	internal enum MatPropertyType {
		Int,
		Float,
		Color
	}
}