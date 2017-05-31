using System.Collections;
using UnityEngine;

namespace SE.DC {
	public class ChunkManager {
		Hashtable Chunks;
		public ChunkManager() {
			Chunks = new Hashtable();
		}
		public void StoreChunk(Chunk c) {
			string key = c.min.ToString();
			Chunks.Add(key, c);
		}
		public Chunk GetChunk(Vector3 min) {
			return (Chunk)Chunks[min.ToString()];
		}
	}
}
