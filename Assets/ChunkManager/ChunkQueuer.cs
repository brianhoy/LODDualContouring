using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Chunks {
public enum ChunkState { Blank, Meshing, Uploading, Completed, Cancelled };
public class Chunk {
	public Vector3 Position;
	public float CreationTime;
	public int LOD; // minimum: 0
	public string Key;

	public ChunkState State;

	public Vector3[] Vertices;
	public Vector3[] Normals;
	public int[] Triangles;

	public GameObject UnityObject;
}

public class ChunkQueuer {
	private ConcurrentQueue<Chunk> ChunksToMesh;
	private ConcurrentQueue<Chunk> ChunksToUpload;
	private ConcurrentDictionary<string, Chunk> Chunks;
	private Transform Viewer;
	private Transform Parent;
	private GameObject ChunkPrefab;
	private List<Vector4> DebugPoints;

	private int LODs;
	private int Resolution;
	private int Radius;
	private float MinimumChunkSize;

	private Bounds[] LODBounds;

	public ChunkQueuer(Transform Viewer, Transform Parent, int LODs, int Resolution, int Radius, float MinimumChunkSize, GameObject ChunkPrefab) {
		Debug.Assert(LODs >= 1);
		this.Viewer = Viewer;
		this.Parent = Parent;
		this.LODs = LODs;
		this.Resolution = Resolution;
		this.MinimumChunkSize = MinimumChunkSize;
		this.Radius = Radius;

		LODBounds = new Bounds[4];

		ChunksToMesh = new ConcurrentQueue<Chunk>();
		ChunksToUpload = new ConcurrentQueue<Chunk>();
		Chunks = new ConcurrentDictionary<string, Chunk>();
		DebugPoints = new List<Vector4>();
	}

	public void Update() {
		Vector3 pos = Viewer.position;

		float updateTime = Time.realtimeSinceStartup;

		float color = 0f; //Random.Range(0f, 100f);

		float mc2 = MinimumChunkSize * 2;

		Debug.Log("Updating. #LODs: " + LODs);

		Hashtable chunkLocations = new Hashtable();
		for(int LOD = 1; LOD <= LODs; LOD++) {
			for(int x = -Radius; x < Radius; x += 1) {
				for(int y = -Radius; y < Radius; y += 1) {
					for(int z = -Radius; z < Radius; z += 1) {
						int scale = (int)Mathf.Pow(2, LOD);
						int tfAmount = 0;
						if(LOD != 1) {
							//tfAmount = (int)Mathf.Pow(2, LOD - 2) * (int)MinimumChunkSize;
						}
						if(LOD == 2) {
							tfAmount = (int)MinimumChunkSize * 2;
						}
						if(LOD == 3) {
							tfAmount = -(int)MinimumChunkSize * 2;
						}
						if(LOD == 4) {
							tfAmount = (int)MinimumChunkSize * 6;
						}

						Vector3 localCoords = (MinimumChunkSize * scale * new Vector3(x, y, z)) - (Vector3.one * tfAmount);

						if(chunkLocations.ContainsKey(localCoords)) {
							continue;
						}
						chunkLocations.Add(localCoords, localCoords);

						Vector3 cpos = new Vector3((int)((pos.x)/mc2)*mc2, (int)((pos.y)/mc2)*mc2, (int)((pos.z)/mc2)*mc2) + localCoords;

						string key = "Chunk_"+cpos.x+","+cpos.y+","+cpos.z+"_"+LOD;

						if(Chunks.ContainsKey(key)) {
							//Debug.Log("skipping b/c contained key " + key);
							(Chunks[key] as Chunk).CreationTime = updateTime;
						}
						else {
							//Debug.Log("Adding chunk at cpos " + cpos);
							Vector4 dbPt = new Vector4(cpos.x, cpos.y, cpos.z, color);
							DebugPoints.Add(dbPt);
							Chunk chunk = new Chunk();
							chunk.Position = cpos;
							chunk.Key = key;
							chunk.LOD = 0;
							chunk.CreationTime = updateTime;
							chunk.State = ChunkState.Blank;
							Debug.Assert(Chunks.TryAdd(key, chunk));
							ChunksToMesh.Enqueue(chunk);
						}
					}
				}
			}

		}

		foreach(Chunk chunk in Chunks.Values) {
			if(chunk.CreationTime != updateTime) {
				Debug.Log("Deleting chunk at " + chunk.Position);
				if(chunk.UnityObject != null) {
					UnityEngine.Object.Destroy(chunk.UnityObject);
				}
				chunk.State = ChunkState.Cancelled;
				Chunk removedChunk;
				DebugPoints.Remove(new Vector4(chunk.Position.x, chunk.Position.y, chunk.Position.z, color));
				Debug.Assert(Chunks.TryRemove(chunk.Key, out removedChunk));
			}
		}

		//MeshChunks();
		//UploadChunks();
	}

	public void MeshChunks() {
		List<Chunk> jobs = new List<Chunk>();
		
		while(ChunksToMesh.Count > 0) {
			Chunk chunk;
			if(ChunksToMesh.TryDequeue(out chunk)) {
				jobs.Add(chunk);
			}
		}

		Parallel.ForEach(jobs, (currentJob) => {
			SE.DC.Algorithm2.Run(Resolution, UtilFuncs.Sample, 1, currentJob, ChunksToUpload);
		});
	}

	public void UploadChunks() {
		while(ChunksToUpload.Count > 0) {
			Chunk chunk;
			if(ChunksToMesh.TryDequeue(out chunk)) {
				UploadChunk(chunk);
			}
		}
	}

	public void UploadChunk(Chunk chunk) {
		chunk.State = ChunkState.Uploading;
        GameObject clone = Object.Instantiate(ChunkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        Color c = UtilFuncs.SinColor(chunk.LOD * 3f);
        clone.GetComponent<MeshRenderer>().material.color = new Color(c.r, c.g, c.b, 0.9f);
        clone.name = "Node " + chunk.Key + ", LOD " + chunk.LOD;
        
        MeshFilter mf = clone.GetComponent<MeshFilter>();
		Mesh m = new Mesh();
		m.vertices = chunk.Vertices;
		m.triangles = chunk.Triangles;
		m.normals = chunk.Normals;
        mf.mesh = m;
        clone.GetComponent<Transform>().SetParent(Parent);
        clone.GetComponent<Transform>().SetPositionAndRotation(chunk.Position, Quaternion.identity);
		chunk.State = ChunkState.Completed;
		chunk.UnityObject = clone;
	}

	public void DrawGizmos() {
		foreach(Vector4 point in DebugPoints) {
			Gizmos.color = UtilFuncs.SinColor(point.w);
			Gizmos.DrawSphere(point, 5f);
		}
	}
}
}