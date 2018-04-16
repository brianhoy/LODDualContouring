using System.Collections.Generic;
using UnityEngine;

namespace Chunks {
public enum ChunkState { Blank, Meshing, Uploading, Completed, Cancelled };
public class Chunk {
	public Vector3Int Position;
	public float CreationTime;
	public int LOD; // minimum: 0
	public int LODCode;

	public Vector4Int Key;
	public int LinearArrayIndex;

	public ChunkState State;

	public List<Vector3> Vertices;
	public List<Vector3> Normals;
	public int[] Triangles;

	public GameObject UnityObject;
}
}