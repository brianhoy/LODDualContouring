using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SE;

public class DCController : MonoBehaviour {
	public GameObject ChunkPrefab;
	public GameObject Camera;

	public float WorldSize = 64f;
	public int MaxDepth = 4;
	public int Resolution = 32;

	SE.DC.Wrapper wrapper;

	// Use this for initialization
	void Start () {
		//Util.ExtractionResult res = SE.DC.Algorithm.Run(32, 0, UtilFuncs.Sample, false);

		wrapper = new SE.DC.Wrapper(this.GetComponent<Transform>(), ChunkPrefab, WorldSize, MaxDepth, Resolution);
	}
	


	// Update is called once per frame
	void Update () {
		//wrapper.Update(Camera.GetComponent<Transform>().position);
	}

	void OnDrawGizmos() {
		if(wrapper != null)
			wrapper.DrawGizmos();
	}
}
