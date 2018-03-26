using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Chunks {
public enum ChunkState { Blank, Meshing, Uploading, Completed, Cancelled };
public class Chunk {
	public Vector3Int Position;
	public float CreationTime;
	public int LOD; // minimum: 0
	public Vector4 Key;
	public int LinearArrayIndex;

	public ChunkState State;

	public List<Vector3> Vertices;
	public List<Vector3> Normals;
	public List<Vector3> LOD1Vertices;
	public List<Vector3> LOD1Normals;

	public int[] Triangles;

	public GameObject UnityObject;
}

public class ChunkQueuer {
	private ConcurrentBag<Chunk> ChunksToMesh;
	private ConcurrentBag<Chunk> ChunksToUpload;
	private ConcurrentBag<GameObject> ObjectsToClear;

	private Hashtable Chunks;

	private Chunk[][,,] ChunkStorage;
	private Chunk[] ChunkLinearArray;
	private int ChunkLinearArrayMin;
	private int ChunkLinearArrayMax;
	private int ChunkArraySize;

	private Transform Viewer;
	private Transform Parent;
	private GameObject ChunkPrefab;
	private CObjectPool<GameObject> UnityObjectPool;
	private bool Busy;

	private int LODs;
	private int Resolution;
	private int Radius;
	private int MinimumChunkSize;
	private bool Initialized;
	private Vector3Int PreviousPosition;

	private Bounds[] LODBounds;

	public ChunkQueuer(Transform Viewer, Transform Parent, int LODs, int Resolution, int Radius, int MinimumChunkSize, GameObject ChunkPrefab) {
		Debug.Assert(LODs >= 1);
		this.Viewer = Viewer;
		this.Parent = Parent;
		this.LODs = LODs;
		this.Resolution = Resolution;
		this.MinimumChunkSize = MinimumChunkSize;
		this.Radius = Radius;
		this.Initialized = false;
		this.ChunkPrefab = ChunkPrefab;

		LODBounds = new Bounds[4];
		ChunkArraySize = 4 * Radius;
		ChunkStorage = new Chunk[LODs][,,];
		ChunkLinearArrayMin = 0;
		ChunkLinearArrayMax = 0;

		for(int i = 0; i < LODs; i++) {
			ChunkStorage[i] = new Chunk[ChunkArraySize,ChunkArraySize,ChunkArraySize];
		}

		UnityObjectPool = new CObjectPool<GameObject>(() => {
			
			var obj = Object.Instantiate(ChunkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
			obj.GetComponent<MeshFilter>().mesh = new Mesh();
			return obj;
		});
		ChunksToMesh = new ConcurrentBag<Chunk>();
		ChunksToUpload = new ConcurrentBag<Chunk>();
		ObjectsToClear = new ConcurrentBag<GameObject>();
		Chunks = new Hashtable();
		ChunkLinearArray = new Chunk[ChunkArraySize*ChunkArraySize*ChunkArraySize * 50];
		AddCommands();
	}

	public void Update() {
		//ClearObjects();
		//UploadChunks();
		if(Busy) {
			return;
		}

		DoChunkUpdate();

		//MeshChunks();
	}

	public void PrintArraySlice() {
		string msg = "";
		for(int y = 0; y < ChunkArraySize; y++) {
			msg += "[";
			for(int x = 0; x < ChunkArraySize; x++) {
				if(ChunkStorage[0][x,y,1] != null) {
					msg += "c ";
				}
				else {
					msg += "0 ";
				}
			}
			msg += "]\n";
		}
		msg += "";
		Debug.Log(msg);
	}

	public void DoChunkUpdate() {
		System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		Vector3 pos = Viewer.position;
		float updateTime = Time.realtimeSinceStartup;

		int numChunksAdded = 0;
		int minCoord = -Radius + 1;
		int maxCoord = Radius + 1;
		BoundsInt previousLodBounds = new BoundsInt();

		for(int LOD = 0; LOD < LODs; LOD++) {
			int scale = (int)Mathf.Pow(2, LOD);
			int chunkSize = MinimumChunkSize * scale;
			int snapSize = chunkSize * 2;

			Vector3Int minExtents = Vector3Int.zero;
			Vector3Int maxExtents = Vector3Int.zero;

			Vector3Int snappedViewerPosition = new Vector3Int(
									(int)Mathf.Floor(pos.x/snapSize), 
									(int)Mathf.Floor(pos.y/snapSize), 
									(int)Mathf.Floor(pos.z/snapSize)) * snapSize;

			Vector3Int posOffset = new Vector3Int(
									(int)Mathf.Floor(pos.x/(float)chunkSize), 
									(int)Mathf.Floor(pos.y/(float)chunkSize), 
									(int)Mathf.Floor(pos.z/(float)chunkSize));

			if(LOD == 0) {
				if(Initialized && snappedViewerPosition == PreviousPosition) {
					Busy = false;
					return;
				}
				PreviousPosition = snappedViewerPosition;
			}

			Vector3Int vChunkSize = Vector3Int.one * chunkSize;

			for(int x = minCoord; x < maxCoord; x++) {
				for(int y = minCoord; y < maxCoord; y++) {
					for(int z = minCoord; z < maxCoord; z++) {
						Vector3Int itPos = new Vector3Int(x, y, z);
						Vector3Int localCoords = itPos * chunkSize - vChunkSize;
						Vector3Int cpos = snappedViewerPosition + localCoords;

						if(LOD != 0 && previousLodBounds.Contains(cpos)) {
							continue;
						}
						if(x == minCoord && y == minCoord && z == minCoord) {
							minExtents = cpos;
						}
						if(x == maxCoord - 1 && y == maxCoord - 1 && z == maxCoord - 1) {
							maxExtents = cpos;
						}

						int arrx = mod(cpos.x/chunkSize, ChunkArraySize);
						int arry = mod(cpos.y/chunkSize, ChunkArraySize);
						int arrz = mod(cpos.z/chunkSize, ChunkArraySize);

						Vector4 key = new Vector4(arrx, arry, arrz, LOD);

						if(ChunkStorage[LOD][arrx,arry,arrz] == null) {
							Debug.Assert(!Chunks.ContainsKey(key));

							Chunk chunk = new Chunk();

							numChunksAdded++;
							chunk.Position = cpos;
							chunk.Key = key;
							chunk.LOD = LOD;
							chunk.CreationTime = updateTime;
							chunk.State = ChunkState.Blank;
							chunk.LinearArrayIndex = ChunkLinearArrayMax++;
							ChunkStorage[LOD][arrx,arry,arrz] = chunk;
							ChunkLinearArray[chunk.LinearArrayIndex] = chunk;
							ChunksToMesh.Add(chunk);
						}
						else {
							ChunkStorage[LOD][arrx,arry,arrz].CreationTime = updateTime;
						}
					}
				}
			}

			Vector3Int size = maxExtents - minExtents;
			previousLodBounds = new BoundsInt(minExtents, size);
		}

		long ElapsedMilliseconds1 = sw.ElapsedMilliseconds;
		sw.Restart();
		//PrintArraySlice();
		Debug.Log("Added " + numChunksAdded + " chunks.");

		for(int i = ChunkLinearArrayMin; i < ChunkLinearArrayMax; i++) {
			Chunk c = ChunkLinearArray[i];
			if(c != null) {
				if(c.CreationTime != updateTime) {
					ChunkLinearArray[i] = null;
					ChunkStorage[(int)c.Key.w][(int)c.Key.x,(int)c.Key.y,(int)c.Key.z] = null;
					if(c.UnityObject != null) {
						UnityObjectPool.PutObject(ref c.UnityObject);
						c.UnityObject.GetComponent<MeshFilter>().mesh.Clear();
					}
					c.State = ChunkState.Cancelled;
				}
			}
		}

		for(int i = ChunkLinearArrayMin; i < ChunkLinearArrayMax; i++) {
			if(ChunkLinearArray[i] != null) {
				ChunkLinearArrayMin = i;
				break;
			}
		}

		long ElapsedMilliseconds2 = sw.ElapsedMilliseconds;
		string msg = "Chunk Coordinates Update: Stage 1 took " + ElapsedMilliseconds1 + "ms, Stage 2 took " + ElapsedMilliseconds2 + "ms, total is " + (ElapsedMilliseconds1 + ElapsedMilliseconds2) + "ms";

		//UConsole.Print(msg);
		Debug.Log(msg);
		sw.Stop();
		Initialized = true;

	}
	int mod(int x, int m) {
		return (x%m + m)%m;
	}

	public static int getChunkHash(Vector3Int position, int LOD) {
		int hash = position.x;
		hash *= 1000003;
		hash += position.y;
		hash *= 1000003;
		hash += position.z;
		hash *= 1000003;
		hash += LOD;
		return hash;
	}

	public void ClearObjects() {
		while(ObjectsToClear.Count > 0) {
			GameObject obj;
			if(ObjectsToClear.TryTake(out obj)) {
				UnityObjectPool.PutObject(ref obj);
				obj.GetComponent<MeshFilter>().mesh.Clear();
			}
		}
	}

	public void MeshChunks() {
		if(ChunksToMesh.Count == 0) {
			return;
		}
		Task.Factory.StartNew(() => {
			Busy = true;
			Parallel.ForEach(ChunksToMesh, (chunk) => {
				UtilFuncs.Sampler sampleFn = (float x, float y, float z) => {
					float scaleFactor = ((float)MinimumChunkSize / (float)Resolution) * Mathf.Pow(2, chunk.LOD);
					x *= scaleFactor; 
					y *= scaleFactor; 
					z *= scaleFactor;
					x += chunk.Position.x; 
					y += chunk.Position.y; 
					z += chunk.Position.z;

					return UtilFuncs.Sample(x/Resolution, y/Resolution, z/Resolution);
				};
				SE.DC.Algorithm2.Run(Resolution, sampleFn, chunk);
				if(chunk.Triangles.Length > 0) {
					ChunksToUpload.Add(chunk);
				}
				//Debug.Log("Enqueuing chunk to upload... Queue size: " + ChunksToUpload.Count);
			});
			ChunksToMesh = new ConcurrentBag<Chunk>();
			Busy = false;
		});
	}

	public void UploadChunks() {
		int ct = ChunksToUpload.Count;
		int amtuploading = 10;
		if(ct < 10) {
			amtuploading = ct;
		}
		while(amtuploading > 0) {
			Chunk chunk;
			if(ChunksToUpload.TryTake(out chunk)) {
				//UConsole.Print("Uploading chunk. ");
				UploadChunk(chunk);
			}
			else {
				break;
			}
			amtuploading--;
		}
	}

	public void UploadChunk(Chunk chunk) {
		//System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
		//sw.Start();

		if(chunk.Triangles.Length == 0) {
			return;
		}

		//Debug.Log("Uploading chunk...");
		chunk.State = ChunkState.Uploading;
		GameObject clone = UnityObjectPool.GetObject();
		//Color c = UtilFuncs.SinColor(chunk.LOD * 3f);
		//clone.GetComponent<MeshRenderer>().material.color = new Color(c.r, c.g, c.b, 0.9f);
		clone.GetComponent<MeshRenderer>().material.SetInt("_LOD", chunk.LOD);
		clone.GetComponent<MeshRenderer>().material.SetVector("_ChunkPosition", new Vector4(chunk.Position.x, chunk.Position.y, chunk.Position.z));

		//clone.name = "Node " + chunk.Key + ", LOD " + chunk.LOD;

		MeshFilter mf = clone.GetComponent<MeshFilter>();
		mf.mesh.SetVertices(chunk.Vertices);
		mf.mesh.SetNormals(chunk.Normals);
		mf.mesh.SetUVs(0, chunk.LOD1Vertices);
		mf.mesh.SetUVs(1, chunk.LOD1Normals);
		mf.mesh.triangles = chunk.Triangles;

		clone.GetComponent<Transform>().SetParent(Parent);
		clone.GetComponent<Transform>().SetPositionAndRotation(chunk.Position, Quaternion.identity);
		clone.GetComponent<Transform>().localScale = Vector3.one * ((float)MinimumChunkSize / (float)Resolution) * Mathf.Pow(2, chunk.LOD);
		chunk.State = ChunkState.Completed;
		chunk.UnityObject = clone;

		//sw.Stop();
		//Debug.Log("Uploading mesh took " + sw.ElapsedMilliseconds + "ms");
	}

	public void DrawGizmos() {
		for(int i = ChunkLinearArrayMin; i < ChunkLinearArrayMax; i++) {
			if(ChunkLinearArray[i] != null) {
				Chunk chunk = ChunkLinearArray[i];
				Gizmos.color = UtilFuncs.SinColor(chunk.LOD * 3f);
				Gizmos.DrawSphere(chunk.Position, chunk.LOD + 0.5f);
			}
		}
	}

	public void AddCommands() {
		UConsole.AddCommand("mesh", (string[] tokens) => {
			MeshChunks();
		});
		UConsole.AddCommand("upload", (string[] tokens) => {
			UploadChunks();
		});
		UConsole.AddCommand("mu", (string[] tokens) => {
			MeshChunks();
			UploadChunks();
		});


	}
}
}