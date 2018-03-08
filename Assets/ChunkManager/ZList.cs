using System.Collections.Generic;
using UnityEngine;

public struct Crossing {
	public Vector3 Surface;
	public Vector3 Normal;
}

public class ZList {
	public List<Crossing>[] Rays;
	public int Resolution;

	public ZList() {
		Rays = new List<Crossing>[3];
		for(int i = 0; i < 3; i++) Rays[i] = new List<Crossing>();
	}


}