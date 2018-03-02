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

	public ChunkState State;

	public List<Vector3> Vertices;
	public List<Vector3> Normals;
	public List<Vector3> LOD1Vertices;
	public List<Vector3> LOD1Normals;

	public int[] Triangles;

	public GameObject UnityObject;
}

public class ChunkQueuer {
	private ConcurrentQueue<Chunk> ChunksToMesh;
	private ConcurrentQueue<Chunk> ChunksToUpload;
	private Hashtable Chunks;
	private Transform Viewer;
	private Transform Parent;
	private GameObject ChunkPrefab;

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

		ChunksToMesh = new ConcurrentQueue<Chunk>();
		ChunksToUpload = new ConcurrentQueue<Chunk>();
		Chunks = new Hashtable();

		AddCommands();
	}

	public void Update() {
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
			int chunkSize = (int)MinimumChunkSize * scale;
			int snapSize = chunkSize * 2;

			Vector3Int minExtents = Vector3Int.zero;
			Vector3Int maxExtents = Vector3Int.zero;

			Vector3Int snappedViewerPosition = new Vector3Int((int)((pos.x)/snapSize)*snapSize, 
											(int)((pos.y)/snapSize)*snapSize, 
											(int)((pos.z)/snapSize)*snapSize);

			if(LOD == 0) {
				if(Initialized && snappedViewerPosition == PreviousPosition) {
					return;
				}
				PreviousPosition = snappedViewerPosition;
			}

			for(int x = minCoord; x < maxCoord; x++) {
				for(int y = minCoord; y < maxCoord; y++) {
					for(int z = minCoord; z < maxCoord; z++) {
						Vector3Int localCoords = new Vector3Int(x, y, z) * chunkSize - Vector3Int.one * chunkSize;
						if(x == minCoord && y == minCoord && z == minCoord) {
							minExtents = localCoords;
						}
						if(x == maxCoord && y == maxCoord && z == maxCoord) {
							maxExtents = localCoords;
						}


						if(LOD != 0 && previousLodBounds.Contains(localCoords)) {
							continue;
						}

							
						Vector3Int cpos = snappedViewerPosition + localCoords;

						Vector3 key = new Vector3(cpos.x, cpos.y, cpos.z); //getChunkHash(cpos, LOD);

						Debug.Log("Key: " + key);

						if(Chunks.ContainsKey(key) && (Chunks[key] as Chunk).LOD <= LOD) {
							//Debug.Log("skipping b/c contained key " + key);
							(Chunks[key] as Chunk).CreationTime = updateTime;
						}
						else {
							Chunk chunk = new Chunk();

							numChunksAdded++;
							chunk.Position = cpos;
							chunk.Key = key;
							chunk.LOD = LOD;
							chunk.CreationTime = updateTime;
							chunk.State = ChunkState.Blank;
							Chunks.Add(key, chunk);
							ChunksToMesh.Enqueue(chunk);
						}
					}
				}
			}

			Vector3Int size = maxExtents - minExtents;
			previousLodBounds = new BoundsInt(minExtents.x, minExtents.y, minExtents.z, size.x, size.y, size.z);

		}

		long ElapsedMilliseconds1 = sw.ElapsedMilliseconds;
		sw.Restart();


		if(numChunksAdded > 0) {
			Debug.Log("Added " + numChunksAdded + " chunks.");
		}

		Hashtable newChunkTable = new Hashtable();
		foreach(Chunk chunk in Chunks.Values) {
			if(chunk.CreationTime == updateTime) {
				newChunkTable.Add(chunk.Key, chunk);
			}
			else {
				//Debug.Log("Deleting chunk at " + chunk.Position);
				if(chunk.UnityObject != null) {
					UnityEngine.Object.Destroy(chunk.UnityObject);
				}
				chunk.State = ChunkState.Cancelled;
			}
		}
		Chunks = newChunkTable;
		long ElapsedMilliseconds2 = sw.ElapsedMilliseconds;

		string msg = "Chunk Coordinates Update: Stage 1 took " + ElapsedMilliseconds1 + "ms, Stage 2 took " + ElapsedMilliseconds2 + "ms, total is " + (ElapsedMilliseconds1 + ElapsedMilliseconds2) + "ms";

		UConsole.Print(msg);
		Debug.Log(msg);
		sw.Stop();

		Initialized = true;
		//MeshChunks();
		//UploadChunks();
	}

	public static int getChunkHash(Vector3Int position, int LOD) {
		int hashCode = position.x.GetHashCode();
		hashCode = (hashCode * 397) ^ position.y.GetHashCode();
		hashCode = (hashCode * 397) ^ position.z.GetHashCode();
		hashCode = (hashCode * 397) ^ LOD.GetHashCode();
		return hashCode;
	}

	public void MeshChunks() {
		List<Chunk> jobs = new List<Chunk>();
		
		foreach(Chunk chunk in ChunksToMesh) {
			jobs.Add(chunk);
		}

		Parallel.ForEach(jobs, (chunk) => {
			UtilFuncs.Sampler sampleFn = (float x, float y, float z) => {
				float scaleFactor = ((float)MinimumChunkSize / (float)Resolution) * Mathf.Pow(2, chunk.LOD);
				x *= scaleFactor; 
				y *= scaleFactor; 
				z *= scaleFactor;
				x += chunk.Position.x; 
				y += chunk.Position.y; 
				z += chunk.Position.z;

				return UtilFuncs.Sample(x, y, z);
			};
			SE.DC.Algorithm2.Run(Resolution, sampleFn, chunk);
			ChunksToUpload.Enqueue(chunk);
			//Debug.Log("Enqueuing chunk to upload... Queue size: " + ChunksToUpload.Count);
		});
	}

	public void UploadChunks() {
		UConsole.Print("Uploading chunks... Queue size: " + ChunksToUpload.Count);
		foreach(Chunk chunk in ChunksToUpload) {
			UploadChunk(chunk);
		}
	}

	public void UploadChunk(Chunk chunk) {
		Debug.Log("Uploading chunk...");
		chunk.State = ChunkState.Uploading;
        GameObject clone = Object.Instantiate(ChunkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        Color c = UtilFuncs.SinColor(chunk.LOD * 3f);
        clone.GetComponent<MeshRenderer>().material.color = new Color(c.r, c.g, c.b, 0.9f);
        clone.name = "Node " + chunk.Key + ", LOD " + chunk.LOD;
        
        MeshFilter mf = clone.GetComponent<MeshFilter>();
		Mesh m = new Mesh();
		m.SetVertices(chunk.Vertices);
		m.SetNormals(chunk.Normals);
		m.SetUVs(0, chunk.LOD1Vertices);
		m.SetUVs(1, chunk.LOD1Normals);
		m.triangles = chunk.Triangles;	
        mf.mesh = m;
        clone.GetComponent<Transform>().SetParent(Parent);
        clone.GetComponent<Transform>().SetPositionAndRotation(chunk.Position, Quaternion.identity);
		clone.GetComponent<Transform>().localScale = Vector3.one * ((float)MinimumChunkSize / (float)Resolution) * Mathf.Pow(2, chunk.LOD);
		chunk.State = ChunkState.Completed;
		chunk.UnityObject = clone;
	}

	public void DrawGizmos() {
		foreach(Chunk chunk in Chunks.Values) {
			Gizmos.color = UtilFuncs.SinColor(chunk.LOD * 3f);
			Gizmos.DrawSphere(chunk.Position, chunk.LOD + 0.5f);
		}
	}

	public void AddCommands() {
		UConsole.AddCommand("mesh", (string[] tokens) => {
			MeshChunks();
		});
		UConsole.AddCommand("upload", (string[] tokens) => {
			UploadChunks();
		});
	}
}
}