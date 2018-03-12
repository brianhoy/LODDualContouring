using System.Collections.Generic;
using UnityEngine;

namespace SE.Z {
public struct Crossing {
	public float Z;
	public Vector3 Normal;
	public Crossing(float Z, Vector3 Normal) {
		this.Z = Z;
		this.Normal = Normal;
	}
}

public class Ray {
	public List<Crossing> Crossings;
}

public class ZList {
	public Ray[][,] Rays;
	public sbyte[,,] Samples;
	public int Resolution;
	public static Vector3Int[] Directions = { new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, 0, 1) };

	public ZList(int Resolution) {
		Rays = new Ray[3][,];
		this.Resolution = Resolution;
		for(int dir = 0; dir < 3; dir++) {
			Rays[dir] = new Ray[Resolution, Resolution];
			for(int x = 0; x < Resolution; x++) {
				for(int y = 0; y < Resolution; y++) {
					Rays[dir][x,y]  = new Ray();
					Rays[dir][x,y].Crossings = new List<Crossing>();
				}
			}
		}
	}

	public void Fill(UtilFuncs.Sampler fn) {
		float[,,] data = new float[Resolution,Resolution,Resolution];
		Samples = new sbyte[Resolution,Resolution,Resolution];
		for(int x = 0; x < Resolution; x++) {
			for(int y = 0; y < Resolution; y++) {
				for(int z = 0; z < Resolution; z++) {
					float sample = fn(x, y, z);
					data[x,y,z] = sample;
					Samples[x,y,z] = (sbyte)(sample * 127);
				}
			}
		}

		// xy plane
		for(int x = 0; x < Resolution; x++) {
			for(int y = 0; y < Resolution; y++) {
				float lastSample = data[x,y,0];

				for(int z = 1; z < Resolution; z++) {
					float nextSample = data[x,y,1];
					if(lastSample > 0 && nextSample < 0 || lastSample < 0 && nextSample > 0) {
						Vector3 position = ApproximateZeroCrossingPosition(new Vector3(x, y, z - 1), new Vector3(x, y, z), fn);
						Vector3 normal = CalculateSurfaceNormal(position, fn);
						Crossing c = new Crossing(position.z, normal);
						Rays[0][x,y].Crossings.Add(c);
					}
					lastSample = nextSample;
				}
			}
		}

		// yz plane
		for(int y = 0; y < Resolution; y++) {
			for(int z = 0; z < Resolution; z++) {
				float lastSample = data[y,z,0];

				for(int x = 1; x < Resolution; x++) {
					float nextSample = data[y,z,1];
					if(lastSample > 0 && nextSample < 0 || lastSample < 0 && nextSample > 0) {
						Vector3 position = ApproximateZeroCrossingPosition(new Vector3(x - 1, y, z), new Vector3(x, y, z), fn);
						Vector3 normal = CalculateSurfaceNormal(position, fn);
						Crossing c = new Crossing(position.x, normal);
						Rays[1][y,z].Crossings.Add(c);
					}
					lastSample = nextSample;
				}
			}
		}

		// xz plane
		for(int x = 0; x < Resolution; x++) {
			for(int z = 0; z < Resolution; z++) {
				float lastSample = data[x,z,0];

				for(int y = 1; y < Resolution; y++) {
					float nextSample = data[x,y,1];
					if(lastSample > 0 && nextSample < 0 || lastSample < 0 && nextSample > 0) {
						Vector3 position = ApproximateZeroCrossingPosition(new Vector3(x, y - 1, z), new Vector3(x, y, z), fn);
						Vector3 normal = CalculateSurfaceNormal(position, fn);
						Crossing c = new Crossing(position.z, normal);
						Rays[2][x,y].Crossings.Add(c);
					}
					lastSample = nextSample;
				}
			}
		}

	}

	public Crossing[,,][] Voxelize() {
		int res1 = Resolution + 1;
		Crossing[,,][] voxels = new Crossing[res1,res1,res1][];
		for(int x = 0; x < res1; x++) {
			for(int y = 0; y < res1; y++) {
				for(int z = 0; z < res1; z++) {
					voxels[x,y,z] = new Crossing[3];
				}
			}
		}

		for(int x = 0; x < Resolution; x++) { // xy rays
			for(int y = 0; y < Resolution; y++) {
				foreach(Crossing c in Rays[0][x,y].Crossings) {
					int z = (int)c.Z;
					voxels[x,y,z][0] = c;
				}
			}
		}
		for(int y = 0; y < Resolution; y++) { // yz rays
			for(int z = 0; z < Resolution; z++) {
				foreach(Crossing c in Rays[0][y,z].Crossings) {
					int x = (int)c.Z;
					voxels[x,y,z][0] = c;
				}
			}
		}
		for(int x = 0; x < Resolution; x++) { // xz rays
			for(int z = 0; z < Resolution; z++) {
				foreach(Crossing c in Rays[0][x,z].Crossings) {
					int y = (int)c.Z;
					voxels[x,y,z][0] = c;
				}
			}
		}


		return voxels;
	}

	public static Vector3 ApproximateZeroCrossingPosition(Vector3 p0, Vector3 p1, UtilFuncs.Sampler sample)
	{
		// approximate the zero crossing by finding the min value along the edge
		float minValue = 100000;
		float t = 0;
		float currentT = 0;
		const int steps = 8;
		const float increment = 1f / (float)steps;
		while (currentT <= 1f)
		{
			Vector3 p = p0 + ((p1 - p0) * currentT);
			float density = Mathf.Abs(sample(p.x, p.y, p.z));
			if (density < minValue)
			{
				minValue = density;
				t = currentT;
			}

			currentT += increment;
		}

		return p0 + ((p1 - p0) * t);
	}

	public static Vector3 CalculateSurfaceNormal(Vector3 p, UtilFuncs.Sampler sample)
	{
		const float H = 0.001f;
		float dx = sample(p.x + H, p.y, p.z) - sample(p.x - H, p.y, p.z);
		float dy = sample(p.x, p.y + H, p.z) - sample(p.x, p.y - H, p.z);
		float dz = sample(p.x, p.y, p.z + H) - sample(p.x, p.y, p.z - H);

		return new Vector3(dx, dy, dz).normalized;
	}



}
}
