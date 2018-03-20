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

	Chunks.ChunkQueuer queuer;

	Chunks.Chunk testChunk;

	// Use this for initialization
	void Start () {
		//Util.ExtractionResult res = SE.DC.Algorithm.Run(32, 0, UtilFuncs.Sample, false);
		Shader.SetGlobalInt("_ChunkRadius", Radius);
		Shader.SetGlobalInt("_ChunkResolution", Resolution);
		Shader.SetGlobalInt("_ChunkMinimumSize", MinimumChunkSize);

		DualContouringTest();
		//queuer = new Chunks.ChunkQueuer(Camera.GetComponent<Transform>(), this.GetComponent<Transform>(), LODs, Resolution, Radius, MinimumChunkSize, ChunkPrefab);
	}
	


	// Update is called once per frame
	void Update () {
		if(Input.GetKeyDown(KeyCode.U)) {
			DualContouringTest();
		}
		//queuer.Update();
		Vector3 pos = Camera.GetComponent<Transform>().position;
		Shader.SetGlobalVector("_ViewerPosition", new Vector4(pos.x, pos.y, pos.z, 0));
		//wrapper.Update(Camera.GetComponent<Transform>().position);
	}

	void DualContouringTest() {
		Debug.Log("Dual contouring test running...");
		if(testChunk != null) {
			UnityEngine.Object.Destroy(testChunk.UnityObject);
		}

		Chunks.Chunk chunk = new Chunks.Chunk();
		testChunk = chunk;
		SE.Z.ZList zList = new SE.Z.ZList(Resolution+1);
		zList.Fill(UtilFuncs.FlatGround);

		SE.Z.ZList zListB = new SE.Z.ZList(Resolution + 1);
		zListB.Fill(UtilFuncs.Sphere);

		zList.AddZList(zListB);

		SE.DC.Algorithm2.Run(zList, chunk);

		if(chunk.Triangles.Length == 0) {
			return;
		}

		//Debug.Log("Uploading chunk...");
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
		mf.mesh.SetUVs(0, chunk.LOD1Vertices);
		mf.mesh.SetUVs(1, chunk.LOD1Normals);
		mf.mesh.triangles = chunk.Triangles;

        clone.GetComponent<Transform>().SetPositionAndRotation(chunk.Position, Quaternion.identity);
		clone.GetComponent<Transform>().localScale = Vector3.one * ((float)MinimumChunkSize / (float)Resolution) * Mathf.Pow(2, chunk.LOD);
		chunk.UnityObject = clone;
	}


	void OnDrawGizmos() {
		if(queuer != null) {
			queuer.DrawGizmos();
		}
	}
}
