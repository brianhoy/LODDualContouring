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

	// Use this for initialization
	void Start () {
		//Util.ExtractionResult res = SE.DC.Algorithm.Run(32, 0, UtilFuncs.Sample, false);

		queuer = new Chunks.ChunkQueuer(Camera.GetComponent<Transform>(), this.GetComponent<Transform>(), LODs, Resolution, Radius, MinimumChunkSize, ChunkPrefab);
	}
	


	// Update is called once per frame
	void Update () {
		if(Input.GetKeyDown(KeyCode.U)) {
			
		}
		queuer.Update();
		//wrapper.Update(Camera.GetComponent<Transform>().position);
	}

	void OnDrawGizmos() {
		if(queuer != null) {
			queuer.DrawGizmos();
		}
	}
}
