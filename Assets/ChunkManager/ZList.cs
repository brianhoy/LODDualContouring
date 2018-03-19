using System.Collections.Generic;
using UnityEngine;

namespace SE.Z {
public struct Crossing {
	public float Z;
	public Vector3 Normal;
	public byte Index;
	public Crossing(float Z, Vector3 Normal, byte Index) {
		this.Z = Z;
		this.Normal = Normal;
		this.Index = Index;
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

	public ZList(int resolution) {
		Rays = new Ray[3][,];
		this.Resolution = resolution;
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
		Samples = new sbyte[Resolution,Resolution,Resolution];
		for(int x = 0; x < Resolution; x++) {
			for(int y = 0; y < Resolution; y++) {
				for(int z = 0; z < Resolution; z++) {
					float sample = fn(x, y, z);
					sample = Mathf.Clamp(sample, -1, 1);
					Samples[x,y,z] = (sbyte)(sample * 127);
				}
			}
		}

		// yz plane
		for(int y = 0; y < Resolution; y++) {
			for(int z = 0; z < Resolution; z++) {
				sbyte lastSample = Samples[0,y,z];

				for(int x = 1; x < Resolution; x++) {
					sbyte nextSample = Samples[x,y,z];
					if(lastSample > 0 && nextSample <= 0 || lastSample <= 0 && nextSample > 0) {
						Vector3 position = ApproximateZeroCrossingPosition(new Vector3(x - 1, y, z), new Vector3(x, y, z), fn);
						Vector3 normal = CalculateSurfaceNormal(position, fn);
						Crossing c = new Crossing(position.x, normal, (byte)(x - 1));
						Rays[0][y,z].Crossings.Add(c);
					}
					lastSample = nextSample;
				}
			}
		}

		// xz plane
		for(int x = 0; x < Resolution; x++) {
			for(int z = 0; z < Resolution; z++) {
				sbyte lastSample = Samples[x,0,z];

				for(int y = 1; y < Resolution; y++) {
					sbyte nextSample = Samples[x,y,z];
					if(lastSample > 0 && nextSample <= 0 || lastSample <= 0 && nextSample > 0) {
						Vector3 position = ApproximateZeroCrossingPosition(new Vector3(x, y - 1, z), new Vector3(x, y, z), fn);
						Vector3 normal = CalculateSurfaceNormal(position, fn);
						Crossing c = new Crossing(position.y, normal, (byte)(y - 1));
						Rays[1][x,z].Crossings.Add(c);
					}
					lastSample = nextSample;
				}
			}
		}

		// xy plane
		for(int x = 0; x < Resolution; x++) {
			for(int y = 0; y < Resolution; y++) {
				sbyte lastSample = Samples[x,y,0];

				for(int z = 1; z < Resolution; z++) {
					sbyte nextSample = Samples[x,y,z];
					if(lastSample > 0 && nextSample <= 0 || lastSample <= 0 && nextSample > 0) {
						Vector3 position = ApproximateZeroCrossingPosition(new Vector3(x, y, z - 1), new Vector3(x, y, z), fn);
						Vector3 normal = CalculateSurfaceNormal(position, fn);
						Crossing c = new Crossing(position.z, normal, (byte)(z - 1));
						Rays[2][x,y].Crossings.Add(c);
					}
					lastSample = nextSample;
				}
			}
		}
	}

	public Crossing[,,][] Voxelize() {
		Crossing[,,][] voxels = new Crossing[Resolution,Resolution,Resolution][];
		for(int x = 0; x < Resolution; x++) {
			for(int y = 0; y < Resolution; y++) {
				for(int z = 0; z < Resolution; z++) {
					voxels[x,y,z] = new Crossing[3];
				}
			}
		}

		for(int y = 0; y < Resolution; y++) { // yz rays
			for(int z = 0; z < Resolution; z++) {
				foreach(Crossing c in Rays[2][y,z].Crossings) {
					int x = (int)c.Z;
					voxels[x,y,z][0] = c;
				}
			}
		}
		for(int x = 0; x < Resolution; x++) { // xz rays
			for(int z = 0; z < Resolution; z++) {
				foreach(Crossing c in Rays[1][x,z].Crossings) {
					int y = (int)c.Z;
					voxels[x,y,z][1] = c;
				}
			}
		}
		for(int x = 0; x < Resolution; x++) { // xy rays
			for(int y = 0; y < Resolution; y++) {
				foreach(Crossing c in Rays[0][x,y].Crossings) {
					int z = (int)c.Z;
					voxels[x,y,z][2] = c;
				}
			}
		}

		return voxels;
	}

	public static void PrintVoxelData(Crossing[,,][] voxels) {
		string result = "PrintVoxelData()";
		for(int y = 0; y < voxels.GetLength(0); y++) {
			result += "\nxz slice at y = " + y;
			for(int x = 0; x < voxels.GetLength(0); x++) {
				for(int z = 0; z < voxels.GetLength(0); z++) {
					Crossing[] crossings = voxels[x,y,z];
					result += "{";
					for(int i = 0; i < 3; i++) {
						if(crossings[i].Z != 0) {
							result += "C ";
						}
						else {
							result += "0 ";
						}
					}
					result += "} ";
				}
				result += "\n";
			}
		}
		Debug.Log(result);

	}

	public static void PrintZList(ZList z) {
		string result = "PrintZList()\n";
		string[] types = new string[] {"yz", "xz", "xy"};
		for(int x = 0; x < z.Resolution; x++) {
			for(int y = 0; y < z.Resolution; y++) {
				Ray r = z.Rays[0][x,y];
			}
		}
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
