using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SE;
using SE.DC;
using Util;

public class TerrainManager : MonoBehaviour {
	public GameObject MeshPrefab;
	private List<GameObject> Meshes;
	private List<Util.ExtractionResult> results;
	private ChunkManager Manager = new ChunkManager();

	// Use this for initialization
	void Start () {
		ChunkManager manager = new SE.DC.ChunkManager(); 

		Meshes = new List<GameObject>();
		results = new List<ExtractionResult>();

		int chunks = 4;
		int resolution = 16;

		Vector3 offset = new Vector3(0, 0, 0);

		for(int i = 0; i < chunks; i++) {
			Chunk chunk = new Chunk();
			UtilFuncs.Sampler sampler = (float x, float y, float z) => { return UtilFuncs.Sample(offset.x + x, offset.y + y, offset.z + z); };
			offset.x += resolution;
			ExtractionResult R = DualContouringAlgorithm.Run(resolution, 0, sampler, false);
		}

		foreach(ExtractionResult r in results) {
			CreateMesh(r);
		}
	}

	void CreateMesh(Util.ExtractionResult r) {
		GameObject isosurfaceMesh = Instantiate(MeshPrefab, r.offset, Quaternion.identity);
		Meshes.Add(isosurfaceMesh);

		Material mat = isosurfaceMesh.GetComponent<Renderer>().materials[0];
		MeshFilter mf = isosurfaceMesh.GetComponent<MeshFilter>();
		MeshCollider mc = isosurfaceMesh.GetComponent<MeshCollider>();

		mf.mesh = r.mesh;
		mc.sharedMesh = mf.mesh;
		//if(m.normals != null) mf.mesh.normals = m.normals;
		mf.mesh.RecalculateNormals();
		mf.mesh.RecalculateBounds();
	}

	// Update is called once per frame
	void Update () {
		
	}
}
