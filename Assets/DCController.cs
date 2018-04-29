using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SE;

public class DCController : MonoBehaviour {
	public GameObject ChunkPrefab;
	public GameObject Camera;

	public int LODs = 2;
	public int MinimumChunkSize = 16;
	public int MaxDepth = 4;
	public int Resolution = 16;
	public int Radius = 5;

	public int BenchmarkResolution = 64;
	public int BenchmarkTrials = 64;

	public float[] testData;

	Chunks.ChunkQueuer queuer;

	Chunks.Chunk testChunk;

	// Use this for initialization
	void Start () {
		//Util.ExtractionResult res = SE.DC.Algorithm.Run(32, 0, UtilFuncs.Sample, false);
		Shader.SetGlobalInt("_ChunkRadius", Radius);
		Shader.SetGlobalInt("_ChunkResolution", Resolution);
		Shader.SetGlobalInt("_ChunkMinimumSize", MinimumChunkSize);

		testData = new float[(int)Mathf.Pow(Resolution + 1, 3) * 4];
		//CreateTestChunk();

		//DualContouringTest();
		//queuer = new Chunks.ChunkQueuer(Camera.GetComponent<Transform>(), this.GetComponent<Transform>(), LODs, Resolution, Radius, MinimumChunkSize, ChunkPrefab);
	}
	


	// Update is called once per frame
	void Update () {
		if(Input.GetKeyDown(KeyCode.R)) {
			CreateTestChunk();
			//DualContouringTest();
		}
		if(Input.GetKeyDown(KeyCode.B)) {
			Benchmark();
		}
		//queuer.Update();
	}

	void Benchmark() {
		Chunks.Chunk chunk = new Chunks.Chunk();
		chunk.Position = new Vector3Int(0, 0, 0);
		chunk.LOD = 0;
		SE.MC.Algorithm.BenchmarkResult totals = new SE.MC.Algorithm.BenchmarkResult();

		for(int i = 0; i < BenchmarkTrials; i++) {
			chunk.Position.x += Resolution;
			SE.MC.Algorithm.BenchmarkResult result = SE.MC.Algorithm.PolygonizeAreaBenchmarked(BenchmarkResolution, UtilFuncs.Sample, chunk, testData);
			totals.createVerticesMs += result.createVerticesMs;
			totals.fillDataMs += result.fillDataMs;
			totals.transitionCellMs = result.transitionCellMs;
			totals.triangulateMs = result.triangulateMs;
		}

		totals.createVerticesMs /= (float)BenchmarkTrials;
		totals.fillDataMs /= (float)BenchmarkTrials;
		totals.transitionCellMs /= (float)BenchmarkTrials;
		totals.triangulateMs /= (float)BenchmarkTrials;
		
		Debug.Log("Done benchmark. " + "Average FillData ms: " + totals.fillDataMs + ", CreateVertices ms: " + totals.createVerticesMs + ", Triangulate ms: " + totals.triangulateMs + ", TransitionCell ms: " + totals.transitionCellMs + " (Total: " + (totals.transitionCellMs + totals.triangulateMs + totals.createVerticesMs + totals.fillDataMs) + "ms. )");
	}

	void CreateTestChunk() {
		if(testChunk != null) {
			Object.Destroy(testChunk.UnityObject);
		}

		Chunks.Chunk chunk = new Chunks.Chunk();
		chunk.Position = new Vector3Int(0, 0, 0);
		//chunk.LOD = 1;
		chunk.LODCode = 1;

		System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		SE.MC.Algorithm.PolygonizeArea(Resolution, UtilFuncs.Sample, chunk, testData);

		sw.Stop();
		Debug.Log("Time to mesh " + Resolution + "^3 chunk: " + sw.Elapsed.Milliseconds + " ms");

		testChunk = chunk;
		Meshify(chunk);
	}

	GameObject Meshify(Chunks.Chunk chunk) {
        GameObject clone = Object.Instantiate(ChunkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        Color c = UtilFuncs.SinColor(chunk.LOD * 3f);
        clone.GetComponent<MeshRenderer>().material.color = new Color(c.r, c.g, c.b, 0.9f);
		clone.GetComponent<MeshRenderer>().material.SetInt("_LOD", chunk.LOD);
		clone.GetComponent<MeshRenderer>().material.SetVector("_ChunkPosition", new Vector4(chunk.Position.x, chunk.Position.y, chunk.Position.z));

        clone.name = "BENCHMARK test Node " + chunk.Key + ", LOD " + chunk.LOD;
        
        MeshFilter mf = clone.GetComponent<MeshFilter>();
		mf.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		mf.mesh.SetVertices(chunk.Vertices);
		mf.mesh.SetNormals(chunk.Normals);
		//mf.mesh.SetUVs(0, chunk.LOD1Vertices);
		//mf.mesh.SetUVs(1, chunk.LOD1Normals);
		mf.mesh.triangles = chunk.Triangles;

        clone.GetComponent<Transform>().SetPositionAndRotation(chunk.Position, Quaternion.identity);
		clone.GetComponent<Transform>().localScale = Vector3.one * ((float)MinimumChunkSize / (float)Resolution) * Mathf.Pow(2, chunk.LOD);
		chunk.UnityObject = clone;
		return clone;
	}

	void OnDrawGizmos() {
		if(queuer != null) {
			queuer.DrawGizmos();
		}
		SE.MC.Algorithm.DrawGizmos();
	}
}
